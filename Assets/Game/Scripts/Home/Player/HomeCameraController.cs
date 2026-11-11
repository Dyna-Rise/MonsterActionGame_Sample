using UnityEngine;

namespace MonsterAction.Home.Player
{
    /// <summary>
    /// Home 用の見下ろしカメラ。角度は固定で、プレイヤーを追いかけるだけです（回転しない）。
    /// Main Camera に付けます。角度・距離はインスペクターで調整してください（Play 中に変えるとすぐ反映）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HomeCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("角度（固定）")]
        [Tooltip("見下ろす角度（90 = 真上から）")]
        [SerializeField, Range(10f, 90f)] private float pitch = 50f;
        [Tooltip("カメラの向き（Y 軸の角度）。画面の上がこの方角になる")]
        [SerializeField] private float yaw = 0f;
        [Tooltip("注視点からカメラまでの距離")]
        [SerializeField] private float distance = 10f;
        [Tooltip("プレイヤーの足元からどれだけ上を見るか")]
        [SerializeField] private float lookHeight = 1f;
        [SerializeField] private float fieldOfView = 45f;

        [Header("追従")]
        [SerializeField] private float followSmoothTime = 0.15f;

        private Vector3 focus;
        private Vector3 focusVelocity;

        public Transform Target => target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null) focus = target.position + Vector3.up * lookHeight;
        }

        private void Start()
        {
            if (target == null && HomePlayerController.Active != null) target = HomePlayerController.Active.transform;
            SetTarget(target);
            GetComponent<Camera>().fieldOfView = fieldOfView;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Apply();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            focus = Vector3.SmoothDamp(focus, target.position + Vector3.up * lookHeight, ref focusVelocity, followSmoothTime);
            Apply();
        }

        private void Apply()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }

        private void OnValidate()
        {
            var cam = GetComponent<Camera>();
            if (cam != null && Application.isPlaying) cam.fieldOfView = fieldOfView;
        }
    }
}
