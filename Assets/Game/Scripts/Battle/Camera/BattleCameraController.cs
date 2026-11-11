using MonsterAction.Battle.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterAction.Battle.CameraSystem
{
    /// <summary>
    /// Battle 用カメラ。Main Camera に付けます。3つのモードを自動で切り替えます。
    ///
    ///  ・通常        … 背面追従。マウス / 右スティックで自由に回せる
    ///  ・ターゲット  … ロックオン中（〇ボタン）。ターゲットが画面に入るよう自動で向きを合わせる
    ///  ・集中モード  … L1 を押している間。肩越しに寄って画角を狭め、照準で狙いやすくする（ロックオンより優先）
    ///
    /// ロックオン相手がいないときに〇を押すと、カメラがキャラの背後に戻ります。
    /// 壁や地形にめり込まないよう SphereCast で距離を縮めます。
    /// プレイ開始時にマウスカーソルをロック。Esc で解除、画面クリックで再ロック。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattleCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("通常")]
        [SerializeField] private float pivotHeight = 1.6f;
        [SerializeField] private float distance = 5f;
        [SerializeField] private float followSmoothTime = 0.05f;

        [Header("集中モード（肩越し）")]
        [SerializeField] private float focusDistance = 2.3f;
        [SerializeField] private float focusPivotHeight = 1.55f;
        [Tooltip("右肩側へずらす量（m）")]
        [SerializeField] private float focusShoulderOffset = 0.6f;
        [SerializeField] private float focusFieldOfView = 45f;
        [Tooltip("集中モード中の視点感度の倍率")]
        [SerializeField] private float focusSensitivityScale = 0.6f;
        [Tooltip("通常 ⇔ 集中 の切り替えの速さ")]
        [SerializeField] private float focusBlendSpeed = 10f;

        [Header("ターゲットカメラ")]
        [Tooltip("ターゲットへ向きを合わせる速さ")]
        [SerializeField] private float lockOnTurnSpeed = 6f;
        [Tooltip("ターゲットを見下ろす角度の追加分")]
        [SerializeField] private float lockOnPitchOffset = 8f;
        [Tooltip("背後へ戻すときの速さ")]
        [SerializeField] private float resetTurnSpeed = 10f;

        [Header("回転")]
        [Tooltip("マウス：1ピクセルあたりの回転角度")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [Tooltip("スティック：1秒あたりの回転角度")]
        [SerializeField] private float stickSpeed = 180f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;

        [Header("めり込み防止")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float minDistance = 0.8f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Header("カーソル")]
        [SerializeField] private bool lockCursor = true;

        private Camera cam;
        private PlayerBattleController player;
        private PlayerInputReader input;
        private float normalFieldOfView;
        private float yaw;
        private float pitch = 15f;
        private float focusBlend;      // 0 = 通常, 1 = 集中
        private bool resetting;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        public Transform Target => target;

        /// <summary>追従対象を設定する</summary>
        public void SetTarget(Transform newTarget)
        {
            if (player != null) player.CameraResetRequested -= ResetBehindTarget;

            target = newTarget;
            player = target != null ? target.GetComponentInParent<PlayerBattleController>() : null;
            input = target != null ? target.GetComponentInParent<PlayerInputReader>() : null;
            if (player != null) player.CameraResetRequested += ResetBehindTarget;

            if (target != null)
            {
                yaw = target.eulerAngles.y;
                pivot = target.position + Vector3.up * pivotHeight;
            }
        }

        /// <summary>カメラをターゲットの背後へ（なめらかに）戻す</summary>
        public void ResetBehindTarget() => resetting = true;

        private float shakeStrength;
        private float shakeDuration;
        private float shakeTime;

        /// <summary>カメラを揺らす（strength：揺れ幅 m、duration：秒）</summary>
        public void Shake(float strength, float duration)
        {
            // 今の揺れより強ければ上書き
            float remaining = shakeDuration > 0f ? shakeStrength * (1f - shakeTime / shakeDuration) : 0f;
            if (strength < remaining) return;
            shakeStrength = strength;
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeTime = 0f;
        }

        private Vector3 GetShakeOffset(float dt)
        {
            if (shakeTime >= shakeDuration) return Vector3.zero;
            shakeTime += dt;
            float power = shakeStrength * (1f - shakeTime / shakeDuration); // だんだん弱まる
            return transform.right * Random.Range(-power, power) + transform.up * Random.Range(-power, power);
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            normalFieldOfView = cam.fieldOfView;
        }

        private void Start()
        {
            if (target == null && PlayerBattleController.Active != null) target = PlayerBattleController.Active.transform;
            if (target == null)
            {
                // Home など、PlayerBattleController がいないシーン用：入力を持つキャラを探す
                var reader = FindFirstObjectByType<PlayerInputReader>();
                if (reader != null) target = reader.transform;
            }
            SetTarget(target);
            if (lockCursor) SetCursorLocked(true);
        }

        private void OnDestroy()
        {
            if (player != null) player.CameraResetRequested -= ResetBehindTarget;
        }

        /// <summary>メニュー表示中か（カーソルを出し、視点操作を止める）</summary>
        public bool MenuMode { get; private set; }
        private int menuModeChangedFrame = -1;

        /// <summary>メニューを開いている間は true にする（Home のショップなど）</summary>
        public void SetMenuMode(bool on)
        {
            MenuMode = on;
            menuModeChangedFrame = Time.frameCount;
            if (lockCursor) SetCursorLocked(!on);
        }

        private void Update()
        {
            if (!lockCursor || MenuMode || Time.frameCount == menuModeChangedFrame) return; // メニューを閉じた Esc でカーソルを外さない
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            bool focusing = player != null && player.IsFocusing;
            bool lockedOn = player != null && player.LockTarget != null && !focusing;
            focusBlend = Mathf.MoveTowards(focusBlend, focusing ? 1f : 0f, focusBlendSpeed * dt);
            float blend = Mathf.SmoothStep(0f, 1f, focusBlend);

            // ---- 回転（手動） ----
            Vector2 look = ReadLook(dt) * Mathf.Lerp(1f, focusSensitivityScale, blend);
            if (look.sqrMagnitude > 0.0001f) resetting = false; // 手で動かしたら背後リセットは中止
            if (!lockedOn) yaw += look.x;
            pitch += invertY ? look.y : -look.y;

            // ---- 回転（自動） ----
            float k;
            if (lockedOn)
            {
                // ターゲットが画面に入るよう向きを合わせる
                Vector3 to = player.LockTarget.AimPosition - pivot;
                if (to.sqrMagnitude > 0.01f)
                {
                    float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg + lockOnPitchOffset;
                    k = 1f - Mathf.Exp(-lockOnTurnSpeed * dt);
                    yaw = Mathf.LerpAngle(yaw, wantYaw, k);
                    pitch = Mathf.LerpAngle(pitch, wantPitch, k);
                }
                resetting = false;
            }
            else if (resetting)
            {
                k = 1f - Mathf.Exp(-resetTurnSpeed * dt);
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, k);
                pitch = Mathf.LerpAngle(pitch, 15f, k);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, target.eulerAngles.y)) < 0.5f) resetting = false;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // ---- 位置 ----
            float height = Mathf.Lerp(pivotHeight, focusPivotHeight, blend);
            Vector3 targetPivot = target.position + Vector3.up * height;
            pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, followSmoothTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 localOffset = new Vector3(focusShoulderOffset * blend, 0f, -Mathf.Lerp(distance, focusDistance, blend));
            Vector3 desired = pivot + rotation * localOffset;

            Vector3 toDesired = desired - pivot;
            float maxDist = toDesired.magnitude;
            Vector3 dir = maxDist > 0.0001f ? toDesired / maxDist : -transform.forward;
            float dist = GetCollisionDistance(pivot, dir, maxDist);

            transform.SetPositionAndRotation(pivot + dir * dist, rotation);
            transform.position += GetShakeOffset(dt);
            cam.fieldOfView = Mathf.Lerp(normalFieldOfView, focusFieldOfView, blend);
        }

        private Vector2 ReadLook(float dt)
        {
            if (input == null || MenuMode) return Vector2.zero;
            Vector2 look = input.Look;
            if (input.LookIsPointer)
                return Cursor.lockState == CursorLockMode.Locked ? look * mouseSensitivity : Vector2.zero;
            return look * (stickSpeed * dt);
        }

        private float GetCollisionDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, hits, maxDist, collisionLayers, QueryTriggerInteraction.Ignore);
            float result = maxDist;
            Transform root = target.root;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(root)) continue; // プレイヤー自身は無視
                if (hits[i].distance > 0f && hits[i].distance < result) result = hits[i].distance;
            }
            return Mathf.Min(maxDist, Mathf.Max(minDistance, result));
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable() => SetCursorLocked(false);
    }
}
