"use strict";
global.window = global;
global.performance = global.performance || { now: () => 0 };
require("../js/config.js"); require("../js/random.js"); require("../js/equipment.js"); require("../js/timing.js"); require("../js/combat.js"); require("../js/resolution.js"); require("../js/queue.js");
const api = global.CombatDemo;
let passed = 0;
function test(name, fn) { try { fn(); passed += 1; process.stdout.write(`PASS ${name}\n`); } catch (error) { process.stderr.write(`FAIL ${name}: ${error.message}\n`); process.exitCode = 1; } }
function equal(actual, expected) { if (actual !== expected) throw new Error(`expected ${expected}, got ${actual}`); }
function close(actual, expected) { if (Math.abs(actual - expected) > 0.0001) throw new Error(`expected ${expected}, got ${actual}`); }
function makeState() { const config=api.createConfig(); const loadout={...api.equipment.defaultLoadout}; const condition=api.condition.create(config.condition); return {config,loadout,condition,controls:{disableEquipment:false,disableTraits:false,forceAuto:null},stats:{queueTimeout:0,selectionTimeout:0},queue:api.createQueue()}; }

test("Soft-RPS 9개 조합",()=>{const expected={attack:{attack:"neutral",defense:"disadvantage",recovery:"advantage"},defense:{attack:"advantage",defense:"neutral",recovery:"disadvantage"},recovery:{attack:"disadvantage",defense:"advantage",recovery:"neutral"}};Object.keys(expected).forEach(p=>Object.keys(expected[p]).forEach(e=>equal(api.affinityFor(p,e),expected[p][e])));});
test("Recovery vs Defense는 1.25 유리",()=>{const c=api.createConfig();equal(api.affinityFor("recovery","defense"),"advantage");close(c.affinity.advantage,1.25);});
test("Nested Timing Zone과 판정 우선순위",()=>{const c=api.createConfig();const t=api.createTimingState(c.timing,{speed:1,zone:1});t.progress=.7;equal(api.gradeTiming(t),"extreme");t.progress=.62;equal(api.gradeTiming(t),"great");t.progress=.2;equal(api.gradeTiming(t),"normal");});
test("Pressure 2 + Recovery는 같은 턴 강화 후 0",()=>{const c=api.createConfig();const l={...api.equipment.defaultLoadout};const s=api.createCombat(c,"normal",null,l);s.pressure=2;s.enemyAction="attack";const log=api.resolveTurn(s,"recovery","normal",c,{disableEquipment:true,disableTraits:true},l);equal(log.empowered,true);equal(s.pressure,0);});
test("Defense Counter는 Enemy Attack에만 발생",()=>{const c=api.createConfig();const l={...api.equipment.defaultLoadout};const controls={disableEquipment:true,disableTraits:true};["attack","defense","recovery"].forEach((enemyAction)=>{const s=api.createCombat(c,"normal",null,l);s.enemyAction=enemyAction;const log=api.resolveTurn(s,"defense","normal",c,controls,l);if(enemyAction==="attack"){if(log.counter<=0)throw new Error("Attack에는 Counter가 필요합니다");}else equal(log.counter,0);});});
test("Condition 상태 전이",()=>{const c=api.createConfig();const s=api.condition.create(c.condition);api.condition.apply(s,"victory",c.condition);equal(s.current,9);api.condition.apply(s,"defeat",c.condition);equal(s.current,4);api.condition.apply(s,"defeat",c.condition);equal(s.current,0);equal(api.condition.status(s),"critical");api.condition.apply(s,"defeat",c.condition);equal(api.condition.status(s),"incapacitated");});
test("Universal 장비 3개는 additive +30%",()=>{const c=api.createConfig();const s=api.equipment.getStats(c,{attack:"standardAttack",defense:"standardDefense",recovery:"standardRecovery"},false);equal(s.maxHp,130);equal(s.attack,39);});
test("결정론 난수는 같은 입력에 같은 결과",()=>{equal(api.deterministicRoll([1,2,"AutoCombat"]),api.deterministicRoll([1,2,"AutoCombat"]));});
test("QueueTimeout은 Auto Resolver로 기록",()=>{const s=makeState();const e=api.spawnEncounter(s.queue,s.config,"weak");e.remaining=0;api.updateQueue(s,.1);equal(s.queue.ledger[0].reason,"QueueTimeout");equal(s.queue.items.length,0);});
test("Active는 최대 1개",()=>{const s=makeState();const a=api.spawnEncounter(s.queue,s.config,"weak");const b=api.spawnEncounter(s.queue,s.config,"normal");api.activateEncounter(s.queue,a.id,s.config);equal(api.activateEncounter(s.queue,b.id,s.config),null);});
process.stdout.write(`${passed} tests passed\n`);
