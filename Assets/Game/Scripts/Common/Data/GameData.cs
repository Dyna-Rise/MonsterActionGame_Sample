using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterAction.Data
{
    /// <summary>
    /// Home と Battle で共有するデータ（所持金・アイテム・スキル・プレイヤータイプ）。
    /// static なので、シーンを切り替えても値が残ります（ゲームを終了すると初期値に戻る）。
    ///
    /// 【Home のショップ担当の人へ】
    ///   GameData.Money                         … 所持金
    ///   GameData.GetCount(item)                … 所持数
    ///   GameData.TryBuy(item)                  … 買う（お金が足りない / 最大数ならfalse）
    ///   GameData.MoneyChanged / ItemCountChanged … 表示を更新するためのイベント
    ///   ItemDefinition.All                     … 売っているアイテムの一覧
    ///   GameData.GetSkillLevel(type) / TryBuySkill(type) … スキルの★と購入（値段と倍率は SkillData）
    ///
    /// 【装備・タイプ選択の担当の人へ】
    ///   GameData.SetSkillEquipped(type, on)    … スキルを付ける / 外す（MaxEquippedSkills 個まで）
    ///   GameData.SelectedPlayerTypeId          … Battle で使うプレイヤータイプ
    ///
    /// 【出撃の担当の人へ】
    ///   GameData.SelectedBossId                … Battle で戦うボス（BattleBossSpawner の一覧と合わせる）
    ///
    /// 【Battle では】
    ///   回復薬を使う → TryConsume(item)、ボスを倒す → AddMoney(報酬)
    ///   スキルの効果 → GetSkillMultiplier(type)（付けているスキルだけ効く）
    /// </summary>
    public static class GameData
    {
        // ---- 初期値（テスト用） ----
        public const int StartMoney = 300;
        public const string StartItemId = "potion";
        public const int StartItemCount = 2;
        /// <summary>同時に付けられるスキルの数</summary>
        public const int MaxEquippedSkills = 2;
        /// <summary>最初に選ばれているプレイヤータイプ</summary>
        public const string DefaultPlayerTypeId = "SwordShield";
        /// <summary>最初に選ばれているボス（出撃先）</summary>
        public const string DefaultBossId = "DragonNightmare";

        private static int money;
        private static readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>();
        private static readonly Dictionary<SkillType, int> skillLevels = new Dictionary<SkillType, int>();
        private static readonly HashSet<SkillType> equippedSkills = new HashSet<SkillType>();
        private static string selectedPlayerTypeId = DefaultPlayerTypeId;
        private static string selectedBossId = DefaultBossId;
        private static int questClearCount;

        /// <summary>所持金が変わった（新しい所持金）</summary>
        public static event Action<int> MoneyChanged;
        /// <summary>アイテムの所持数が変わった（アイテム, 新しい所持数）</summary>
        public static event Action<ItemDefinition, int> ItemCountChanged;
        /// <summary>スキルの★、または付け外しが変わった</summary>
        public static event Action<SkillType> SkillChanged;
        /// <summary>プレイヤータイプが変わった（新しいタイプの ID）</summary>
        public static event Action<string> PlayerTypeChanged;
        /// <summary>出撃するボスが変わった（新しいボスの ID）</summary>
        public static event Action<string> BossChanged;

        public static int Money => money;

        // Play ボタンを押すたびに初期値に戻す（ドメインリロードを切っていても正しく動くように）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            MoneyChanged = null;
            ItemCountChanged = null;
            SkillChanged = null;
            PlayerTypeChanged = null;
            BossChanged = null;
            ResetToDefault();
        }

        /// <summary>初期状態に戻す</summary>
        public static void ResetToDefault()
        {
            money = StartMoney;
            itemCounts.Clear();
            itemCounts[StartItemId] = StartItemCount;
            skillLevels.Clear();
            equippedSkills.Clear();
            selectedPlayerTypeId = DefaultPlayerTypeId;
            selectedBossId = DefaultBossId;
            questClearCount = 0;
        }

        // ================================================================
        //  お金
        // ================================================================

        public static void AddMoney(int amount)
        {
            if (amount <= 0) return;
            money += amount;
            MoneyChanged?.Invoke(money);
        }

        /// <summary>お金を払う。足りなければ払わずに false</summary>
        public static bool TrySpendMoney(int amount)
        {
            if (amount < 0 || money < amount) return false;
            money -= amount;
            MoneyChanged?.Invoke(money);
            return true;
        }

        // ================================================================
        //  アイテム
        // ================================================================

        public static int GetCount(ItemDefinition item) =>
            item != null && itemCounts.TryGetValue(item.Id, out int c) ? c : 0;

        /// <summary>アイテムを増やす（最大数を超えた分は捨てる）。実際に増えた数を返す</summary>
        public static int AddItem(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;
            int before = GetCount(item);
            int after = Mathf.Min(item.MaxCount, before + count);
            itemCounts[item.Id] = after;
            if (after != before) ItemCountChanged?.Invoke(item, after);
            return after - before;
        }

        /// <summary>アイテムを1つ減らす。持っていなければ false</summary>
        public static bool TryConsume(ItemDefinition item)
        {
            int count = GetCount(item);
            if (count <= 0) return false;
            itemCounts[item.Id] = count - 1;
            ItemCountChanged?.Invoke(item, count - 1);
            return true;
        }

        /// <summary>ショップで1つ買う。お金が足りない / 最大数まで持っているなら false</summary>
        public static bool TryBuy(ItemDefinition item)
        {
            if (item == null || GetCount(item) >= item.MaxCount) return false;
            if (!TrySpendMoney(item.Price)) return false;
            AddItem(item, 1);
            return true;
        }

        // ================================================================
        //  スキル
        // ================================================================

        /// <summary>スキルの★の数（0 = 未購入）</summary>
        public static int GetSkillLevel(SkillType type) => skillLevels.TryGetValue(type, out int lv) ? lv : 0;

        /// <summary>★3 まで買ってある（売り切れ）</summary>
        public static bool IsSkillMaxed(SkillType type) => GetSkillLevel(type) >= SkillData.MaxLevel;

        /// <summary>次の★の値段（売り切れなら 0）</summary>
        public static int GetNextSkillPrice(SkillType type) =>
            IsSkillMaxed(type) ? 0 : SkillData.GetPrice(GetSkillLevel(type) + 1);

        /// <summary>
        /// スキルの★を1つ上げる。お金が足りない / 売り切れなら false。
        /// 初めて買ったときは、枠に空きがあれば自動で装備する。
        /// </summary>
        public static bool TryBuySkill(SkillType type)
        {
            if (IsSkillMaxed(type)) return false;
            if (!TrySpendMoney(GetNextSkillPrice(type))) return false;

            int level = GetSkillLevel(type) + 1;
            skillLevels[type] = level;
            if (level == 1 && equippedSkills.Count < MaxEquippedSkills) equippedSkills.Add(type);
            SkillChanged?.Invoke(type);
            return true;
        }

        public static bool IsSkillEquipped(SkillType type) => equippedSkills.Contains(type);

        public static int EquippedSkillCount => equippedSkills.Count;

        /// <summary>
        /// スキルを付ける / 外す。未購入のスキルや、枠がいっぱいのときに付けようとすると false
        /// </summary>
        public static bool SetSkillEquipped(SkillType type, bool equipped)
        {
            if (equipped == IsSkillEquipped(type)) return true;
            if (equipped)
            {
                if (GetSkillLevel(type) <= 0 || equippedSkills.Count >= MaxEquippedSkills) return false;
                equippedSkills.Add(type);
            }
            else
            {
                equippedSkills.Remove(type);
            }
            SkillChanged?.Invoke(type);
            return true;
        }

        /// <summary>Battle で使う倍率。付けているスキルは★に応じた倍率、付けていなければ 1</summary>
        public static float GetSkillMultiplier(SkillType type) =>
            IsSkillEquipped(type) ? SkillData.GetMultiplier(GetSkillLevel(type)) : 1f;

        // ================================================================
        //  プレイヤータイプ
        // ================================================================

        /// <summary>Battle で使うプレイヤータイプの ID（"SwordShield" / "Bow" など。BattlePlayerSpawner の一覧と合わせる）</summary>
        public static string SelectedPlayerTypeId
        {
            get => selectedPlayerTypeId;
            set
            {
                if (string.IsNullOrEmpty(value) || value == selectedPlayerTypeId) return;
                selectedPlayerTypeId = value;
                PlayerTypeChanged?.Invoke(value);
            }
        }

        // ================================================================
        //  出撃するボス
        // ================================================================

        /// <summary>Battle で戦うボスの ID（"DragonNightmare" など。BattleBossSpawner の一覧と合わせる）</summary>
        public static string SelectedBossId
        {
            get => selectedBossId;
            set
            {
                if (string.IsNullOrEmpty(value) || value == selectedBossId) return;
                selectedBossId = value;
                BossChanged?.Invoke(value);
            }
        }

        // ================================================================
        //  記録
        // ================================================================

        /// <summary>クエストをクリアした回数</summary>
        public static int QuestClearCount => questClearCount;

        /// <summary>クエストクリア（BattleFlowController が呼ぶ）</summary>
        public static void AddQuestClear() => questClearCount++;

        // ================================================================
        //  セーブ / ロード（SaveSystem から使う）
        // ================================================================

        /// <summary>今の値をセーブ用のデータにする</summary>
        public static SaveData ToSaveData()
        {
            var data = new SaveData
            {
                money = money,
                playerTypeId = selectedPlayerTypeId,
                bossId = selectedBossId,
                questClearCount = questClearCount,
            };
            foreach (var kv in itemCounts) data.items.Add(new SaveData.ItemCount { id = kv.Key, count = kv.Value });
            foreach (var kv in skillLevels) data.skills.Add(new SaveData.SkillLevel { skill = kv.Key.ToString(), level = kv.Value });
            foreach (var s in equippedSkills) data.equippedSkills.Add(s.ToString());
            return data;
        }

        /// <summary>セーブデータの値を読み込む（足りない値は初期値のまま）</summary>
        public static void LoadFromSaveData(SaveData data)
        {
            ResetToDefault();
            if (data == null) return;

            money = Mathf.Max(0, data.money);
            if (data.items != null)
            {
                itemCounts.Clear();
                foreach (var it in data.items)
                    if (!string.IsNullOrEmpty(it.id)) itemCounts[it.id] = Mathf.Max(0, it.count);
            }
            if (data.skills != null)
                foreach (var s in data.skills)
                    if (Enum.TryParse(s.skill, out SkillType type)) skillLevels[type] = Mathf.Clamp(s.level, 0, SkillData.MaxLevel);
            if (data.equippedSkills != null)
                foreach (var name in data.equippedSkills)
                    if (Enum.TryParse(name, out SkillType type) && GetSkillLevel(type) > 0 && equippedSkills.Count < MaxEquippedSkills)
                        equippedSkills.Add(type);
            if (!string.IsNullOrEmpty(data.playerTypeId)) selectedPlayerTypeId = data.playerTypeId;
            if (!string.IsNullOrEmpty(data.bossId)) selectedBossId = data.bossId;
            questClearCount = Mathf.Max(0, data.questClearCount);

            MoneyChanged?.Invoke(money);
            PlayerTypeChanged?.Invoke(selectedPlayerTypeId);
            BossChanged?.Invoke(selectedBossId);
        }
    }
}
