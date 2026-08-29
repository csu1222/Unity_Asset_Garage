(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};
  const actions = ["attack", "defense", "recovery"];
  const beats = { attack: "recovery", recovery: "defense", defense: "attack" };

  function affinityFor(player, enemy) {
    if (player === enemy) return "neutral";
    return beats[player] === enemy ? "advantage" : "disadvantage";
  }

  function selectEnemyAction(enemy, forced) {
    if (forced) return forced;
    const choices = enemy.hp >= enemy.maxHp ? ["attack", "defense"] : actions;
    return choices[Math.floor(Math.random() * choices.length)];
  }

  function gradeMultiplier(config, grade, equipmentModifier) {
    if (grade === "failed") return 0;
    if (grade === "great" && equipmentModifier.great) return equipmentModifier.great;
    if (grade === "extreme" && equipmentModifier.extreme) return equipmentModifier.extreme;
    return config.timing[`${grade}Multiplier`];
  }

  function pressureDelta(action, grade) {
    if (grade === "failed") return 0;
    if (action === "recovery") return 1;
    if (grade === "great" || grade === "extreme") return -1;
    return 0;
  }

  function round(value) { return Math.max(0, Math.round(value)); }

  api.affinityFor = affinityFor;
  api.createCombat = function createCombat(config, enemyPreset, condition, loadout) {
    const playerStats = api.equipment.getStats(config, loadout, false);
    const source = config.combat.enemies[enemyPreset];
    return {
      active: true,
      turn: 1,
      player: { ...playerStats, hp: playerStats.maxHp },
      enemy: { ...source, preset: enemyPreset, hp: source.maxHp },
      pressure: 0,
      condition,
      enemyAction: null,
      timing: null,
      logs: [],
      maxPressure: 0,
      outcome: null,
      encounterId: 0
    };
  };

  api.prepareTurn = function prepareTurn(state, config, controls, loadout) {
    state.enemyAction = selectEnemyAction(state.enemy, controls.forceEnemyAction);
    const role = controls.pendingRole || "attack";
    const modifiers = api.equipment.timingModifiers(role, loadout, controls.disableEquipment);
    state.timing = api.createTimingState(config.timing, modifiers);
  };

  api.resolveTurn = function resolveTurn(state, action, grade, config, controls, loadout) {
    if (!state.active) return null;
    if (grade === "failed") action = null;
    const enemyAction = state.enemyAction;
    const affinity = action ? affinityFor(action, enemyAction) : "neutral";
    const affinityMultiplier = config.affinity[affinity];
    const equipmentModifier = action ? api.equipment.timingModifiers(action, loadout, controls.disableEquipment) : { great: null, extreme: null };
    const timingMultiplier = action ? gradeMultiplier(config, grade, equipmentModifier) : 0;
    const trait = action ? api.equipment.traitModifier(action, grade, loadout, controls.disableEquipment, controls.disableTraits) : { multiplier: 1, label: "" };
    const pressureBefore = state.pressure;
    const delta = action ? pressureDelta(action, grade) : 0;
    state.pressure = Math.max(0, Math.min(config.pressure.threshold, state.pressure + delta));
    state.maxPressure = Math.max(state.maxPressure, state.pressure);
    const empowered = state.pressure >= config.pressure.threshold;
    let finalValue = 0;
    let damage = 0;
    let heal = 0;
    let counter = 0;

    if (action === "attack") {
      finalValue = round(state.player.attack * affinityMultiplier * timingMultiplier * trait.multiplier);
      damage = finalValue;
      state.enemy.hp = Math.max(0, state.enemy.hp - damage);
    } else if (action === "defense") {
      const effectiveDefense = state.player.defense * affinityMultiplier * timingMultiplier;
      counter = enemyAction === "attack"
        ? round(state.player.defense * 0.5 * affinityMultiplier * timingMultiplier * trait.multiplier)
        : 0;
      finalValue = round(effectiveDefense);
      state.enemy.hp = Math.max(0, state.enemy.hp - counter);
    } else if (action === "recovery") {
      finalValue = round(state.player.recovery * affinityMultiplier * timingMultiplier * trait.multiplier);
      const before = state.player.hp;
      state.player.hp = Math.min(state.player.maxHp, state.player.hp + finalValue);
      heal = state.player.hp - before;
    }

    if (state.enemy.hp > 0) {
      const enemyMultiplier = empowered ? config.pressure.enemyMultiplier : 1;
      if (enemyAction === "attack") {
        const incoming = state.enemy.attack * enemyMultiplier;
        const defense = action === "defense" ? finalValue : 0;
        const taken = Math.max(1, Math.ceil(incoming * config.combat.defenseScale / (defense + config.combat.defenseScale)));
        state.player.hp = Math.max(0, state.player.hp - taken);
        damage += taken;
      } else if (enemyAction === "defense" && action === "attack") {
        const guarded = Math.max(1, Math.ceil(finalValue * config.combat.defenseScale / (state.enemy.defense * enemyMultiplier + config.combat.defenseScale)));
        const restored = finalValue - guarded;
        state.enemy.hp = Math.min(state.enemy.maxHp, state.enemy.hp + restored);
        damage = guarded;
      } else if (enemyAction === "recovery") {
        state.enemy.hp = Math.min(state.enemy.maxHp, state.enemy.hp + round(state.enemy.recovery * enemyMultiplier));
      }
      if (empowered) state.pressure = 0;
    }

    const log = { turn: state.turn, enemyAction, playerAction: action || "none", affinity, affinityMultiplier, grade, timingMultiplier, finalValue, damage, heal, counter, pressureBefore, pressureAfter: state.pressure, pressureDelta: delta, empowered, trait: trait.label };
    state.logs.unshift(log);
    if (state.enemy.hp <= 0) state.outcome = "victory";
    else if (state.player.hp <= 0) state.outcome = "defeat";
    else if (state.turn >= config.combat.maxTurn || controls.forceMaxTurn) {
      const playerRatio = state.player.hp / state.player.maxHp;
      const enemyRatio = state.enemy.hp / state.enemy.maxHp;
      const chance = playerRatio / (playerRatio + enemyRatio);
      const roll = api.deterministicRoll([config.worldSeed, state.encounterId, "MaxTurn"]);
      state.outcome = roll < chance ? "victory" : "defeat";
      log.maxTurnChance = chance;
      log.maxTurnRoll = roll;
    }
    if (state.outcome) state.active = false;
    else state.turn += 1;
    return log;
  };
})(window);
