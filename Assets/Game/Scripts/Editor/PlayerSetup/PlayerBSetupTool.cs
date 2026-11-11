using MonsterAction.Battle.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// PlayerB（弓）の「雛形」を作る。共通の動き（移動・ダッシュ・回避・被弾・集中モード）だけが入った状態で、
    /// 弓の攻撃は入っていません。弓担当の人はここから作り込みます。
    ///
    /// ・モデルは Kevin Iglesias の HumanM_Archer（弓を手に持った状態にします）
    /// ・モデルに付いている HumanArcherController（デモ用）は、弦の描画や「引く / 放つ」の弓の見た目を
    ///   担当しているので残しています。LoadBow / ShootArrow / GetArrow を呼ぶと弓が動きます。
    ///
    /// ※ 弓担当の人が Animator にステートを追加した後でこのメニューを実行すると、追加分は消えます。
    /// </summary>
    public static class PlayerBSetupTool
    {
        private const string ModelPrefabPath = PlayerSetupCommon.KevinRoot +
            "/Unity Demo Scenes/Human Archer Animations/Prefabs/HumanM_Archer.prefab";

        public const string ControllerPath = PlayerSetupCommon.AnimatorDir + "/PlayerB_Bow.controller";
        public const string PrefabPath = PlayerSetupCommon.PrefabDir + "/PlayerB_Bow.prefab";

        [MenuItem("MonsterAction/Player/Build PlayerB (Bow) Template", priority = 1)]
        public static void Build()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(PrefabPath, ControllerPath)) return;
            PlayerSetupCommon.EnsureFolder(PlayerSetupCommon.AnimatorDir);

            // ---- Animator：共通ステートのみ（待機は弓の構え） ----
            var idle = PlayerSetupCommon.LoadKevinClip("Male/Combat/Bow/HumanM@BowIdle01.fbx");
            var ctrl = PlayerSetupCommon.CreateCommonController(ControllerPath, idle);

            // HumanArcherController（デモ用スクリプト）が毎フレーム SetTrigger("Nothing") を呼ぶので、
            // 警告が出ないように同名のトリガーだけ用意しておく（使わない）
            ctrl.AddParameter("Nothing", AnimatorControllerParameterType.Trigger);

            // ---- Prefab：共通の土台 + 弓を手に持たせる ----
            var root = PlayerSetupCommon.CreatePlayerRoot("PlayerB_Bow", ModelPrefabPath, ctrl,
                typeof(PlayerTypeB_Bow), out var model, out _);

            var bowInHand = PlayerSetupCommon.FindChildByName(model, "HumanArcher_Bow");
            var bowSheathed = PlayerSetupCommon.FindChildByName(model, "HumanArcher_BowSheathed");
            if (bowInHand != null) bowInHand.gameObject.SetActive(true);
            if (bowSheathed != null) bowSheathed.gameObject.SetActive(false);

            PlayerSetupCommon.SavePrefab(root, PrefabPath);
        }
    }
}
