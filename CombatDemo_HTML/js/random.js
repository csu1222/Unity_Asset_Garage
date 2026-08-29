(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};

  function hashString(text) {
    let hash = 2166136261;
    for (let i = 0; i < text.length; i += 1) {
      hash ^= text.charCodeAt(i);
      hash = Math.imul(hash, 16777619);
    }
    return hash >>> 0;
  }

  api.deterministicRoll = function deterministicRoll(parts) {
    let state = hashString(parts.join("|")) || 1;
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    return (state >>> 0) / 4294967296;
  };
})(window);
