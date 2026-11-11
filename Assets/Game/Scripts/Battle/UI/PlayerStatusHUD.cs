using MonsterAction.Battle.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Battle.UI
{
    /// <summary>
    /// Battle の HUD（HP / スタミナゲージ、集中モードの照準、ロックオンマーカー）。
    /// Canvas（BattleHUD プレハブ）のルートに付いていて、インスペクターで各 UI を参照しています。
    ///
    /// 【UI 担当の人へ】
    /// ・見た目は Canvas の中の Image を自由に変えて OK（色・スプライト・位置・大きさ）
    /// ・ゲージは Image の Image Type = Filled、Fill Method = Horizontal にして fillAmount（0〜1）で長さを表しています
    /// ・数値の受け取りはプレイヤーのイベントを使います
    ///     HealthChanged(current, max) / StaminaChanged(current, max) / IsExhausted / IsFocusing / LockTarget
    /// </summary>
    public class PlayerStatusHUD : MonoBehaviour
    {
        [Tooltip("未設定なら PlayerBattleController.Active（シーンのプレイヤー）を使う")]
        [SerializeField] private PlayerBattleController player;

        [Header("ゲージ（Image Type = Filled）")]
        [SerializeField] private Image hpFill;
        [SerializeField] private Image staminaFill;
        [SerializeField] private Color staminaColor = new Color(1f, 0.82f, 0.2f);
        [Tooltip("スタミナ切れ（疲労）中の色")]
        [SerializeField] private Color exhaustedColor = new Color(0.85f, 0.35f, 0.25f);
        [Tooltip("ゲージがなめらかに追いつく速さ")]
        [SerializeField] private float smoothSpeed = 12f;

        [Header("照準 / ロックオン")]
        [Tooltip("集中モード中だけ表示する照準")]
        [SerializeField] private GameObject reticle;
        [Tooltip("ロックオン中、ターゲットの位置に表示するマーカー")]
        [SerializeField] private RectTransform lockOnMarker;

        private Canvas canvas;
        private Camera mainCamera;
        private float hpRate = 1f, staminaRate = 1f;

        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (player == null) player = PlayerBattleController.Active;
            if (player == null)
            {
                Debug.LogWarning("[PlayerStatusHUD] プレイヤーが見つかりません", this);
                enabled = false;
                return;
            }

            player.HealthChanged += OnHealthChanged;
            player.StaminaChanged += OnStaminaChanged;
            OnHealthChanged(player.CurrentHp, player.MaxHp);
            OnStaminaChanged(player.CurrentStamina, player.MaxStamina);
            if (hpFill != null) hpFill.fillAmount = hpRate;
            if (staminaFill != null) staminaFill.fillAmount = staminaRate;
        }

        private void OnDestroy()
        {
            if (player == null) return;
            player.HealthChanged -= OnHealthChanged;
            player.StaminaChanged -= OnStaminaChanged;
        }

        private void OnHealthChanged(float current, float max) => hpRate = max > 0f ? current / max : 0f;
        private void OnStaminaChanged(float current, float max) => staminaRate = max > 0f ? current / max : 0f;

        private void Update()
        {
            // ゲージ（なめらかに追いつく）
            float k = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            if (hpFill != null) hpFill.fillAmount = Mathf.Lerp(hpFill.fillAmount, hpRate, k);
            if (staminaFill != null)
            {
                staminaFill.fillAmount = Mathf.Lerp(staminaFill.fillAmount, staminaRate, k);
                staminaFill.color = player.IsExhausted ? exhaustedColor : staminaColor;
            }

            // 照準
            if (reticle != null && reticle.activeSelf != player.IsFocusing) reticle.SetActive(player.IsFocusing);

            // ロックオンマーカー
            if (lockOnMarker != null) UpdateLockOnMarker();
        }

        private void UpdateLockOnMarker()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            var target = player.LockTarget;
            bool show = target != null && mainCamera != null;

            if (show)
            {
                Vector3 screen = mainCamera.WorldToScreenPoint(target.AimPosition);
                show = screen.z > 0f; // カメラの後ろにいるときは出さない
                var parent = (RectTransform)lockOnMarker.parent;
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
                    lockOnMarker.localPosition = local;
            }

            if (lockOnMarker.gameObject.activeSelf != show) lockOnMarker.gameObject.SetActive(show);
        }
    }
}
