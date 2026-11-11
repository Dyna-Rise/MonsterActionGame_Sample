using MonsterAction.Data;
using UnityEditor;
using UnityEngine;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// 初期アイテム（回復薬）のアセットを作る。
    /// アイテムは Assets/Game/Resources/Items/ に置くと、ゲーム中に ItemDefinition.All / Find で見つけられる。
    /// 新しいアイテムは Project ウィンドウの右クリック → Create → MonsterAction → Item でも作れる。
    /// </summary>
    public static class ItemSetupTool
    {
        public const string ItemsFolder = "Assets/Game/Resources/Items";
        private const string PotionPath = ItemsFolder + "/Potion.asset";

        [MenuItem("MonsterAction/Data/Create Default Items (Potion)", priority = 0)]
        public static void CreateDefaultItemsMenu()
        {
            var potion = EnsureDefaultItems();
            Selection.activeObject = potion;
            EditorGUIUtility.PingObject(potion);
        }

        /// <summary>回復薬のアセットが無ければ作る</summary>
        public static ItemDefinition EnsureDefaultItems()
        {
            var potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotionPath);
            if (potion != null) return potion;

            PlayerSetupCommon.EnsureFolder(ItemsFolder);
            potion = ScriptableObject.CreateInstance<ItemDefinition>(); // 初期値が回復薬（ID: potion、30%回復、100G）
            AssetDatabase.CreateAsset(potion, PotionPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemSetup] 作成完了: {PotionPath}");
            return potion;
        }
    }
}
