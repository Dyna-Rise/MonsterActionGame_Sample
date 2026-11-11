namespace MonsterAction.Combat
{
    /// <summary>
    /// 「攻撃を受けられるもの」の共通インターフェース。
    ///
    /// ・Player は PlayerBattleController がこれを実装済み
    /// ・Boss 担当は、ボス本体（または部位ごとの当たり判定スクリプト）にこれを実装してください
    ///
    /// 攻撃側は Collider から GetComponentInParent&lt;IDamageable&gt;() で相手を見つけ、
    /// ApplyDamage を呼ぶだけで良いので、PlayerとBossがお互いの具体的なクラスを知らなくて済みます。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>生きているか（死亡後は攻撃対象から外すために使う）</summary>
        bool IsAlive { get; }

        /// <summary>
        /// ダメージを与える。
        /// 無敵中などでダメージが通らなかった場合は false を返す。
        /// </summary>
        bool ApplyDamage(DamageInfo info);
    }
}
