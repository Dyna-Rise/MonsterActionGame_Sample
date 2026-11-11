using MonsterAction.Home.Player;
using MonsterAction.Home.UI;
using UnityEngine;
using UnityEngine.Events;

namespace MonsterAction.Home.Interaction
{
    /// <summary>
    /// 出撃口（村の出口）。プレイヤーがこの箱の中に入ると On Enter を呼びます（出撃画面を開く）。
    ///
    /// 【置き方】
    ///   柵の間など、村の外へ出るところに置き、Scene ビューの緑の箱が通り道をふさぐように
    ///   位置・向き・Size を合わせてください。
    ///
    ///   一度反応したら、箱の外に出るまでは再び反応しません（メニューを閉じたあと、すぐまた開かないように）。
    /// </summary>
    public class DepartureGate : MonoBehaviour
    {
        [Tooltip("反応する範囲（この GameObject から見た大きさ。m）")]
        [SerializeField] private Vector3 size = new Vector3(3f, 2.5f, 1.5f);
        [Tooltip("範囲の中心のずれ")]
        [SerializeField] private Vector3 center = new Vector3(0f, 1.25f, 0f);
        [SerializeField] private UnityEvent onEnter = new UnityEvent();

        public UnityEvent OnEnter => onEnter;

        private bool armed = true;

        private void Update()
        {
            var player = HomePlayerController.Active;
            if (player == null) return;

            bool inside = Contains(player.transform.position + Vector3.up * 0.9f);
            if (!inside)
            {
                armed = true;
                return;
            }

            // メニューを開いている / 操作できないときは反応しない
            if (!armed || HomeMenuBase.Current != null || !player.ControlEnabled) return;
            armed = false;
            onEnter.Invoke();
        }

        public bool Contains(Vector3 worldPos)
        {
            Vector3 p = transform.InverseTransformPoint(worldPos) - center;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
