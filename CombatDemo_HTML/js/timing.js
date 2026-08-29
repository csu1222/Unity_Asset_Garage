(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};

  function wrap(value) { return ((value % 1) + 1) % 1; }
  function centeredZone(center, size) { return { start: wrap(center - size / 2), end: wrap(center + size / 2), size }; }
  function contains(zone, progress) {
    return zone.start <= zone.end ? progress >= zone.start && progress <= zone.end : progress >= zone.start || progress <= zone.end;
  }

  api.createTimingState = function createTimingState(config, modifiers) {
    let center = config.zoneCenter;
    if (config.pattern === "random") center = 0.12 + Math.random() * 0.76;
    const greatSize = Math.min(0.9, config.greatSize * modifiers.zone);
    const extremeSize = Math.min(greatSize, config.extremeSize * modifiers.zone);
    return {
      progress: 0,
      direction: 1,
      zoneCenter: center,
      greatZone: centeredZone(center, greatSize),
      extremeZone: centeredZone(center, extremeSize),
      movementPattern: config.pattern,
      speed: config.baseCyclesPerSecond * (config.speedPreset === "fast" ? config.fastMultiplier : 1) * modifiers.speed,
      decisionElapsed: 0,
      decisionDuration: config.decisionDuration,
      active: true,
      grade: null
    };
  };

  api.updateTiming = function updateTiming(state, deltaSeconds) {
    if (!state.active) return state;
    state.decisionElapsed += deltaSeconds;
    state.progress = wrap(state.progress + deltaSeconds * state.speed * state.direction);
    if (state.movementPattern === "moving") {
      state.zoneCenter = 0.5 + Math.sin(state.decisionElapsed * 2.4) * 0.28;
      state.greatZone = centeredZone(state.zoneCenter, state.greatZone.size);
      state.extremeZone = centeredZone(state.zoneCenter, state.extremeZone.size);
    }
    if (state.decisionElapsed >= state.decisionDuration) {
      state.active = false;
      state.grade = "failed";
    }
    return state;
  };

  api.gradeTiming = function gradeTiming(state, forcedGrade) {
    if (forcedGrade) return forcedGrade;
    if (!state.active) return "failed";
    if (contains(state.extremeZone, state.progress)) return "extreme";
    if (contains(state.greatZone, state.progress)) return "great";
    return "normal";
  };
})(window);
