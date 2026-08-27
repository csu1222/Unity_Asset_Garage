using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AssetGarage.CombatDemo
{
    public sealed class CombatPrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private ManualCombatBalanceConfig manualBalance;
        [SerializeField] private AutoCombatBalanceConfig autoBalance;
        [SerializeField] private ResolutionBalanceConfig resolutionBalance;
        [SerializeField] private TimingPresentationConfig presentation;
        [SerializeField] private int worldSeed = 1222;
        [SerializeField] private int playerGold = 2000;
        [SerializeField] private CombatStats player = new CombatStats(120, 120, 28, 22, 18);
        [SerializeField] private CombatStats enemy = new CombatStats(90, 90, 20, 18, 12);

        private readonly Queue<string> log = new Queue<string>();
        private CombatEncounterQueue queue; private AutoCombatResolver auto; private NegotiationResolver negotiation; private EscapeResolver escape; private ManualCombatResolver manual; private ManualCombatTimingController timing; private PressureState pressure;
        private InputAction encounterQ, encounterW, encounterE, encounterR, debugToggle, spawnAction, timeoutAction, resetAction;
        private EncounterData active; private CombatAction enemyIntent; private bool manualMode, debugVisible = true; private int turn;
        private Text playerText, enemyText, intentText, resultText, queueText, pressureText, debugText; private GameObject encounterPanel, manualPanel, debugPanel; private TimingVisualizer timingVisualizer;

        private void Awake()
        {
            EnsureConfigs(); queue = new CombatEncounterQueue(); auto = new AutoCombatResolver(autoBalance); negotiation = new NegotiationResolver(resolutionBalance, auto); escape = new EscapeResolver(resolutionBalance); manual = new ManualCombatResolver(manualBalance); timing = new ManualCombatTimingController(manualBalance); timing.Expired += ResolveFailedInput; pressure = new PressureState();
            BuildWorldIfNeeded(); BuildUI(); CreateInput(); SpawnEncounter(); Refresh();
        }
        private void OnDestroy() { encounterQ?.Dispose(); encounterW?.Dispose(); encounterE?.Dispose(); encounterR?.Dispose(); debugToggle?.Dispose(); spawnAction?.Dispose(); timeoutAction?.Dispose(); resetAction?.Dispose(); }
        private void Update() { queue.Update(Time.deltaTime, ResolveTimeout); if (manualMode) timing.Tick(Time.deltaTime); Refresh(); }

        public void ChooseManualOrAttack() { if (!manualMode) BeginManual(); else ChooseAction(CombatAction.Attack); }
        public void ChooseAutoOrDefense() { if (!manualMode) ResolveAuto(); else ChooseAction(CombatAction.Defense); }
        public void ChooseNegotiationOrRecovery() { if (!manualMode) ResolveNegotiation(); else ChooseAction(CombatAction.Recovery); }
        public void ChooseEscape() { if (!manualMode) ResolveEscape(); }
        public void SpawnEncounter()
        {
            int id = queue.Items.Count + 1 + DateTime.UtcNow.Millisecond;
            queue.Enqueue(new EncounterData { EncounterId = $"demo-{id}", DisplayName = $"Greybox Enemy {id}", CreatedTick = id, OccurrenceTime = Time.realtimeSinceStartupAsDouble, RemainingWait = resolutionBalance.DefaultQueueWaitDuration, Enemy = enemy.Copy() }); active = queue.Active; Refresh();
        }
        public void ForceQueueTimeout() { foreach (EncounterData e in queue.Items) if (e.State == EncounterState.Queued) e.RemainingWait = 0; }
        public void ResetDemo() { player.Restore(); playerGold = 2000; manualMode = false; turn = 0; SetPanels(false); AddLog("Demo reset"); }
        public void CycleTimingView() { timingVisualizer.Kind = (TimingViewKind)(((int)timingVisualizer.Kind + 1) % 3); }

        private void BeginManual()
        {
            if (queue.Active == null) return; active = queue.Active; manualMode = true; turn = 1; SetPanels(true); BeginTurn(); AddLog($"Manual: {active.DisplayName}");
        }
        private void BeginTurn() { enemyIntent = WeightedEnemyAction(active.CreatedTick + turn); intentText.text = enemyIntent.ToString().ToUpperInvariant(); timing.Start(); resultText.text = "Choose Q / W / E"; }
        private void ChooseAction(CombatAction action)
        {
            if (!manualMode || !timing.TryAccept(out TimingGrade grade)) return;
            bool empowered = pressure.ShouldEmpower(manualBalance.PressureThreshold); TurnResolution r = manual.Resolve(player, active.Enemy, action, enemyIntent, grade, empowered); if (empowered) pressure.ConsumeEmpowerment(); pressure.Record(grade);
            resultText.text = $"{grade}  P-{r.PlayerDamage} E-{r.EnemyDamage} Heal+{r.PlayerHeal} Counter {r.CounterDamage}"; AddLog($"T{turn} {enemyIntent}/{action} {grade} P-{r.PlayerDamage} E-{r.EnemyDamage}");
            if (active.Enemy.IsDead) FinishActive(CombatOutcome.Victory); else if (player.IsDead || turn >= manualBalance.MaxTurn) FinishActive(CombatOutcome.Defeat); else { turn++; BeginTurn(); }
        }
        private void ResolveFailedInput()
        {
            if (!manualMode) return;
            bool empowered = pressure.ShouldEmpower(manualBalance.PressureThreshold); TurnResolution r = manual.Resolve(player, active.Enemy, CombatAction.None, enemyIntent, TimingGrade.Failed, empowered); if (empowered) pressure.ConsumeEmpowerment(); pressure.Record(TimingGrade.Failed);
            resultText.text = $"FAILED  P-{r.PlayerDamage}"; AddLog($"T{turn} {enemyIntent}/None Failed P-{r.PlayerDamage}");
            if (player.IsDead || turn >= manualBalance.MaxTurn) FinishActive(CombatOutcome.Defeat); else { turn++; BeginTurn(); }
        }
        private void ResolveAuto() { if (queue.Active == null) return; CombatResolutionResult r = auto.Resolve(player, queue.Active, worldSeed, ResolutionReason.PlayerChoice); AddLog($"Auto {r.Outcome} roll {r.RandomRoll:F3}/{r.SuccessProbability:F3}"); FinishActive(r.Outcome); }
        private void ResolveNegotiation() { if (queue.Active == null) return; if (!negotiation.TryResolve(player, queue.Active, ref playerGold, out CombatResolutionResult r)) { resultText.text = "Insufficient Gold"; return; } AddLog($"Negotiated -{r.GoldCost}G"); FinishActive(r.Outcome); }
        private void ResolveEscape() { if (queue.Active == null) return; CombatResolutionResult r = escape.Resolve(player, queue.Active, worldSeed); AddLog($"Escape {r.Outcome} {r.RandomRoll:F3}/{r.SuccessProbability:F3}"); FinishActive(r.Outcome); }
        private void ResolveTimeout(EncounterData e) { CombatResolutionResult r = auto.Resolve(player, e, worldSeed, ResolutionReason.Timeout); AddLog($"Timeout Auto {e.DisplayName}: {r.Outcome}"); }
        private void FinishActive(CombatOutcome outcome) { resultText.text = outcome.ToString().ToUpperInvariant(); manualMode = false; queue.ResolveActive(); active = queue.Active; SetPanels(false); }
        private CombatAction WeightedEnemyAction(long key) { float a = manualBalance.AttackWeight, d = manualBalance.DefenseWeight, r = manualBalance.RecoveryWeight, total = a + d + r; if (total <= 0) return CombatAction.Attack; double roll = DeterministicRoll.Value(worldSeed, active.EncounterId, active.CaravanId, key, "EnemyAction") * total; return roll < a ? CombatAction.Attack : roll < a + d ? CombatAction.Defense : CombatAction.Recovery; }

        private void CreateInput()
        {
            encounterQ = Bind("Q", "<Keyboard>/q", _ => ChooseManualOrAttack()); encounterW = Bind("W", "<Keyboard>/w", _ => ChooseAutoOrDefense()); encounterE = Bind("E", "<Keyboard>/e", _ => ChooseNegotiationOrRecovery()); encounterR = Bind("R", "<Keyboard>/r", _ => ChooseEscape());
            debugToggle = Bind("Debug", "<Keyboard>/f1", _ => { debugVisible = !debugVisible; debugPanel.SetActive(debugVisible); }); spawnAction = Bind("Spawn", "<Keyboard>/f2", _ => SpawnEncounter()); timeoutAction = Bind("Timeout", "<Keyboard>/f3", _ => ForceQueueTimeout()); resetAction = Bind("Reset", "<Keyboard>/f4", _ => ResetDemo());
        }
        private static InputAction Bind(string name, string binding, Action<InputAction.CallbackContext> callback) { var action = new InputAction(name, InputActionType.Button, binding); action.performed += callback; action.Enable(); return action; }

        private void Refresh()
        {
            float pp = auto.Power(player); float ep = active == null ? 0 : auto.Power(active.Enemy); playerText.text = $"PLAYER  HP {player.CurrentHP}/{player.MaxHP}\nATK {player.Attack}  DEF {player.Defense}  REC {player.Recovery}\nPOWER {pp:F1}  GOLD {playerGold}";
            enemyText.text = active == null ? "NO ACTIVE ENCOUNTER" : $"{active.DisplayName}  HP {active.Enemy.CurrentHP}/{active.Enemy.MaxHP}\nATK {active.Enemy.Attack}  DEF {active.Enemy.Defense}  REC {active.Enemy.Recovery}\nPOWER {ep:F1}";
            pressureText.text = $"PRESSURE {pressure.Value}/{manualBalance.PressureThreshold}" + (pressure.ShouldEmpower(manualBalance.PressureThreshold) ? $"  ENEMY EMPOWERED x{manualBalance.EnemyPressureMultiplier:F1}" : "");
            var lines = new List<string>(); foreach (EncounterData e in queue.Items) lines.Add($"{e.State,-7} {e.DisplayName}  {Mathf.Max(0,e.RemainingWait):F1}s"); queueText.text = "ENCOUNTER QUEUE\n" + string.Join("\n", lines);
            TimingState ts = timing.State; timingVisualizer.Render(ts); debugText.text = $"F1 HUD | F2 Spawn | F3 Timeout | F4 Reset\nTurn {turn} Enemy {enemyIntent} Grade {ts.CurrentGrade}\nTime {ts.NormalizedTime:F3} Remain {ts.RemainingTime:F2} View {timingVisualizer.Kind}\nGreat {ts.GreatStart:F3} Extreme {ts.ExtremeStart:F3}\nPressure {pressure.Value}/{manualBalance.PressureThreshold}\nPower P {pp:F1} E {ep:F1} Ratio {(ep <= 0 ? 0 : pp/ep):F2} Win {(ep <= 0 ? 0 : auto.WinProbability(pp,ep)):P1}\nSeed {worldSeed}\nLOG\n{string.Join("\n", log)}";
        }
        private void AddLog(string value) { if (log.Count >= 25) log.Dequeue(); log.Enqueue(value); }
        private void SetPanels(bool manualActive) { if (encounterPanel) encounterPanel.SetActive(!manualActive); if (manualPanel) manualPanel.SetActive(manualActive); }

        private void EnsureConfigs() { if (!manualBalance) manualBalance = ScriptableObject.CreateInstance<ManualCombatBalanceConfig>(); if (!autoBalance) autoBalance = ScriptableObject.CreateInstance<AutoCombatBalanceConfig>(); if (!resolutionBalance) resolutionBalance = ScriptableObject.CreateInstance<ResolutionBalanceConfig>(); if (!presentation) presentation = ScriptableObject.CreateInstance<TimingPresentationConfig>(); }
        private void BuildWorldIfNeeded() { if (!GameObject.Find("Player")) Primitive("Player", PrimitiveType.Capsule, new Vector3(-2.5f, 1, 0), Color.cyan); if (!GameObject.Find("Enemy")) Primitive("Enemy", PrimitiveType.Capsule, new Vector3(2.5f, 1, 0), Color.red); if (!GameObject.Find("Ground")) Primitive("Ground", PrimitiveType.Plane, Vector3.zero, new Color(.2f,.2f,.2f)); }
        private static void Primitive(string name, PrimitiveType type, Vector3 position, Color color) { GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color; go.GetComponent<Renderer>().material = material; }

        private void BuildUI()
        {
            if (GameObject.Find("CombatHUD")) return; EnsureEventSystem(); var canvasGo = new GameObject("CombatHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            playerText = Label(canvasGo.transform, "PlayerStatus", new Vector2(.02f,.82f), new Vector2(.38f,.98f), 24, TextAnchor.UpperLeft); enemyText = Label(canvasGo.transform, "EnemyStatus", new Vector2(.62f,.82f), new Vector2(.98f,.98f), 24, TextAnchor.UpperRight);
            intentText = Label(canvasGo.transform, "EnemyIntent", new Vector2(.35f,.72f), new Vector2(.65f,.82f), 34, TextAnchor.MiddleCenter); resultText = Label(canvasGo.transform, "Result", new Vector2(.3f,.42f), new Vector2(.7f,.53f), 26, TextAnchor.MiddleCenter); pressureText = Label(canvasGo.transform, "Pressure", new Vector2(.3f,.34f), new Vector2(.7f,.42f), 24, TextAnchor.MiddleCenter);
            timingVisualizer = new GameObject("TimingVisualizer", typeof(RectTransform), typeof(TimingVisualizer)).GetComponent<TimingVisualizer>(); timingVisualizer.transform.SetParent(canvasGo.transform, false); Rect(timingVisualizer.GetComponent<RectTransform>(), new Vector2(.35f,.53f), new Vector2(.65f,.72f)); timingVisualizer.Initialize(presentation);
            encounterPanel = Panel(canvasGo.transform,"EncounterResolutionPanel",new Vector2(.27f,.18f),new Vector2(.73f,.34f)); ButtonRow(encounterPanel.transform, new[]{("[Q] Manual",(Action)ChooseManualOrAttack),("[W] Auto",ChooseAutoOrDefense),("[E] Negotiate",ChooseNegotiationOrRecovery),("[R] Escape",ChooseEscape)});
            manualPanel = Panel(canvasGo.transform,"ManualCombatPanel",new Vector2(.3f,.18f),new Vector2(.7f,.34f)); ButtonRow(manualPanel.transform, new[]{("[Q] Attack",(Action)ChooseManualOrAttack),("[W] Defense",ChooseAutoOrDefense),("[E] Recovery",ChooseNegotiationOrRecovery)});
            queueText = Label(canvasGo.transform,"Queue",new Vector2(.02f,.02f),new Vector2(.49f,.18f),18,TextAnchor.UpperLeft); debugPanel = Panel(canvasGo.transform,"DebugHUD",new Vector2(.51f,.02f),new Vector2(.98f,.32f)); debugText = Label(debugPanel.transform,"DebugText",Vector2.zero,Vector2.one,16,TextAnchor.UpperLeft); Button(debugPanel.transform,"Switch Timing View",CycleTimingView,new Vector2(.68f,.02f),new Vector2(.98f,.15f)); SetPanels(false);
        }
        private static void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>()) return; var go = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
        private static GameObject Panel(Transform parent,string name,Vector2 min,Vector2 max) { var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false); Rect(go.GetComponent<RectTransform>(),min,max); go.GetComponent<Image>().color=new Color(0,0,0,.55f); return go; }
        private static Text Label(Transform parent,string name,Vector2 min,Vector2 max,int size,TextAnchor anchor) { var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false); Rect(go.GetComponent<RectTransform>(),min,max); var t=go.GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize=size;t.alignment=anchor;t.color=Color.white;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow; return t; }
        private static void ButtonRow(Transform parent,(string,Action)[] buttons) { for(int i=0;i<buttons.Length;i++){float w=1f/buttons.Length;Button(parent,buttons[i].Item1,buttons[i].Item2,new Vector2(i*w,.1f),new Vector2((i+1)*w,.9f));} }
        private static void Button(Transform parent,string label,Action action,Vector2 min,Vector2 max) { var go=Panel(parent,label,min,max);var b=go.AddComponent<Button>();b.onClick.AddListener(()=>action());var t=Label(go.transform,"Label",Vector2.zero,Vector2.one,18,TextAnchor.MiddleCenter);t.text=label; }
        private static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
    }
}
