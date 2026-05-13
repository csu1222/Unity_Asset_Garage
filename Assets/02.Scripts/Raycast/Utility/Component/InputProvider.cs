using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SystemicOverload.Phase1
{
    [DefaultExecutionOrder(-100)]
    public class InputProvider : MonoBehaviour
    {
        public enum ControlDeviceKind
        {
            None,
            KeyboardMouse,
            Gamepad,
            Other
        }

        public enum LookDeviceKind
        {
            None,
            Pointer,
            Gamepad,
            Other
        }

        private const string DefaultActionAssetResourcesPath = "";
        private const string DefaultActionAssetEditorPath = "Assets/00.Preset/InputSystem_TPS.inputactions";
        private const string GameplayMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string PointerPositionActionName = "PointerPosition";
        private const string ZoomActionName = "Zoom";
        private const string PrimaryHoldActionName = "PrimaryHold";
        private const string SecondaryHoldActionName = "SecondaryHold";
        private const string AttackActionName = "Fire";
        private const string InteractActionName = "Interact";
        private const string MeleeActionName = "Melee";
        private const string MagicActionName = "Magic";
        private const string AoeActionName = "Aoe";
        private const string DashActionName = "Dash";

        [Header("Action Asset")]
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private string resourcesFallbackPath = DefaultActionAssetResourcesPath;

        [Header("Movement")]
        [SerializeField] private bool normalizeDiagonalInput = true;
        [SerializeField] private bool enableDualMouseForwardMove = true;
        [SerializeField] private float dualMouseForwardAmount = 1.0f;

        [Header("Gamepad")]
        [SerializeField] private float gamepadLookDeadzone = 0.15f;

        private InputActionAsset runtimeInputActions;
        private InputActionMap gameplayMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction pointerPositionAction;
        private InputAction zoomAction;
        private InputAction primaryHoldAction;
        private InputAction secondaryHoldAction;
        private InputAction attackAction;
        private InputAction interactAction;
        private InputAction meleeAction;
        private InputAction magicAction;
        private InputAction aoeAction;
        private InputAction dashAction;
        private bool callbacksBound;
        private bool initializationFailed;

        public Vector2 RawMoveInput { get; private set; }
        public Vector2 MoveInput { get; private set; }
        public Vector2 PointerScreenPosition { get; private set; }
        public Vector2 LookInput { get; private set; }
        public float ZoomDelta { get; private set; }
        public bool IsPrimaryHeld { get; private set; }
        public bool IsSecondaryHeld { get; private set; }
        public bool IsLeftMouseHeld => IsPrimaryHeld;
        public bool IsRightMouseHeld => IsSecondaryHeld;
        public bool IsDualMouseForwardHeld => IsDualInputForwardHeld;
        public bool IsDualInputForwardHeld => IsPrimaryHeld && IsSecondaryHeld;
        public ControlDeviceKind LastUsedDeviceKind { get; private set; }
        public LookDeviceKind CurrentLookDeviceKind { get; private set; }
        public Vector2 PointerLookDelta => CurrentLookDeviceKind == LookDeviceKind.Pointer ? LookInput : Vector2.zero;
        public Vector2 GamepadLookInput => CurrentLookDeviceKind == LookDeviceKind.Gamepad ? LookInput : Vector2.zero;
        public bool HasGamepadLookInput => GamepadLookInput.sqrMagnitude > gamepadLookDeadzone * gamepadLookDeadzone;
        public bool IsUsingGamepad => LastUsedDeviceKind == ControlDeviceKind.Gamepad;
        public bool ShouldAlignCharacterToCamera => IsSecondaryHeld || HasGamepadLookInput;
        public bool ShouldBlockPointerFacing => IsPrimaryHeld && !HasGamepadLookInput;
        public bool IsCameraLookHeld => IsPrimaryHeld || IsSecondaryHeld;

        /// <summary>
        /// 이번 프레임에 공격 입력이 눌렸는지 여부입니다. Phase 1 전용 씬에서 Attack 액션이 없으면 항상 false입니다.
        /// </summary>
        public bool WasAttackPressedThisFrame { get; private set; }

        /// <summary>
        /// 이번 프레임에 상호작용 입력(E)이 눌렸는지 여부입니다.
        /// </summary>
        public bool WasInteractPressedThisFrame { get; private set; }

        /// <summary>
        /// 이번 프레임에 근접 공격 입력(F)이 눌렸는지 여부입니다.
        /// </summary>
        public bool WasMeleePressedThisFrame { get; private set; }

        /// <summary>
        /// 이번 프레임에 마법 입력(Q)이 눌렸는지 여부입니다.
        /// </summary>
        public bool WasMagicPressedThisFrame { get; private set; }

        /// <summary>
        /// 이번 프레임에 광역 스킬 입력(R)이 눌렸는지 여부입니다.
        /// </summary>
        public bool WasAoePressedThisFrame { get; private set; }

        /// <summary>
        /// 이번 프레임에 대시 입력(LeftShift)이 눌렸는지 여부입니다.
        /// </summary>
        public bool WasDashPressedThisFrame { get; private set; }

        private void Reset()
        {
            TryAssignDefaultAssetInEditor();
        }

        private void OnValidate()
        {
            dualMouseForwardAmount = Mathf.Max(0.0f, dualMouseForwardAmount);
            gamepadLookDeadzone = Mathf.Clamp01(gamepadLookDeadzone);
            TryAssignDefaultAssetInEditor();
        }

        private void OnEnable()
        {
            if (!EnsureInputActionsInitialized())
            {
                return;
            }

            BindCallbacks();
            gameplayMap.Enable();
            SampleActions();
        }

        private void Update()
        {
            if (!EnsureInputActionsInitialized())
            {
                return;
            }

            SampleActions();
        }

        private void OnDisable()
        {
            if (gameplayMap != null)
            {
                gameplayMap.Disable();
            }

            UnbindCallbacks();
            ClearRuntimeState();
        }

        private void OnDestroy()
        {
            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
                runtimeInputActions = null;
            }
        }

        /// <summary>
        /// 외부에서 사용할 InputActionAsset을 명시적으로 설정합니다.
        /// </summary>
        public void SetInputActionsAsset(InputActionAsset targetAsset)
        {
            inputActionsAsset = targetAsset;
            initializationFailed = false;
        }

        private bool EnsureInputActionsInitialized()
        {
            if (runtimeInputActions != null)
            {
                return true;
            }

            if (initializationFailed)
            {
                return false;
            }

            InputActionAsset sourceAsset = ResolveSourceAsset();
            if (sourceAsset == null)
            {
                initializationFailed = true;
                Debug.LogError("InputProvider could not resolve a Phase 1 InputActionAsset.", this);
                return false;
            }

            runtimeInputActions = Instantiate(sourceAsset);
            gameplayMap = runtimeInputActions.FindActionMap(GameplayMapName, true);
            moveAction = gameplayMap.FindAction(MoveActionName, true);
            lookAction = gameplayMap.FindAction(LookActionName, true);
            pointerPositionAction = gameplayMap.FindAction(PointerPositionActionName, false);
            zoomAction = gameplayMap.FindAction(ZoomActionName, false);
            primaryHoldAction = gameplayMap.FindAction(PrimaryHoldActionName, false);
            secondaryHoldAction = gameplayMap.FindAction(SecondaryHoldActionName, false);
            attackAction = gameplayMap.FindAction(AttackActionName, false);
            interactAction = gameplayMap.FindAction(InteractActionName, false);
            meleeAction = gameplayMap.FindAction(MeleeActionName, false);
            magicAction = gameplayMap.FindAction(MagicActionName, false);
            aoeAction = gameplayMap.FindAction(AoeActionName, false);
            dashAction = gameplayMap.FindAction(DashActionName, false);
            if (attackAction == null)
            {
                Debug.LogWarning(
                    $"InputProvider: '{AttackActionName}' 액션을 찾을 수 없습니다. MainInputActionData.inputactions를 확인하세요.",
                    this);
            }
            if (interactAction == null)
            {
                Debug.LogWarning($"InputProvider: '{InteractActionName}' 액션을 찾을 수 없습니다.", this);
            }
            if (meleeAction == null)
            {
                Debug.LogWarning($"InputProvider: '{MeleeActionName}' 액션을 찾을 수 없습니다.", this);
            }
            if (magicAction == null)
            {
                Debug.LogWarning($"InputProvider: '{MagicActionName}' 액션을 찾을 수 없습니다.", this);
            }
            if (aoeAction == null)
            {
                Debug.LogWarning($"InputProvider: '{AoeActionName}' 액션을 찾을 수 없습니다.", this);
            }
            if (dashAction == null)
            {
                Debug.LogWarning($"InputProvider: '{DashActionName}' 액션을 찾을 수 없습니다.", this);
            }

            return true;
        }

        private void BindCallbacks()
        {
            if (callbacksBound)
            {
                return;
            }

            moveAction.performed += OnGameplayActionPerformed;
            lookAction.performed += OnGameplayActionPerformed;
            if (pointerPositionAction != null)
            {
                pointerPositionAction.performed += OnGameplayActionPerformed;
            }
            if (zoomAction != null)
            {
                zoomAction.performed += OnGameplayActionPerformed;
            }
            if (primaryHoldAction != null)
            {
                primaryHoldAction.performed += OnGameplayActionPerformed;
            }
            if (secondaryHoldAction != null)
            {
                secondaryHoldAction.performed += OnGameplayActionPerformed;
            }
            if (attackAction != null)
            {
                attackAction.performed += OnGameplayActionPerformed;
            }
            if (interactAction != null)
            {
                interactAction.performed += OnGameplayActionPerformed;
            }
            if (meleeAction != null)
            {
                meleeAction.performed += OnGameplayActionPerformed;
            }
            if (magicAction != null)
            {
                magicAction.performed += OnGameplayActionPerformed;
            }
            if (aoeAction != null)
            {
                aoeAction.performed += OnGameplayActionPerformed;
            }
            if (dashAction != null)
            {
                dashAction.performed += OnGameplayActionPerformed;
            }

            callbacksBound = true;
        }

        private void UnbindCallbacks()
        {
            if (!callbacksBound || moveAction == null)
            {
                callbacksBound = false;
                return;
            }

            moveAction.performed -= OnGameplayActionPerformed;
            lookAction.performed -= OnGameplayActionPerformed;
            if (pointerPositionAction != null)
            {
                pointerPositionAction.performed -= OnGameplayActionPerformed;
            }
            if (zoomAction != null)
            {
                zoomAction.performed -= OnGameplayActionPerformed;
            }
            if (primaryHoldAction != null)
            {
                primaryHoldAction.performed -= OnGameplayActionPerformed;
            }
            if (secondaryHoldAction != null)
            {
                secondaryHoldAction.performed -= OnGameplayActionPerformed;
            }
            if (attackAction != null)
            {
                attackAction.performed -= OnGameplayActionPerformed;
            }
            if (interactAction != null)
            {
                interactAction.performed -= OnGameplayActionPerformed;
            }
            if (meleeAction != null)
            {
                meleeAction.performed -= OnGameplayActionPerformed;
            }
            if (magicAction != null)
            {
                magicAction.performed -= OnGameplayActionPerformed;
            }
            if (aoeAction != null)
            {
                aoeAction.performed -= OnGameplayActionPerformed;
            }
            if (dashAction != null)
            {
                dashAction.performed -= OnGameplayActionPerformed;
            }

            callbacksBound = false;
        }

        private void OnGameplayActionPerformed(InputAction.CallbackContext context)
        {
            LastUsedDeviceKind = ClassifyControlDevice(context.control.device);
        }

        private void SampleActions()
        {
            // MainInputActionData에 Hold 액션이 없을 수 있으므로 New Input System 디바이스 입력으로 보완합니다.
            IsPrimaryHeld = primaryHoldAction != null
                ? primaryHoldAction.IsPressed()
                : (Mouse.current != null && Mouse.current.leftButton.isPressed);
            IsSecondaryHeld = secondaryHoldAction != null
                ? secondaryHoldAction.IsPressed()
                : (Mouse.current != null && Mouse.current.rightButton.isPressed);

            RawMoveInput = moveAction.ReadValue<Vector2>();
            MoveInput = ApplyDualMouseForward(PrepareMoveInput(RawMoveInput));

            Vector2 pointerPosition = pointerPositionAction != null
                ? pointerPositionAction.ReadValue<Vector2>()
                : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
            PointerScreenPosition = pointerPosition.sqrMagnitude > 0.0f
                ? pointerPosition
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            LookInput = lookAction.ReadValue<Vector2>();
            CurrentLookDeviceKind = ClassifyLookDevice(lookAction.activeControl?.device);
            ZoomDelta = zoomAction != null
                ? zoomAction.ReadValue<float>()
                : (Mouse.current != null ? Mouse.current.scroll.ReadValue().y * 0.01f : 0.0f);
            WasAttackPressedThisFrame = attackAction != null && attackAction.WasPressedThisFrame();
            WasInteractPressedThisFrame = (interactAction != null && interactAction.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
            WasMeleePressedThisFrame = (meleeAction != null && meleeAction.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
            WasMagicPressedThisFrame = (magicAction != null && magicAction.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);
            WasAoePressedThisFrame = (aoeAction != null && aoeAction.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
            WasDashPressedThisFrame = (dashAction != null && dashAction.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame);
        }

        private Vector2 PrepareMoveInput(Vector2 sourceMoveInput)
        {
            if (normalizeDiagonalInput && sourceMoveInput.sqrMagnitude > 1.0f)
            {
                sourceMoveInput.Normalize();
            }

            return sourceMoveInput;
        }

        private Vector2 ApplyDualMouseForward(Vector2 sourceMoveInput)
        {
            if (!enableDualMouseForwardMove || !IsDualInputForwardHeld)
            {
                return sourceMoveInput;
            }

            Vector2 composedInput = sourceMoveInput + Vector2.up * dualMouseForwardAmount;
            if (normalizeDiagonalInput && composedInput.sqrMagnitude > 1.0f)
            {
                composedInput.Normalize();
            }

            return composedInput;
        }

        private void ClearRuntimeState()
        {
            RawMoveInput = Vector2.zero;
            MoveInput = Vector2.zero;
            PointerScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            LookInput = Vector2.zero;
            ZoomDelta = 0.0f;
            IsPrimaryHeld = false;
            IsSecondaryHeld = false;
            LastUsedDeviceKind = ControlDeviceKind.None;
            CurrentLookDeviceKind = LookDeviceKind.None;
            WasAttackPressedThisFrame = false;
            WasInteractPressedThisFrame = false;
            WasMeleePressedThisFrame = false;
            WasMagicPressedThisFrame = false;
            WasAoePressedThisFrame = false;
            WasDashPressedThisFrame = false;
        }

        private InputActionAsset ResolveSourceAsset()
        {
            if (inputActionsAsset != null)
            {
                return inputActionsAsset;
            }

            if (!string.IsNullOrWhiteSpace(resourcesFallbackPath))
            {
                inputActionsAsset = Resources.Load<InputActionAsset>(resourcesFallbackPath);
            }

            return inputActionsAsset;
        }

        private static LookDeviceKind ClassifyLookDevice(InputDevice device)
        {
            if (device == null)
            {
                return LookDeviceKind.None;
            }

            if (device is Mouse || device is Pen || device is Touchscreen)
            {
                return LookDeviceKind.Pointer;
            }

            if (device is Gamepad)
            {
                return LookDeviceKind.Gamepad;
            }

            return LookDeviceKind.Other;
        }

        private static ControlDeviceKind ClassifyControlDevice(InputDevice device)
        {
            if (device == null)
            {
                return ControlDeviceKind.None;
            }

            if (device is Gamepad)
            {
                return ControlDeviceKind.Gamepad;
            }

            if (device is Keyboard || device is Mouse || device is Pen || device is Touchscreen)
            {
                return ControlDeviceKind.KeyboardMouse;
            }

            return ControlDeviceKind.Other;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TryAssignDefaultAssetInEditor()
        {
            if (inputActionsAsset != null)
            {
                return;
            }

#if UNITY_EDITOR
            inputActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultActionAssetEditorPath);
#endif
        }
    }
}