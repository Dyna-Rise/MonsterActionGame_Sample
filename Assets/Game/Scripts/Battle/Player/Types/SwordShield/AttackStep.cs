using System;
using UnityEngine;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// コンボ1段分の設定。インスペクターで調整できます。
    /// 時間系はすべて「この攻撃の長さを 1 とした割合（0〜1）」です。
    ///
    ///  0 ──── comboInputStart ── hitStart ━━ hitEnd ── comboNext ── dodgeCancel ── moveCancel ── 1
    ///         ↑ここから次の入力を      ↑攻撃判定ON       ↑次段へ    ↑回避で          ↑移動で
    ///           受け付ける                                移れる      キャンセル可     キャンセル可
    /// </summary>
    [Serializable]
    public class AttackStep
    {
        [Tooltip("Animator のステート名")]
        public string animState = "Attack1";

        [Tooltip("アニメーションクリップの長さ（秒）。セットアップツールが自動で入れます")]
        public float clipLength = 1f;

        [Tooltip("再生速度（大きいほど速い）")]
        public float animSpeed = 1f;

        [Header("ダメージ")]
        public float damage = 10f;
        [Tooltip("相手をひるませるか（ボス側で無視しても良い）")]
        public bool causesFlinch = false;

        [Header("タイミング（0〜1）")]
        [Range(0f, 1f)] public float hitStart = 0.3f;
        [Range(0f, 1f)] public float hitEnd = 0.5f;
        [Range(0f, 1f)] public float comboInputStart = 0.15f;
        [Range(0f, 1f)] public float comboNext = 0.55f;
        [Range(0f, 1f)] public float dodgeCancel = 0.5f;
        [Range(0f, 1f)] public float moveCancel = 0.85f;

        [Header("踏み込み")]
        [Tooltip("攻撃中に前進する距離（m）")]
        public float lungeDistance = 0.6f;
        [Range(0f, 1f)] public float lungeStart = 0.1f;
        [Range(0f, 1f)] public float lungeEnd = 0.35f;

        /// <summary>実際の再生時間（秒）</summary>
        public float Duration => clipLength / Mathf.Max(0.01f, animSpeed);
    }
}
