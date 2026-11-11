using MonsterAction.Battle.Player;
using UnityEditor;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// PlayerC（魔法使い）の「雛形」を作る。共通の動き（移動・ダッシュ・回避・被弾・集中モード）だけが入った状態で、
    /// 魔法の攻撃は入っていません。魔法担当の人はここから作り込みます。
    ///
    /// ・モデルは Kevin Iglesias の HumanM_Dummy_Purple（Spellcasting のデモのキャラ）
    /// ・待機は詠唱の構え（CastingIdle01）。手を開くので、武器を握る Hands レイヤーは切っています
    ///
    /// ※ 魔法担当の人が Animator にステートを追加した後でこのメニューを実行すると、追加分は消えます。
    /// </summary>
    public static class PlayerCSetupTool
    {
        public const string ModelPrefabPath = PlayerSetupCommon.KevinRoot +
            "/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/HumanM_Dummy_Purple.prefab";
        public const string IdleClip = "Male/Combat/Spellcasting/HumanM@CastingIdle01.fbx";

        public const string ControllerPath = PlayerSetupCommon.AnimatorDir + "/PlayerC_Magic.controller";
        public const string PrefabPath = PlayerSetupCommon.PrefabDir + "/PlayerC_Magic.prefab";

        [MenuItem("MonsterAction/Player/Build PlayerC (Magic) Template", priority = 2)]
        public static void Build()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(PrefabPath, ControllerPath)) return;
            PlayerSetupCommon.EnsureFolder(PlayerSetupCommon.AnimatorDir);

            // ---- Animator：共通ステートのみ（待機は詠唱の構え） ----
            var idle = PlayerSetupCommon.LoadKevinClip(IdleClip);
            var ctrl = PlayerSetupCommon.CreateCommonController(ControllerPath, idle);
            PlayerSetupCommon.DisableHandsLayer(ctrl);

            // ---- Prefab：共通の土台だけ ----
            var root = PlayerSetupCommon.CreatePlayerRoot("PlayerC_Magic", ModelPrefabPath, ctrl,
                typeof(PlayerTypeC_Magic), out _, out _);

            PlayerSetupCommon.SavePrefab(root, PrefabPath);
        }
    }
}
