using System;
using MonsterAction.Battle.Player;
using MonsterAction.Home.Interaction;
using UnityEngine;

namespace MonsterAction.Home.Player
{
    /// <summary>
    /// Home（村）を歩くだけのプレイヤー。戦闘はしません。
    ///   ・左スティック / WASD で移動、R1 / Shift で走る（スタミナなし）
    ///   ・近くの Interactable（ドア・村人など）をハイライトし、□ / 左クリック / E で調べる
    ///   ・メニューを開いている間は ControlEnabled = false（動けない）
    /// カメラは Battle と同じ BattleCameraController を使います。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class HomePlayerController : MonoBehaviour
    {
        private static readonly int AnimParamSpeed = Animator.StringToHash("Speed");

        /// <summary>シーンにいる Home のプレイヤー</summary>
        public static HomePlayerController Active { get; private set; }

        [SerializeField] private Animator animator;
        [Tooltip("未設定ならメインカメラ")]
        [SerializeField] private Transform cameraTransform;

        [Header("移動")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private float gravity = -20f;

        [Header("調べる")]
        [Tooltip("この角度より後ろにある物は選ばない（正面 = 0°）")]
        [SerializeField] private float interactAngle = 100f;

        private CharacterController controller;
        private PlayerInputReader input;
        private float verticalVelocity;
        private bool controlEnabled = true;
        private float controlEnabledTime = float.NegativeInfinity;

        /// <summary>今ハイライトしている（□で調べられる）もの</summary>
        public Interactable Focused { get; private set; }

        /// <summary>ハイライト対象が変わった（null = 何も無い）</summary>
        public event Action<Interactable> FocusChanged;

        /// <summary>false の間は動けず、調べることもできない（メニュー表示中）</summary>
        public bool ControlEnabled
        {
            get => controlEnabled;
            set
            {
                controlEnabled = value;
                controlEnabledTime = Time.time;
                input.InputEnabled = value;
                input.ClearBuffer();
                if (!value) SetFocused(null);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this) Active = null;
            SetFocused(null);
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            // Home に来たら自動セーブ（クエストの報酬などを保存。タイトルを通していなければ何もしない）
            MonsterAction.Data.SaveSystem.AutoSave();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 move = controlEnabled ? GetMoveDirection() : Vector3.zero;
            bool running = controlEnabled && input.SprintHeld && move.sqrMagnitude > 0.01f;

            // 移動
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            Vector3 velocity = move * (running ? runSpeed : walkSpeed);
            velocity.y = verticalVelocity;
            controller.Move(velocity * dt);

            // 向き
            if (move.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
            }

            // アニメーション（Battle と同じ Locomotion：0 待機 / 1 走り / 1.6 ダッシュ）
            if (animator != null)
            {
                float blend = move.magnitude * (running ? PlayerBattleController.SprintAnimBlendValue : 1f);
                animator.SetFloat(AnimParamSpeed, blend, 0.15f, dt);
            }

            if (!controlEnabled) return;

            // 調べる
            SetFocused(Interactable.FindBest(transform.position, transform.forward, interactAngle));
            // メニューを閉じた直後の決定ボタンで、すぐ開き直さないように少し待つ
            if (Focused != null && input.InteractPressedThisFrame && Time.time - controlEnabledTime > 0.25f) Focused.Interact(this);
        }

        private Vector3 GetMoveDirection()
        {
            Vector2 stick = input.Move;
            if (stick.sqrMagnitude < 0.01f) return Vector3.zero;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(forward * stick.y + right * stick.x, 1f);
        }

        private void SetFocused(Interactable next)
        {
            if (next == Focused) return;
            if (Focused != null) Focused.SetHighlighted(false);
            Focused = next;
            if (Focused != null) Focused.SetHighlighted(true);
            FocusChanged?.Invoke(Focused);
        }
    }
}
