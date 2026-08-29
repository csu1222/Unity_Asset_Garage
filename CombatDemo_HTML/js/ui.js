(function (root) {
  "use strict";
  const api = root.CombatDemo = root.CombatDemo || {};
  const $ = (selector) => document.querySelector(selector);
  const $$ = (selector) => Array.from(document.querySelectorAll(selector));
  const actionLabels = { attack: "⚔ 공격", defense: "◆ 방어", recovery: "✚ 회복", none: "행동 없음" };
  const gradeLabels = { normal: "보통", great: "좋음", extreme: "극한", failed: "실패" };
  const affinityLabels = { advantage: "유리", neutral: "중립", disadvantage: "불리" };
  const methodLabels = { manual: "직접 전투", auto: "자동 전투", negotiation: "협상", escape: "도주" };
  let debugSignature = "";
  let queueSignature = "";
  let timingPreviewSignature = "";
  let timingPreview = null;

  function percent(value) { return `${Math.round(value * 100)}%`; }
  function setBar(selector, value) { $(selector).style.width = `${Math.max(0, Math.min(1, value)) * 100}%`; }
  function zoneStyle(element, zone) { element.style.left = `${zone.start * 100}%`; element.style.width = `${zone.size * 100}%`; }

  function renderTiming(state) {
    let timing = state.combat.timing;
    const isPreview = !timing;
    if (isPreview) {
      const nextPreviewSignature = JSON.stringify(state.config.timing);
      if (nextPreviewSignature !== timingPreviewSignature) {
        timingPreview = api.createTimingState(state.config.timing, { speed: 1, zone: 1 });
        timingPreview.active = false;
        timingPreview.progress = 0.16;
        timingPreviewSignature = nextPreviewSignature;
      }
      timing = timingPreview;
    }
    const view = state.config.timing.view;
    $$(".timing-view").forEach((element) => element.classList.add("hidden"));
    $(`#timing-${view}`).classList.remove("hidden");
    $("#timing-view-name").textContent = { linear: "선형 타이밍", circle: "수렴 원형", radial: "방사형 핀" }[view];
    $("#timing-clock").textContent = isPreview ? "대기" : `${Math.max(0, timing.decisionDuration - timing.decisionElapsed).toFixed(1)}초`;
    zoneStyle($("#timing-linear .great"), timing.greatZone);
    zoneStyle($("#timing-linear .extreme"), timing.extremeZone);
    $("#timing-linear .cursor").style.left = `${timing.progress * 100}%`;
    $("#timing-circle .closing-ring").style.transform = `translate(-50%,-50%) scale(${Math.max(.18, 1 - timing.progress * .82)})`;
    $("#timing-radial .radial-pin").style.transform = `translate(-50%,-100%) rotate(${timing.progress * 360}deg)`;
    const circumference = 553;
    const setArc = (selector, zone) => {
      const el = $(selector); const length = circumference * zone.size;
      el.style.strokeDasharray = `${length} ${circumference - length}`;
      el.style.strokeDashoffset = `${-circumference * zone.start}`;
    };
    setArc("#timing-radial .radial-great", timing.greatZone);
    setArc("#timing-radial .radial-extreme", timing.extremeZone);
  }

  function renderQueue(state) {
    $("#queue-count").textContent = state.queue.items.length;
    const container = $("#queue-list");
    const nextSignature = JSON.stringify({
      ids: state.queue.items.map((item) => [item.id, item.caravan, item.enemy.name, item.enemy.threat]),
      active: Boolean(state.queue.active),
      combat: state.combat.active,
      gameOver: state.gameOver
    });
    if (nextSignature !== queueSignature) {
      if (!state.queue.items.length) container.innerHTML = '<div class="queue-empty">새 전투 알림이 없습니다.<br>＋ 버튼으로 생성하세요.</div>';
      else container.innerHTML = state.queue.items.map((item) => `<article class="queue-item"><header><span>${item.caravan}</span><b data-queue-time="${item.id}">0.0초</b></header><h3>${item.enemy.name}</h3><div class="queue-meta"><span>위험도 ${item.enemy.threat}</span><span data-queue-chance="${item.id}">자동 승률 --</span></div><button data-activate="${item.id}" ${state.queue.active || state.combat.active || state.gameOver ? "disabled" : ""}>처리하기</button></article>`).join("");
      queueSignature = nextSignature;
    }
    const player = api.equipment.getStats(state.config, state.loadout, state.controls.disableEquipment);
    state.queue.items.forEach((item) => {
      const time = container.querySelector(`[data-queue-time="${item.id}"]`);
      const chance = container.querySelector(`[data-queue-chance="${item.id}"]`);
      if (time) time.textContent = `${Math.max(0, item.remaining).toFixed(1)}초`;
      if (chance) chance.textContent = `자동 승률 ${percent(api.autoChance(player, item.enemy, state.condition, state.config).final)}`;
    });
  }

  function renderEquipment(state) {
    const names = {};
    Object.keys(state.loadout).forEach((role) => { names[role] = api.equipment.find(role, state.loadout[role]).name; $(`#${role}-equipment`).textContent = names[role]; });
    $("#equipment-content").innerHTML = Object.keys(api.equipment.catalog).map((role) => `<section class="equipment-role"><h3>${{attack:"공격",defense:"방어",recovery:"회복"}[role]} 용병</h3>${api.equipment.catalog[role].map((item) => `<label class="equipment-option"><input type="radio" name="equipment-${role}" data-role="${role}" value="${item.id}" ${state.loadout[role] === item.id ? "checked" : ""} ${state.combat.active && !state.controls.allowCombatEquipment ? "disabled" : ""}><b>${item.name} · ${item.risk} 위험</b><small>${item.traitLabel}</small></label>`).join("")}</section>`).join("");
  }

  function renderLog(state) {
    const combatLogs = state.combat.logs.map((log) => `<article class="log-entry"><b>턴 ${log.turn} · ${actionLabels[log.playerAction]} vs ${actionLabels[log.enemyAction]}</b><p>${affinityLabels[log.affinity]} ×${log.affinityMultiplier.toFixed(2)} / ${gradeLabels[log.grade]} ×${log.timingMultiplier.toFixed(2)}</p><p>최종값 ${log.finalValue} · 피해 ${log.damage} · 회복 ${log.heal} · 압박 ${log.pressureBefore}→${log.pressureAfter}${log.empowered ? " · 적 강화" : ""}</p>${log.trait ? `<p>${log.trait}</p>` : ""}</article>`).join("");
    const ledger = state.queue.ledger.map((entry) => `<article class="log-entry"><b>${entry.caravan} · ${entry.enemyName}</b><p>${methodLabels[entry.method]} ${entry.outcome === "victory" ? "승리" : "패배"} · ${entry.reason}</p><p>컨디션 ${entry.condition.before} → ${entry.condition.after}</p></article>`).join("");
    $("#log-content").innerHTML = combatLogs + ledger || '<div class="queue-empty">아직 기록이 없습니다.</div>';
  }

  function field(label, path, value, type, options) {
    if (type === "select") return `<label class="field"><span>${label}</span><select data-config="${path}">${options.map(([v,l]) => `<option value="${v}" ${String(value) === v ? "selected" : ""}>${l}</option>`).join("")}</select></label>`;
    return `<label class="field"><span>${label}</span><input data-config="${path}" type="${type || "number"}" value="${value}" step="0.01"></label>`;
  }

  function renderDebug(state) {
    const c = state.config;
    $("#debug-content").innerHTML = `
      <section class="debug-section"><h3>전투 / 상성</h3>${field("최대 턴", "combat.maxTurn", c.combat.maxTurn)}${field("방어 스케일", "combat.defenseScale", c.combat.defenseScale)}${field("유리 배율", "affinity.advantage", c.affinity.advantage)}${field("중립 배율", "affinity.neutral", c.affinity.neutral)}${field("불리 배율", "affinity.disadvantage", c.affinity.disadvantage)}</section>
      <section class="debug-section"><h3>타이밍</h3>${field("화면", "timing.view", c.timing.view,"select",[["linear","선형"],["circle","수렴 원형"],["radial","방사형 핀"]])}${field("패턴", "timing.pattern", c.timing.pattern,"select",[["static","고정"],["random","랜덤 위치"],["moving","이동 타겟"]])}${field("속도", "timing.speedPreset", c.timing.speedPreset,"select",[["normal","기본 ×1.0"],["fast","고속 ×1.3"]])}${field("판정 시간", "timing.decisionDuration", c.timing.decisionDuration)}${field("좋음 영역", "timing.greatSize", c.timing.greatSize)}${field("극한 영역", "timing.extremeSize", c.timing.extremeSize)}${field("영역 중심", "timing.zoneCenter", c.timing.zoneCenter)}</section>
      <section class="debug-section"><h3>압박 / 컨디션</h3>${field("압박 임계값", "pressure.threshold", c.pressure.threshold)}${field("적 강화 배율", "pressure.enemyMultiplier", c.pressure.enemyMultiplier)}${field("최대 컨디션", "condition.max", c.condition.max)}${field("승리 손실", "condition.victoryLoss", c.condition.victoryLoss)}${field("패배 손실", "condition.defeatLoss", c.condition.defeatLoss)}</section>
      <section class="debug-section"><h3>자동전투 / 큐</h3>${field("파워 곡선", "auto.exponent", c.auto.exponent)}${field("컨디션 최대 페널티", "auto.maxConditionPenalty", c.auto.maxConditionPenalty)}${field("큐 용량", "queue.capacity", c.queue.capacity)}${field("큐 대기 시간", "queue.waitDuration", c.queue.waitDuration)}${field("선택 시간", "queue.selectionDuration", c.queue.selectionDuration)}${field("생성 간격", "queue.spawnInterval", c.queue.spawnInterval)}<label class="field"><span>자동 생성</span><input data-toggle="autoSpawn" type="checkbox" ${c.queue.autoSpawn ? "checked" : ""}></label></section>
      <section class="debug-section"><h3>강제 제어</h3><div class="force-grid"><button data-force="enemy:attack">적 공격</button><button data-force="enemy:defense">적 방어</button><button data-force="enemy:recovery">적 회복</button><button data-force="enemy:none">적 랜덤</button><button data-force="grade:normal">보통 판정</button><button data-force="grade:great">좋음 판정</button><button data-force="grade:extreme">극한 판정</button><button data-force="grade:failed">실패 판정</button><button data-force="pressure:0">압박 0</button><button data-force="pressure:max">압박 임계</button><button data-force="condition:max">컨디션 최대</button><button data-force="condition:0">위험 상태</button><button data-force="condition:incapacitated">전투 불능</button><button data-force="maxturn:now">최대 턴 판정</button><button data-force="auto:victory">자동 승리</button><button data-force="auto:defeat">자동 패배</button><button data-force="queue:timeout">큐 시간 초과</button><button data-force="selection:timeout">선택 시간 초과</button><button data-force="equipment:toggle">장비 효과 ON/OFF</button><button data-force="traits:toggle">특성 ON/OFF</button><button data-force="combat:new">새 직접 전투</button><button data-force="stats:reset">통계 초기화</button></div></section>
      <section class="debug-section"><h3>통계</h3><div class="log-entry"><p>전투 ${state.stats.total} · 승리 ${state.stats.victory} · 패배 ${state.stats.defeat}</p><p>공격 ${state.stats.actions.attack} · 방어 ${state.stats.actions.defense} · 회복 ${state.stats.actions.recovery}</p><p>보통 ${state.stats.grades.normal} · 좋음 ${state.stats.grades.great} · 극한 ${state.stats.grades.extreme} · 실패 ${state.stats.grades.failed}</p><p>큐 초과 ${state.stats.queueTimeout} · 선택 초과 ${state.stats.selectionTimeout} · 특성 ${state.stats.traits}</p></div></section>`;
  }

  api.ui = {
    init(commands) {
      $("#equipment-toggle").onclick = () => commands.toggleDrawer("equipment");
      $("#log-toggle").onclick = () => commands.toggleDrawer("log");
      $("#debug-toggle").onclick = () => commands.toggleDrawer("debug");
      $("#spawn-quick").onclick = () => commands.spawn("random");
      $("#reset-playtest").onclick = () => commands.resetPlaytest();
      $$("[data-command]").forEach((button) => { button.onclick = () => commands.action(button.dataset.command); });
      $$("[data-resolution]").forEach((button) => { button.onclick = () => commands.resolve(button.dataset.resolution); });
      document.addEventListener("click", (event) => {
        const activate = event.target.closest("[data-activate]"); if (activate) commands.activate(Number(activate.dataset.activate));
        const close = event.target.closest("[data-close]"); if (close) commands.toggleDrawer(close.dataset.close, false);
        const force = event.target.closest("[data-force]"); if (force) commands.force(force.dataset.force);
      });
      document.addEventListener("change", (event) => {
        if (event.target.matches("[data-role]")) commands.equip(event.target.dataset.role, event.target.value);
        if (event.target.matches("[data-config]")) commands.config(event.target.dataset.config, event.target.value);
        if (event.target.matches("[data-toggle='autoSpawn']")) commands.autoSpawn(event.target.checked);
      });
      document.addEventListener("keydown", (event) => commands.key(event));
    },
    render(state) {
      const combat = state.combat;
      $("#enemy-name").textContent = combat.enemy.name; $("#enemy-threat").textContent = combat.enemy.threat;
      $("#enemy-hp").textContent = `${combat.enemy.hp} / ${combat.enemy.maxHp}`; setBar("#enemy-hp-bar", combat.enemy.hp / combat.enemy.maxHp);
      $("#player-hp").textContent = `${combat.player.hp} / ${combat.player.maxHp}`; setBar("#player-hp-bar", combat.player.hp / combat.player.maxHp);
      $("#condition-value").textContent = `${state.condition.current} / ${state.condition.max}`; setBar("#condition-bar", state.condition.current / state.condition.max);
      const status = api.condition.status(state.condition); const badge = $("#condition-badge"); badge.className = `badge ${status}`; badge.textContent = {normal:"정상",critical:"위험 상태",incapacitated:"전투 불능"}[status];
      $("#turn-value").textContent = `${combat.turn} / ${state.config.combat.maxTurn}`; $("#pressure-value").textContent = `${combat.pressure} / ${state.config.pressure.threshold}`;
      $("#pressure-dots").innerHTML = Array.from({length:state.config.pressure.threshold},(_,i)=>`<i class="${i < combat.pressure ? "on" : ""}"></i>`).join("");
      $("#pressure-warning").textContent = combat.pressure >= state.config.pressure.threshold ? "⚠ 다음 적 행동 강화" : "안정";
      $("#enemy-intent").textContent = combat.enemyAction ? actionLabels[combat.enemyAction] : "대기 중"; $("#empower-label").textContent = combat.pressure >= state.config.pressure.threshold ? "[강화]" : "";
      $$(".rps-triangle b").forEach((node) => node.classList.toggle("active", node.dataset.action === combat.enemyAction));
      $("#affinity-preview").textContent = combat.enemyAction ? `공격 ${affinityLabels[api.affinityFor("attack",combat.enemyAction)]} · 방어 ${affinityLabels[api.affinityFor("defense",combat.enemyAction)]} · 회복 ${affinityLabels[api.affinityFor("recovery",combat.enemyAction)]}` : "적 의도를 확인하세요";
      renderTiming(state); renderQueue(state); renderEquipment(state); renderLog(state);
      const nextDebugSignature = JSON.stringify({ config:state.config, stats:state.stats, controls:state.controls, condition:state.condition, pressure:state.combat.pressure, gameOver:state.gameOver });
      if (nextDebugSignature !== debugSignature) { renderDebug(state); debugSignature = nextDebugSignature; }
      $("#resolution-modal").classList.toggle("hidden", !state.queue.active);
      $("#gameover-modal").classList.toggle("hidden", !state.gameOver);
      $$("[data-command]").forEach((button) => { button.disabled = state.gameOver || !combat.active; });
      if (state.queue.active) { const active=state.queue.active; const chance=api.autoChance(api.equipment.getStats(state.config,state.loadout,state.controls.disableEquipment),active.enemy,state.condition,state.config).final; $("#modal-title").textContent=active.enemy.name; $("#modal-details").innerHTML=`<span>위험도 <b>${active.enemy.threat}</b></span><span>자동 승률 <b>${percent(chance)}</b></span><span>협상 비용 <b>${Math.round(state.config.negotiation.baseCost*active.enemy.maxHp/100)} Gold</b></span><span>도주 성공률 <b>${percent(state.config.escape.baseChance)}</b></span>`; $("#selection-time").textContent=`${Math.max(0,active.selectionRemaining).toFixed(1)}초`; $("#selection-bar").style.width=percent(active.selectionRemaining/state.config.queue.selectionDuration); $("[data-resolution='manual']").disabled=status==="incapacitated"; }
      $("#toast-stack").innerHTML=state.queue.toasts.map((toast)=>`<article class="toast"><b>${methodLabels[toast.method]} ${toast.outcome==="victory"?"승리":"패배"}</b><span>${toast.caravan} · ${toast.enemyName}</span><span>컨디션 ${toast.condition.before} → ${toast.condition.after}</span></article>`).join("");
    },
    feedback(text, grade) { const el=$("#grade-feedback"); el.textContent=text; el.style.color={normal:"#eef3f8",great:"#5aa7ff",extreme:"#d9ad62",failed:"#ef6262"}[grade]||"#8e9aad"; },
    pulse(role){ $$(".mercenary").forEach((el)=>el.classList.toggle("active",el.dataset.role===role)); setTimeout(()=>$$('.mercenary').forEach((el)=>el.classList.remove('active')),300); }
  };
})(window);
