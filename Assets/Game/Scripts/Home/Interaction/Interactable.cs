using System.Collections.Generic;
using MonsterAction.Home.Player;
using UnityEngine;
using UnityEngine.Events;

namespace MonsterAction.Home.Interaction
{
    /// <summary>
    /// □ボタンで「調べる / 話しかける」ことができる物（ドア・村人など）に付けます。
    /// プレイヤーが近づくとハイライト（光る縁取り）され、□を押すと On Interact に登録した処理が呼ばれます。
    ///
    /// 使い方：
    ///   1) 調べたい物（ドアのモデルなど）に Interactable を付ける
    ///   2) Prompt に画面下に出す文字（「装備を整える」など）を書く
    ///   3) On Interact の「＋」で、開きたいメニュー（ShopMenu / EquipmentMenu など）の Open を登録する
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> all = new List<Interactable>();

        [Tooltip("画面下に出す文字（□ のあとに表示される）")]
        [SerializeField] private string prompt = "調べる";
        [Tooltip("この距離（m）まで近づくと調べられる。ドアの幅などに合わせて調整")]
        [SerializeField] private float interactRadius = 2.5f;
        [Tooltip("距離を測る位置。未設定なら見た目（Renderer）の中心")]
        [SerializeField] private Transform focusPoint;

        [Header("ハイライト")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.25f, 1f);
        [Tooltip("ハイライト用マテリアル。未設定なら Resources/InteractHighlight")]
        [SerializeField] private Material highlightMaterial;

        [Header("□を押したとき")]
        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        private InteractHighlight highlight;
        private Bounds localBounds;
        private bool hasBounds;

        public string Prompt => prompt;
        public float InteractRadius => interactRadius;
        public UnityEvent OnInteract => onInteract;

        /// <summary>□を押したとき（スクリプトから処理を足したいとき用）</summary>
        public event System.Action<HomePlayerController> Interacted;

        /// <summary>距離を測る位置（ワールド座標）</summary>
        public Vector3 Position
        {
            get
            {
                if (focusPoint != null) return focusPoint.position;
                return hasBounds ? transform.TransformPoint(localBounds.center) : transform.position;
            }
        }

        private void Awake()
        {
            // 見た目の中心を覚えておく（モデルの原点がずれていても正しい位置で判定できるように）
            var renderers = GetComponentsInChildren<Renderer>();
            Bounds world = default;
            foreach (var r in renderers)
            {
                if (!hasBounds) { world = r.bounds; hasBounds = true; }
                else world.Encapsulate(r.bounds);
            }
            if (hasBounds) localBounds = new Bounds(transform.InverseTransformPoint(world.center), Vector3.zero);

            highlight = new InteractHighlight(this, renderers, highlightMaterial, highlightColor);
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable()
        {
            all.Remove(this);
            SetHighlighted(false);
        }

        private void OnDestroy() => highlight?.Dispose();

        /// <summary>ハイライトの ON / OFF</summary>
        public void SetHighlighted(bool on) => highlight?.SetVisible(on);

        /// <summary>□で調べた</summary>
        public void Interact(HomePlayerController player)
        {
            Interacted?.Invoke(player);
            onInteract.Invoke();
        }

        /// <summary>
        /// from の近くで一番調べやすいもの（範囲内で、近くて、正面に近いもの）。無ければ null
        /// </summary>
        public static Interactable FindBest(Vector3 from, Vector3 forward, float maxAngle)
        {
            Interactable best = null;
            float bestScore = float.MaxValue;
            forward.y = 0f;

            foreach (var it in all)
            {
                Vector3 to = it.Position - from;
                to.y = 0f; // 高さは無視（ドアの中心が高い位置にあっても届くように）
                float dist = to.magnitude;
                if (dist > it.interactRadius) continue;

                float angle = dist > 0.3f && forward.sqrMagnitude > 0.0001f ? Vector3.Angle(forward, to) : 0f;
                if (angle > maxAngle) continue;

                float score = dist + angle * 0.02f; // 近いほど・正面ほど優先
                if (score < bestScore)
                {
                    bestScore = score;
                    best = it;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = highlightColor;
            Vector3 p = Application.isPlaying ? Position : (focusPoint != null ? focusPoint.position : transform.position);
            Gizmos.DrawWireSphere(p, interactRadius);
        }
    }
}
