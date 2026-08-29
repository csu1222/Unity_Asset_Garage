(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};

  api.createQueue = function createQueue() {
    return { items: [], active: null, nextId: 1, createdOrder: 1, ledger: [], toasts: [], spawnElapsed: 0 };
  };

  api.spawnEncounter = function spawnEncounter(queue, config, preset) {
    if (queue.items.length >= config.queue.capacity) return null;
    const chosen = preset === "random" ? ["weak", "normal", "strong"][Math.floor(Math.random() * 3)] : preset;
    const enemy = { ...config.combat.enemies[chosen] };
    const encounter = { id: queue.nextId++, createdOrder: queue.createdOrder++, caravan: `상단 ${String.fromCharCode(64 + ((queue.nextId - 2) % 4) + 1)}`, preset: chosen, enemy, remaining: config.queue.waitDuration, selectionRemaining: null };
    queue.items.push(encounter);
    return encounter;
  };

  api.activateEncounter = function activateEncounter(queue, id, config) {
    if (queue.active) return null;
    const index = queue.items.findIndex((item) => item.id === id);
    if (index < 0) return null;
    queue.active = queue.items.splice(index, 1)[0];
    queue.active.selectionRemaining = config.queue.selectionDuration;
    return queue.active;
  };

  api.recordResult = function recordResult(queue, encounter, result) {
    const entry = { id: encounter.id, caravan: encounter.caravan, enemyName: encounter.enemy.name, time: new Date().toLocaleTimeString("ko-KR"), ...result };
    queue.ledger.unshift(entry);
    queue.toasts.push({ ...entry, expires: performance.now() + 4200 });
    return entry;
  };

  api.updateQueue = function updateQueue(appState, delta) {
    const queue = appState.queue;
    const timedOut = [];
    if (appState.config.queue.autoSpawn) {
      queue.spawnElapsed += delta;
      if (queue.spawnElapsed >= appState.config.queue.spawnInterval) {
        queue.spawnElapsed = 0;
        api.spawnEncounter(queue, appState.config, "random");
      }
    }
    if (queue.active) {
      queue.active.selectionRemaining -= delta;
      if (queue.active.selectionRemaining <= 0) {
        const active = queue.active;
        queue.active = null;
        api.recordResult(queue, active, api.resolveAuto(active, appState, "SelectionTimeout"));
        appState.stats.selectionTimeout += 1;
      }
    }
    queue.items.forEach((item) => { item.remaining -= delta; if (item.remaining <= 0) timedOut.push(item); });
    timedOut.sort((a, b) => a.createdOrder - b.createdOrder).forEach((encounter) => {
      queue.items = queue.items.filter((item) => item.id !== encounter.id);
      api.recordResult(queue, encounter, api.resolveAuto(encounter, appState, "QueueTimeout"));
      appState.stats.queueTimeout += 1;
    });
  };
})(window);
