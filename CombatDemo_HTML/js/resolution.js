(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};

  function combatPower(stats, config) {
    return stats.maxHp * config.hpWeight + stats.attack * config.attackWeight + stats.defense * config.defenseWeight + stats.recovery * config.recoveryWeight;
  }

  function applyCondition(state, outcome, config) {
    const before = state.current;
    if (outcome === "victory") state.current = Math.max(0, state.current - config.victoryLoss);
    else if (state.current === 0) state.incapacitated = true;
    else state.current = Math.max(0, state.current - config.defeatLoss);
    return { before, after: state.current, status: state.incapacitated ? "incapacitated" : state.current === 0 ? "critical" : "normal" };
  }

  api.condition = {
    create(config) { return { current: config.current, max: config.max, incapacitated: false }; },
    status(state) { return state.incapacitated ? "incapacitated" : state.current === 0 ? "critical" : "normal"; },
    apply: applyCondition
  };

  api.autoChance = function autoChance(player, enemy, condition, config) {
    const playerPower = combatPower(player, config.auto);
    const enemyPower = combatPower(enemy, config.auto);
    const ratio = playerPower / enemyPower;
    const curved = Math.pow(ratio, config.auto.exponent);
    const base = curved / (1 + curved);
    const penalty = (1 - condition.current / condition.max) * config.auto.maxConditionPenalty;
    return { playerPower, enemyPower, ratio, base, penalty, final: Math.max(0, Math.min(1, base - penalty)) };
  };

  api.resolveAuto = function resolveAuto(encounter, appState, reason) {
    const player = api.equipment.getStats(appState.config, appState.loadout, appState.controls.disableEquipment);
    const chance = api.autoChance(player, encounter.enemy, appState.condition, appState.config);
    let roll = api.deterministicRoll([appState.config.worldSeed, encounter.id, encounter.createdOrder, reason, "AutoCombat"]);
    if (appState.controls.forceAuto === "victory") roll = 0;
    if (appState.controls.forceAuto === "defeat") roll = 1;
    const outcome = roll < chance.final ? "victory" : "defeat";
    return { method: "auto", reason, outcome, chance: chance.final, roll, condition: applyCondition(appState.condition, outcome, appState.config.condition) };
  };

  api.resolveNegotiation = function resolveNegotiation(encounter, appState) {
    const cost = Math.round(appState.config.negotiation.baseCost * (encounter.enemy.maxHp / 100));
    return { method: "negotiation", reason: "PlayerSelectedNegotiation", outcome: "victory", cost, condition: { before: appState.condition.current, after: appState.condition.current, status: api.condition.status(appState.condition) } };
  };

  api.resolveEscape = function resolveEscape(encounter, appState) {
    const chance = appState.config.escape.baseChance;
    const roll = api.deterministicRoll([appState.config.worldSeed, encounter.id, encounter.createdOrder, "Escape"]);
    const outcome = roll < chance ? "victory" : "defeat";
    return { method: "escape", reason: "PlayerSelectedEscape", outcome, chance, roll, condition: applyCondition(appState.condition, outcome, appState.config.condition) };
  };
})(window);
