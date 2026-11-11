using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// Battle シーン用の入力読み取り。
    /// Input System の Action（Assets/InputSystem_Actions.inputactions の "Player" マップ）を読み、
    /// プレイヤーやカメラが使いやすい形で公開します。
    ///
    /// ・ボタン入力は「先行入力バッファ」付き。押してから bufferTime 秒以内なら Consume〇〇() で拾えます。
    ///   （モンハンのように、攻撃モーション中に次の攻撃ボタンを押しておくとコンボがつながる仕組みに使う）
    /// ・InputEnabled = false にすると全入力がゼロになる（メニュー表示中・ムービー中などに使う）
    ///
    /// 新しいボタン（スキル、アイテム使用など）を追加したい場合：
    ///   1) InputSystem_Actions.inputactions に Action を追加
    ///   2) ここに InputAction フィールドと Consume メソッドを追加
    /// </summary>
    [DefaultExecutionOrder(-100)] // プレイヤーより先に入力を更新する
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("使用する Input Action Asset。未設定ならプロジェクト共通の Input Actions を使います")]
        [SerializeField] private InputActionAsset actions;

        [SerializeField] private string actionMapName = "Player";

        [Header("Action 名")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string attackActionName = "Attack";
        [Tooltip("回避（ステップ）。ジャンプボタンに割り当てています")]
        [SerializeField] private string dodgeActionName = "Jump";
        [Tooltip("ダッシュ（押している間）。Shift / R1")]
        [SerializeField] private string sprintActionName = "Sprint";
        [Tooltip("ターゲットカメラ（ロックオン）切り替え。〇ボタン / Q / マウス中ボタン")]
        [SerializeField] private string lockOnActionName = "LockOn";
        [Tooltip("集中モード（押している間）。L1 / マウス右ボタン")]
        [SerializeField] private string focusActionName = "Focus";
        [Tooltip("アイテム使用。△ボタン / R")]
        [SerializeField] private string useItemActionName = "UseItem";
        [Tooltip("Home で調べる / 話しかける（キーボードの E）。□ボタンは Attack を使う")]
        [SerializeField] private string interactActionName = "Interact";
        private InputAction interactAction;

        /// <summary>
        /// Home 用：調べる / 話しかけるボタンをこのフレームで押したか（□ / 左クリック / E）
        /// </summary>
        public bool InteractPressedThisFrame =>
            InputEnabled && ((attackAction != null && attackAction.WasPressedThisFrame()) ||
                             (interactAction != null && interactAction.WasPressedThisFrame()));

        [Header("先行入力")]
        [Tooltip("ボタンを押してから何秒間その入力を覚えておくか")]
        [SerializeField] private float bufferTime = 0.3f;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;
        private InputAction dodgeAction;
        private InputAction sprintAction;
        private InputAction lockOnAction;
        private InputAction focusAction;
        private InputAction useItemAction;

        private float lastAttackPressedTime = float.NegativeInfinity;
        private float lastDodgePressedTime = float.NegativeInfinity;

        /// <summary>false の間は全入力を無視する</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>移動入力（-1〜1）</summary>
        public Vector2 Move => InputEnabled && moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>視点入力。マウスならピクセル移動量、スティックなら -1〜1</summary>
        public Vector2 Look => InputEnabled && lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>視点入力がマウスなどのポインタから来ているか（感度の扱いが変わるため）</summary>
        public bool LookIsPointer => lookAction?.activeControl?.device is Pointer;

        /// <summary>攻撃ボタンを押し続けているか（溜め攻撃などを作るとき用）</summary>
        public bool AttackHeld => InputEnabled && attackAction != null && attackAction.IsPressed();

        /// <summary>ダッシュボタンを押し続けているか</summary>
        public bool SprintHeld => InputEnabled && sprintAction != null && sprintAction.IsPressed();

        /// <summary>集中モードボタンを押し続けているか</summary>
        public bool FocusHeld => InputEnabled && focusAction != null && focusAction.IsPressed();

        /// <summary>攻撃ボタンをこのフレームで離したか（溜め攻撃の発射タイミングなどに使う）</summary>
        public bool AttackReleasedThisFrame => InputEnabled && attackAction != null && attackAction.WasReleasedThisFrame();

        /// <summary>ロックオンボタンをこのフレームで押したか</summary>
        public bool LockOnPressedThisFrame => InputEnabled && lockOnAction != null && lockOnAction.WasPressedThisFrame();

        private void OnEnable()
        {
            var asset = actions != null ? actions : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("[PlayerInputReader] Input Action Asset が見つかりません。Project Settings > Input System Package で Project-wide Actions を設定してください。", this);
                return;
            }

            var map = asset.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, true);
            lookAction = map.FindAction(lookActionName, true);
            attackAction = map.FindAction(attackActionName, true);
            dodgeAction = map.FindAction(dodgeActionName, true);
            sprintAction = map.FindAction(sprintActionName, true);
            lockOnAction = map.FindAction(lockOnActionName, true);
            focusAction = map.FindAction(focusActionName, true);
            useItemAction = map.FindAction(useItemActionName, true);
            useItemAction.performed += OnUseItemPerformed;
            interactAction = map.FindAction(interactActionName, false); // 無くてもよい

            attackAction.performed += OnAttackPerformed;
            dodgeAction.performed += OnDodgePerformed;
            map.Enable();
        }

        private void OnDisable()
        {
            if (attackAction != null) attackAction.performed -= OnAttackPerformed;
            if (dodgeAction != null) dodgeAction.performed -= OnDodgePerformed;
            if (useItemAction != null) useItemAction.performed -= OnUseItemPerformed;
        }

        private float lastUseItemPressedTime = float.NegativeInfinity;

        private void OnUseItemPerformed(InputAction.CallbackContext _)
        {
            if (InputEnabled) lastUseItemPressedTime = Time.time;
        }

        /// <summary>バッファ内にアイテム使用の入力があれば true を返し、その入力を消費する</summary>
        public bool ConsumeUseItem() => Consume(ref lastUseItemPressedTime);

        private void OnAttackPerformed(InputAction.CallbackContext _)
        {
            if (InputEnabled) lastAttackPressedTime = Time.time;
        }

        private void OnDodgePerformed(InputAction.CallbackContext _)
        {
            if (InputEnabled) lastDodgePressedTime = Time.time;
        }

        /// <summary>バッファ内に攻撃入力があれば true を返し、その入力を消費する</summary>
        public bool ConsumeAttack() => Consume(ref lastAttackPressedTime);

        /// <summary>バッファ内に回避入力があれば true を返し、その入力を消費する</summary>
        public bool ConsumeDodge() => Consume(ref lastDodgePressedTime);

        /// <summary>溜まっている入力を全て捨てる（被弾時など）</summary>
        public void ClearBuffer()
        {
            lastAttackPressedTime = float.NegativeInfinity;
            lastDodgePressedTime = float.NegativeInfinity;
            lastUseItemPressedTime = float.NegativeInfinity;
        }

        private bool Consume(ref float pressedTime)
        {
            if (Time.time - pressedTime > bufferTime) return false;
            pressedTime = float.NegativeInfinity;
            return true;
        }
    }
}
