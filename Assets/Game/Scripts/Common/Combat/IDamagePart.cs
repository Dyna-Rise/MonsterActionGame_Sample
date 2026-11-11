namespace MonsterAction.Combat
{
    /// <summary>
    /// 「本体の一部分」として攻撃を受けるもの（ボスの頭・尻尾などの部位）。
    /// IDamageable と一緒に実装し、Owner に本体を返します。
    ///
    /// 攻撃側（WeaponHitbox など）はこれを見て、
    ///   ・1回の攻撃で同じ本体に何度も当たらないようにする
    ///   ・本体のコライダーより部位のコライダーを優先して当てる
    /// といった判断をします。
    /// </summary>
    public interface IDamagePart
    {
        /// <summary>この部位の持ち主（本体）</summary>
        IDamageable Owner { get; }
    }
}
