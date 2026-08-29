(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};

  const catalog = {
    attack: [
      { id: "steadySword", name: "안정형 검", role: "attack", risk: "낮음", speed: -0.15, zone: 0.15, great: 1.15, extreme: 1.35, traitGrade: "great", trait: 0.1, traitLabel: "좋음 이상 공격 피해 +10%" },
      { id: "berserkerSword", name: "광전사의 검", role: "attack", risk: "높음", speed: 0.3, zone: -0.2, great: 1.35, extreme: 1.8, traitGrade: "extreme", trait: 0.3, traitLabel: "극한 공격 피해 +30%" },
      { id: "standardAttack", name: "용병단 표준 장비", role: "universal", risk: "보통", universal: 0.1, traitLabel: "파티 능력치 +10%" }
    ],
    defense: [
      { id: "guardianShield", name: "수호 방패", role: "defense", risk: "낮음", speed: -0.2, zone: 0.2, great: 1.1, extreme: 1.3, traitGrade: "great", trait: 0.15, traitLabel: "좋음 이상 반격 +15%" },
      { id: "counterShield", name: "반격 방패", role: "defense", risk: "높음", speed: 0.25, zone: -0.2, great: 1.3, extreme: 1.7, traitGrade: "extreme", trait: 0.4, traitLabel: "극한 반격 +40%" },
      { id: "standardDefense", name: "용병단 표준 장비", role: "universal", risk: "보통", universal: 0.1, traitLabel: "파티 능력치 +10%" }
    ],
    recovery: [
      { id: "fieldKit", name: "야전 치료 키트", role: "recovery", risk: "낮음", speed: -0.2, zone: 0.2, great: 1.1, extreme: 1.3, traitGrade: "great", trait: 0.1, traitLabel: "좋음 이상 회복량 +10%" },
      { id: "emergencyStim", name: "응급 자극제", role: "recovery", risk: "높음", speed: 0.3, zone: -0.2, great: 1.3, extreme: 1.75, traitGrade: "extreme", trait: 0.3, traitLabel: "극한 회복량 +30%" },
      { id: "standardRecovery", name: "용병단 표준 장비", role: "universal", risk: "보통", universal: 0.1, traitLabel: "파티 능력치 +10%" }
    ]
  };

  function gradeTriggers(required, actual) {
    return required === "great" ? actual === "great" || actual === "extreme" : actual === "extreme";
  }

  api.equipment = {
    catalog,
    defaultLoadout: { attack: "steadySword", defense: "guardianShield", recovery: "fieldKit" },
    find(role, id) { return catalog[role].find((item) => item.id === id) || catalog[role][0]; },
    getStats(config, loadout, disabled) {
      const stats = { ...config.combat.player };
      if (disabled) return stats;
      const universalCount = Object.keys(loadout).filter((role) => this.find(role, loadout[role]).universal).length;
      const factor = 1 + universalCount * 0.1;
      Object.keys(stats).forEach((key) => { stats[key] = Math.round(stats[key] * factor); });
      return stats;
    },
    timingModifiers(role, loadout, disabled) {
      const item = this.find(role, loadout[role]);
      if (disabled || item.universal) return { speed: 1, zone: 1, great: null, extreme: null };
      return { speed: 1 + item.speed, zone: 1 + item.zone, great: item.great, extreme: item.extreme };
    },
    traitModifier(role, grade, loadout, disabled, traitsDisabled) {
      const item = this.find(role, loadout[role]);
      if (disabled || traitsDisabled || !item.trait || !gradeTriggers(item.traitGrade, grade)) return { multiplier: 1, label: "" };
      return { multiplier: 1 + item.trait, label: `${item.name}: ${item.traitLabel}` };
    }
  };
})(window);
