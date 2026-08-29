(function (root) {
  "use strict";
  const api = root.CombatDemo;
  const loadout = { ...api.equipment.defaultLoadout };
  const config = api.createConfig();
  const state = {
    config,
    loadout,
    controls: { forceEnemyAction: null, forceGrade: null, forceMaxTurn: false, forceAuto: null, disableEquipment: false, disableTraits: false, allowCombatEquipment: false, pendingRole: "attack" },
    condition: api.condition.create(config.condition),
    queue: api.createQueue(),
    stats: createStats(),
    combat: null,
    currentEncounter: null,
    gameOver: false,
    drawers: { equipment: false, log: false, debug: false }
  };
  let lastFrame = performance.now();
  let timeoutHandled = false;

  function createStats() { return { total:0,victory:0,defeat:0,actions:{attack:0,defense:0,recovery:0},grades:{normal:0,great:0,extreme:0,failed:0},affinity:{advantage:0,neutral:0,disadvantage:0},traits:0,empowers:0,damage:0,heal:0,turns:0,queueTimeout:0,selectionTimeout:0,manual:0,auto:0,negotiation:0,escape:0 }; }

  function startCombat(preset, encounter) {
    state.currentEncounter = encounter || null;
    state.combat = api.createCombat(config, preset || "normal", state.condition, loadout);
    state.combat.encounterId = encounter ? encounter.id : Date.now() % 100000;
    state.controls.forceMaxTurn = false;
    timeoutHandled = false;
    api.prepareTurn(state.combat, config, state.controls, loadout);
    api.ui.feedback("Q / W / E로 타이밍을 확정하세요");
  }

  function waitForEncounter() {
    state.currentEncounter = null;
    state.combat = api.createCombat(config, "normal", state.condition, loadout);
    state.combat.active = false;
    state.combat.enemyAction = null;
    state.combat.timing = null;
    state.combat.outcome = null;
    timeoutHandled = false;
    api.ui.feedback("전투 알림에서 Encounter를 선택하세요");
  }

  function enterGameOver() {
    state.gameOver = true;
    state.queue.active = null;
    state.combat.active = false;
    state.combat.timing = null;
    api.ui.feedback("게임 오버 — 전체 초기화가 필요합니다", "failed");
  }

  function finishManual() {
    const outcome = state.combat.outcome;
    state.stats.total += 1; state.stats.manual += 1; state.stats[outcome] += 1; state.stats.turns += state.combat.turn;
    const conditionResult = api.condition.apply(state.condition, outcome, config.condition);
    const encounter = state.currentEncounter || { id:state.combat.encounterId,caravan:"훈련장",enemy:state.combat.enemy };
    api.recordResult(state.queue, encounter, { method:"manual",reason:"PlayerSelectedManual",outcome,condition:conditionResult });
    if (state.condition.current <= 0) enterGameOver();
    else {
      api.ui.feedback(outcome === "victory" ? "승리 — 다음 Encounter를 선택하세요" : "패배 — 다음 Encounter를 선택하세요", outcome === "victory" ? "extreme" : "failed");
      state.combat.active = false;
      state.combat.timing = null;
      state.currentEncounter = null;
    }
  }

  function takeAction(action) {
    if (state.gameOver || !state.combat.active || !state.combat.timing || timeoutHandled) return;
    const grade = api.gradeTiming(state.combat.timing, state.controls.forceGrade);
    state.combat.timing.active = false; state.combat.timing.grade = grade;
    const log = api.resolveTurn(state.combat, action, grade, config, state.controls, loadout);
    state.stats.actions[action] += 1; state.stats.grades[grade] += 1; state.stats.affinity[log.affinity] += 1; state.stats.damage += log.damage; state.stats.heal += log.heal;
    if (log.trait) state.stats.traits += 1; if (log.empowered) state.stats.empowers += 1;
    api.ui.feedback(gradeLabels(grade), grade); api.ui.pulse(action);
    state.controls.forceGrade = null;
    if (state.combat.outcome) finishManual(); else { state.controls.pendingRole = action; api.prepareTurn(state.combat, config, state.controls, loadout); timeoutHandled = false; }
  }

  function handleFailedTiming() {
    if (timeoutHandled || state.combat.outcome) return;
    timeoutHandled = true;
    const log = api.resolveTurn(state.combat, null, "failed", config, state.controls, loadout);
    state.stats.grades.failed += 1; state.stats.affinity[log.affinity] += 1; state.stats.damage += log.damage;
    api.ui.feedback("실패", "failed");
    if (state.combat.outcome) finishManual(); else { api.prepareTurn(state.combat, config, state.controls, loadout); timeoutHandled = false; }
  }

  function gradeLabels(grade) { return {normal:"보통",great:"좋음",extreme:"극한",failed:"실패"}[grade]; }
  function toggleDrawer(name, requested) {
    const next = requested === undefined ? !state.drawers[name] : requested;
    Object.keys(state.drawers).forEach((key) => { state.drawers[key] = key === name ? next : false; document.querySelector(`#${key}-drawer`).classList.toggle("hidden", !state.drawers[key]); });
  }
  function activate(id) { if (!state.gameOver && !state.combat.active && api.activateEncounter(state.queue,id,config)) api.ui.render(state); }
  function resolve(method) {
    const encounter = state.queue.active; if (state.gameOver || !encounter) return;
    if (method === "manual") { if (api.condition.status(state.condition) === "incapacitated") return; state.queue.active=null; startCombat(encounter.preset,encounter); return; }
    let result;
    if(method==="auto") result=api.resolveAuto(encounter,state,"PlayerSelectedAuto");
    if(method==="negotiation") result=api.resolveNegotiation(encounter,state);
    if(method==="escape") result=api.resolveEscape(encounter,state);
    state.stats[method] += 1; state.stats.total += 1; state.stats[result.outcome] += 1;
    api.recordResult(state.queue,encounter,result); state.queue.active=null;
    if(state.condition.current<=0) enterGameOver();
  }
  function setConfig(path, raw) { const keys=path.split("."); let target=config; keys.slice(0,-1).forEach((key)=>{target=target[key];}); const key=keys[keys.length-1]; target[key]=typeof target[key]==="number"?Number(raw):raw; if(path.startsWith("condition.max")){state.condition.max=config.condition.max;state.condition.current=Math.min(state.condition.current,state.condition.max);} }
  function force(command) {
    const [type,value]=command.split(":");
    if(type==="enemy") state.controls.forceEnemyAction=value==="none"?null:value;
    if(type==="grade") state.controls.forceGrade=value;
    if(type==="pressure") state.combat.pressure=value==="max"?config.pressure.threshold:Number(value);
    if(type==="condition"){state.condition.incapacitated=value==="incapacitated";state.condition.current=value==="max"?state.condition.max:0;}
    if(type==="maxturn"){state.controls.forceMaxTurn=true;takeAction("attack");}
    if(type==="auto") state.controls.forceAuto=value;
    if(type==="queue"&&state.queue.items[0]) state.queue.items[0].remaining=0;
    if(type==="selection"&&state.queue.active) state.queue.active.selectionRemaining=0;
    if(type==="equipment") state.controls.disableEquipment=!state.controls.disableEquipment;
    if(type==="traits") state.controls.disableTraits=!state.controls.disableTraits;
    if(type==="combat" && !state.gameOver && state.queue.items[0]) { const encounter=api.activateEncounter(state.queue,state.queue.items[0].id,config); state.queue.active=null; startCombat(encounter.preset,encounter); }
    if(type==="stats") state.stats=createStats();
  }
  function resetPlaytest() {
    state.condition = api.condition.create(config.condition);
    state.queue = api.createQueue();
    state.stats = createStats();
    state.gameOver = false;
    state.controls.forceGrade = null;
    state.controls.forceMaxTurn = false;
    state.controls.forceAuto = null;
    api.spawnEncounter(state.queue,config,"weak");
    api.spawnEncounter(state.queue,config,"normal");
    waitForEncounter();
  }
  function key(event) {
    if(event.key==="F1"){event.preventDefault();toggleDrawer("debug");return;}
    if(event.repeat||event.target.matches("input,select"))return;
    if(state.gameOver)return;
    const key=event.key.toLowerCase();
    if(state.queue.active){const map={q:"manual",w:"auto",e:"negotiation",r:"escape"};if(map[key])resolve(map[key]);return;}
    const map={q:"attack",w:"defense",e:"recovery"};if(map[key])takeAction(map[key]);
  }

  api.ui.init({
    action:takeAction, key, activate, resolve, toggleDrawer,
    spawn(preset){if(!state.gameOver)api.spawnEncounter(state.queue,config,preset);},
    equip(role,id){ if(!state.combat.active||state.controls.allowCombatEquipment)state.loadout[role]=id; },
    config:setConfig, autoSpawn(value){config.queue.autoSpawn=value;}, force, resetPlaytest
  });

  waitForEncounter();
  api.spawnEncounter(state.queue,config,"weak"); api.spawnEncounter(state.queue,config,"normal");
  function frame(now) {
    const delta=Math.min(.1,(now-lastFrame)/1000);lastFrame=now;
    if(state.combat.active&&state.combat.timing){api.updateTiming(state.combat.timing,delta);if(state.combat.timing.grade==="failed")handleFailedTiming();}
    if(!state.gameOver) api.updateQueue(state,delta);
    if(!state.gameOver && state.condition.current<=0) enterGameOver();
    state.queue.toasts=state.queue.toasts.filter((toast)=>toast.expires>now);
    api.ui.render(state); requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  root.__combatDemoState = state;
})(window);
