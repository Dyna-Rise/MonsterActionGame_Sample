namespace MonsterAction.Data
{
    /// <summary>スキルの種類</summary>
    public enum SkillType
    {
        Health,   // 体力アップ（最大HP）
        Attack,   // 攻撃力アップ（与えるダメージ）
        Defense,  // 防御力アップ（受けるダメージを減らす）
        Stamina,  // スタミナアップ（最大スタミナ）
    }

    /// <summary>
    /// スキルの設定表（名前・説明・★ごとの倍率と値段）。
    /// 値段や倍率を変えたいときはここを書き換えてください。
    ///
    ///   ★0（未購入） … 1.00 倍
    ///   ★1           … 1.05 倍
    ///   ★2           … 1.10 倍
    ///   ★3           … 1.20 倍（ここで売り切れ）
    /// </summary>
    public static class SkillData
    {
        public const int MaxLevel = 3;

        /// <summary>全スキル（ショップや装備画面に並べる順番）</summary>
        public static readonly SkillType[] All = { SkillType.Health, SkillType.Attack, SkillType.Defense, SkillType.Stamina };

        /// <summary>★の数ごとの倍率（添字 = ★の数）</summary>
        private static readonly float[] Multipliers = { 1f, 1.05f, 1.1f, 1.2f };

        /// <summary>その★を買うときの値段（添字 = 買った後の★の数。★1 より ★3 のほうが高い）</summary>
        private static readonly int[] Prices = { 0, 300, 700, 1500 };

        public static string GetName(SkillType type) => type switch
        {
            SkillType.Health => "体力アップ",
            SkillType.Attack => "攻撃力アップ",
            SkillType.Defense => "防御力アップ",
            SkillType.Stamina => "スタミナアップ",
            _ => type.ToString(),
        };

        public static string GetDescription(SkillType type) => type switch
        {
            SkillType.Health => "最大体力が上がる。",
            SkillType.Attack => "与えるダメージが上がる。",
            SkillType.Defense => "受けるダメージが減る。",
            SkillType.Stamina => "最大スタミナが上がる。",
            _ => "",
        };

        /// <summary>★の数に対応する倍率</summary>
        public static float GetMultiplier(int level) => Multipliers[UnityEngine.Mathf.Clamp(level, 0, MaxLevel)];

        /// <summary>★を level にするときの値段（level は 1〜3）</summary>
        public static int GetPrice(int level) => Prices[UnityEngine.Mathf.Clamp(level, 1, MaxLevel)];

        /// <summary>"★★☆" のような表示用の文字列</summary>
        public static string GetStars(int level) =>
            new string('★', UnityEngine.Mathf.Clamp(level, 0, MaxLevel)) + new string('☆', MaxLevel - UnityEngine.Mathf.Clamp(level, 0, MaxLevel));
    }
}
