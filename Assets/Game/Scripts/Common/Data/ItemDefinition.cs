using UnityEngine;

namespace MonsterAction.Data
{
    /// <summary>
    /// アイテム1種類の設定（名前・値段・効果）。ScriptableObject なので Project ウィンドウでアセットとして作ります。
    ///
    /// 作り方：Project ウィンドウで右クリック → Create → MonsterAction → Item
    /// 置き場所：Assets/Game/Resources/Items/ に置くと、ItemDefinition.All で一覧が取れます
    ///         （Home のショップでアイテムを並べるときに使う）
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterAction/Item", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("プログラムから探すための ID（英数字・他と重複しない）")]
        [SerializeField] private string id = "potion";
        [SerializeField] private string displayName = "回復薬";
        [TextArea]
        [SerializeField] private string description = "体力を30%回復する。";
        [SerializeField] private Sprite icon;

        [Header("ショップ")]
        [Tooltip("値段")]
        [SerializeField] private int price = 100;
        [Tooltip("持てる最大数")]
        [SerializeField] private int maxCount = 10;

        [Header("効果")]
        [Tooltip("最大 HP に対する回復量（0.3 = 30%）")]
        [SerializeField, Range(0f, 1f)] private float healRate = 0.3f;
        [Tooltip("使うのにかかる時間（秒）。この間は動けない")]
        [SerializeField] private float useTime = 1.0f;
        [Tooltip("使い始めてから効果が出るまでの時間（秒）。この前に攻撃を受けると失敗（アイテムは減らない）")]
        [SerializeField] private float effectTime = 0.6f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int Price => price;
        public int MaxCount => maxCount;
        public float HealRate => healRate;
        public float UseTime => useTime;
        public float EffectTime => effectTime;

        // ================================================================
        //  一覧と検索（Resources/Items フォルダから読む）
        // ================================================================

        private const string ResourcesFolder = "Items";
        private static ItemDefinition[] all;

        /// <summary>Resources/Items にある全アイテム</summary>
        public static ItemDefinition[] All => all ??= Resources.LoadAll<ItemDefinition>(ResourcesFolder);

        /// <summary>ID からアイテムを探す（無ければ null）</summary>
        public static ItemDefinition Find(string itemId)
        {
            foreach (var item in All)
                if (item != null && item.id == itemId) return item;
            return null;
        }
    }
}
