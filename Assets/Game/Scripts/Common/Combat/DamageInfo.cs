using UnityEngine;

namespace MonsterAction.Combat
{
    /// <summary>
    /// 攻撃1回分のダメージ情報。
    /// Player → Boss、Boss → Player のどちらでも同じ型を使います。
    /// 属性・部位破壊・状態異常などを足したくなったら、ここにフィールドを追加してください。
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>ダメージ量</summary>
        public float Amount;

        /// <summary>当たった位置（ヒットエフェクトの表示位置などに使う）</summary>
        public Vector3 HitPoint;

        /// <summary>攻撃の向き（攻撃者 → 被弾者、水平方向）。ノックバック方向に使う</summary>
        public Vector3 Direction;

        /// <summary>攻撃した GameObject（自分の攻撃が自分に当たるのを防ぐ判定などに使う）</summary>
        public GameObject Attacker;

        /// <summary>のけぞり（ひるみ）を発生させるか</summary>
        public bool CausesFlinch;

        /// <summary>ノックバック距離（m）。0 ならその場でひるむ</summary>
        public float KnockbackDistance;

        /// <summary>
        /// ダウンさせる大攻撃か。true なら、受けた側は吹き飛んで倒れ、しばらく起き上がれない。
        /// （CausesFlinch より強いリアクション。ガードに成功すればダウンしない）
        /// </summary>
        public bool KnockDown;

        public DamageInfo(float amount, Vector3 hitPoint, Vector3 direction, GameObject attacker,
                          bool causesFlinch = true, float knockbackDistance = 0f, bool knockDown = false)
        {
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction;
            Attacker = attacker;
            CausesFlinch = causesFlinch;
            KnockbackDistance = knockbackDistance;
            KnockDown = knockDown;
        }
    }
}
