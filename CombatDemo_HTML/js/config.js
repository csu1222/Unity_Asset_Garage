(function (root) {
  "use strict";

  root.CombatDemo = root.CombatDemo || {};
  root.CombatDemo.createConfig = function createConfig() {
    return {
      worldSeed: 2404,
      affinity: { advantage: 1.25, neutral: 1, disadvantage: 0.75 },
      timing: {
        decisionDuration: 1.5,
        greatSize: 0.2,
        extremeSize: 0.08,
        zoneCenter: 0.7,
        normalMultiplier: 1,
        greatMultiplier: 1.2,
        extremeMultiplier: 1.5,
        baseCyclesPerSecond: 0.72,
        fastMultiplier: 1.3,
        pattern: "static",
        speedPreset: "normal",
        view: "linear"
      },
      pressure: { threshold: 3, enemyMultiplier: 1.5 },
      combat: {
        maxTurn: 5,
        defenseScale: 100,
        player: { maxHp: 100, attack: 30, defense: 30, recovery: 25 },
        enemies: {
          weak: { name: "늑대 무리", threat: "낮음", maxHp: 80, attack: 20, defense: 20, recovery: 15 },
          normal: { name: "도적 습격대", threat: "높음", maxHp: 100, attack: 25, defense: 25, recovery: 20 },
          strong: { name: "중갑 용병단", threat: "매우 높음", maxHp: 120, attack: 32, defense: 32, recovery: 25 }
        }
      },
      condition: { max: 10, victoryLoss: 1, defeatLoss: 5, current: 10 },
      auto: { hpWeight: 0.35, attackWeight: 1, defenseWeight: 0.8, recoveryWeight: 0.7, exponent: 2, maxConditionPenalty: 0.2 },
      queue: { capacity: 5, waitDuration: 20, selectionDuration: 5, spawnInterval: 12, autoSpawn: false },
      negotiation: { baseCost: 780 },
      escape: { baseChance: 0.5 }
    };
  };
})(window);
