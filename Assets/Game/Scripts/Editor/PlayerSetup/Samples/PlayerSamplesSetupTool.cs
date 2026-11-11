using System.IO;
using KevinIglesias;
using MonsterAction.Battle.Player.Samples;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// （先生用）タイプB（弓）・タイプC（魔法使い）の見本を作る。
    ///   MonsterAction > Player > Samples > Build PlayerB Sample (Bow)
    ///   MonsterAction > Player > Samples > Build PlayerC Sample (Magic)
    ///
    /// できるもの（Assets/Game/Prefabs/Player/Samples/ と Animations/Player/Samples/）
    ///   PlayerB_Bow_Sample.prefab   … 弓の見本（PlayerSampleB_Bow）
    ///   PlayerC_Magic_Sample.prefab … 魔法使いの見本（PlayerSampleC_Magic）
    ///   Arrow_Sample.prefab / Fireball_Sample.prefab … 飛び道具（PlayerProjectile）
    ///
    /// Battle の PlayerSpawner は、見本があれば見本を、無ければ課題の雛形を使います
    /// （MonsterAction > Battle > Setup HUD + BattleFlow In Scene で設定）。
    /// </summary>
    public static class PlayerSamplesSetupTool
    {
        private const string MenuRoot = "MonsterAction/Player/Samples/";
        public const string AnimatorDir = PlayerSetupCommon.AnimatorDir + "/Samples";
        public const string PrefabDir = PlayerSetupCommon.PrefabDir + "/Samples";

        public const string BowControllerPath = AnimatorDir + "/PlayerB_Bow_Sample.controller";
        public const string BowPrefabPath = PrefabDir + "/PlayerB_Bow_Sample.prefab";
        public const string ArrowPrefabPath = PrefabDir + "/Arrow_Sample.prefab";

        public const string MagicControllerPath = AnimatorDir + "/PlayerC_Magic_Sample.controller";
        public const string MagicPrefabPath = PrefabDir + "/PlayerC_Magic_Sample.prefab";
        public const string FireballPrefabPath = PrefabDir + "/Fireball_Sample.prefab";

        private const string ArcherDemo = PlayerSetupCommon.KevinRoot + "/Unity Demo Scenes/Human Archer Animations";
        private const string SpellDemo = PlayerSetupCommon.KevinRoot + "/Unity Demo Scenes/Human Spellcasting Animations";
        private const string ArrowModelPath = ArcherDemo + "/Prefabs/HumanArcher_ArrowToShoot.prefab";
        private const string FireballFxPath = SpellDemo + "/Prefabs/Spells/Human_Spell_Fireball.prefab";
        private const string ExplosionFxPath = SpellDemo + "/Prefabs/Spells/Human_Spell_Shockwave_Explosion.prefab";
        private const string AuraFxPath = SpellDemo + "/Prefabs/Spells/Human_SpellAura_Fire.prefab";

        private const string BowClips = "Male/Combat/Bow/";
        private const string SpellClips = "Male/Combat/Spellcasting/MagicAttacks/";

        [MenuItem(MenuRoot + "Build All Samples (Bow + Magic)", priority = 0)]
        public static void BuildAll()
        {
            BuildBow();
            BuildMagic();
        }

        /// <summary>
        /// Battle シーンの PlayerSpawner に見本を入れる（Battle シーンを開いていなくても、裏で開いて保存する）。
        /// これをしないと、Battle では攻撃の無い雛形が使われる
        /// </summary>
        [MenuItem(MenuRoot + "Use Samples In Battle Scene", priority = 1)]
        public static void UseSamplesInBattle()
        {
            var scene = EditorSceneManager.GetSceneByPath(BattleHUDBuilder.BattleScenePath);
            bool openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleHUDBuilder.BattleScenePath) == null)
                {
                    Debug.LogWarning("[PlayerSamples] " + BattleHUDBuilder.BattleScenePath + " が見つかりません");
                    return;
                }
                scene = EditorSceneManager.OpenScene(BattleHUDBuilder.BattleScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            MonsterAction.Battle.Player.BattlePlayerSpawner spawner = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                spawner = go.GetComponentInChildren<MonsterAction.Battle.Player.BattlePlayerSpawner>(true);
                if (spawner != null) break;
            }
            if (spawner == null)
            {
                var go = new GameObject("PlayerSpawner", typeof(MonsterAction.Battle.Player.BattlePlayerSpawner));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                spawner = go.GetComponent<MonsterAction.Battle.Player.BattlePlayerSpawner>();
            }

            BattleHUDBuilder.FillPlayerSpawner(spawner);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[PlayerSamples] Battle の PlayerSpawner に見本を入れました（弓 / 魔法を選んで出撃すると見本が出ます）");
        }

        // ================================================================
        //  弓
        // ================================================================

        [MenuItem(MenuRoot + "Build PlayerB Sample (Bow)", priority = 10)]
        public static void BuildBow()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(BowPrefabPath, BowControllerPath, ArrowPrefabPath)) return;
            PlayerSetupCommon.EnsureFolder(AnimatorDir);

            // ---- 矢：デモの矢の見た目 + PlayerProjectile ----
            var arrow = BuildProjectile(ArrowModelPath, ArrowPrefabPath, radius: 0.12f, lifeTime: 3f, stick: true,
                                        impact: null, impactScale: 1f);

            // ---- Animator：共通 + 引く / 構える / 放つ ----
            var idle = PlayerSetupCommon.LoadKevinClip(BowClips + "HumanM@BowIdle01.fbx");
            var ctrl = PlayerSetupCommon.CreateCommonController(BowControllerPath, idle);
            ctrl.AddParameter("Nothing", AnimatorControllerParameterType.Trigger); // HumanArcherController 用（使わない）

            var load = PlayerSetupCommon.LoadKevinClip(BowClips + "HumanM@BowShot01 - Load.fbx");
            var hold = PlayerSetupCommon.LoadKevinClip(BowClips + "HumanM@BowShot01 - Hold.fbx");
            var release = PlayerSetupCommon.LoadKevinClip(BowClips + "HumanM@BowShot01 - Release.fbx");
            PlayerSetupCommon.AddActionState(ctrl, "BowLoad", load);
            PlayerSetupCommon.AddActionState(ctrl, "BowHold", hold);
            PlayerSetupCommon.AddActionState(ctrl, "BowRelease", release);

            // ---- Prefab：弓を手に持たせる ----
            var root = PlayerSetupCommon.CreatePlayerRoot("PlayerB_Bow_Sample", PlayerBSetupTool.ModelPrefabPath, ctrl,
                typeof(PlayerSampleB_Bow), out var model, out var player);

            var bowInHand = PlayerSetupCommon.FindChildByName(model, "HumanArcher_Bow");
            var bowSheathed = PlayerSetupCommon.FindChildByName(model, "HumanArcher_BowSheathed");
            if (bowInHand != null) bowInHand.gameObject.SetActive(true);
            if (bowSheathed != null) bowSheathed.gameObject.SetActive(false);
            var archer = model.GetComponentInChildren<HumanArcherController>(true);
            if (archer != null) archer.animationToPlay = ArcherAnimation.Nothing;

            ((PlayerSampleB_Bow)player).EditorSetup(arrow, load.length, release.length);

            PlayerSetupCommon.SavePrefab(root, BowPrefabPath);
            UseSamplesInBattle();
        }

        // ================================================================
        //  魔法使い
        // ================================================================

        [MenuItem(MenuRoot + "Build PlayerC Sample (Magic)", priority = 11)]
        public static void BuildMagic()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(MagicPrefabPath, MagicControllerPath, FireballPrefabPath)) return;
            PlayerSetupCommon.EnsureFolder(AnimatorDir);

            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionFxPath);
            var aura = AssetDatabase.LoadAssetAtPath<GameObject>(AuraFxPath);

            // ---- 火球：デモの火球エフェクト + PlayerProjectile（当たると小さな爆発） ----
            var fireball = BuildProjectile(FireballFxPath, FireballPrefabPath, radius: 0.3f, lifeTime: 3f, stick: false,
                                           impact: explosion, impactScale: 0.3f);

            // ---- Animator：共通 + 右手 / 左手の詠唱 + 溜め / 放つ ----
            var idle = PlayerSetupCommon.LoadKevinClip(PlayerCSetupTool.IdleClip);
            var ctrl = PlayerSetupCommon.CreateCommonController(MagicControllerPath, idle);
            PlayerSetupCommon.DisableHandsLayer(ctrl);

            var castRLoad = PlayerSetupCommon.LoadKevinClip(SpellClips + "Directional/HumanM@MagicAttackDirect1H01_R - Load.fbx");
            var castR = PlayerSetupCommon.LoadKevinClip(SpellClips + "Directional/HumanM@MagicAttackDirect1H01_R.fbx");
            var castL = PlayerSetupCommon.LoadKevinClip(SpellClips + "Directional/HumanM@MagicAttackDirect1H01_L.fbx");
            var chargeLoad = PlayerSetupCommon.LoadKevinClip(SpellClips + "Omnidirectional/HumanM@MagicAttackOmni01 - Load.fbx");
            var chargeCast = PlayerSetupCommon.LoadKevinClip(SpellClips + "Omnidirectional/HumanM@MagicAttackOmni01 - Cast.fbx");
            PlayerSetupCommon.AddActionState(ctrl, "CastR", castR);
            PlayerSetupCommon.AddActionState(ctrl, "CastL", castL);
            PlayerSetupCommon.AddActionState(ctrl, "ChargeLoad", chargeLoad);
            PlayerSetupCommon.AddActionState(ctrl, "ChargeCast", chargeCast);

            // ---- Prefab ----
            var root = PlayerSetupCommon.CreatePlayerRoot("PlayerC_Magic_Sample", PlayerCSetupTool.ModelPrefabPath, ctrl,
                typeof(PlayerSampleC_Magic), out _, out var player);

            ((PlayerSampleC_Magic)player).EditorSetup(fireball, aura, explosion,
                castRLoad.length, castR.length, chargeCast.length);

            PlayerSetupCommon.SavePrefab(root, MagicPrefabPath);
            UseSamplesInBattle();
        }

        // ================================================================
        //  共通
        // ================================================================

        /// <summary>見た目の Prefab を複製し、デモ用スクリプトを外して PlayerProjectile を付けた Prefab を作る</summary>
        private static PlayerProjectile BuildProjectile(string visualPath, string savePath, float radius, float lifeTime,
                                                        bool stick, GameObject impact, float impactScale)
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            if (visual == null)
            {
                Debug.LogWarning($"[PlayerSamples] {visualPath} が見つからないので、飛び道具は簡易な形で出します。");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(visual);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = Path.GetFileNameWithoutExtension(savePath);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // デモ用のスクリプト（勝手に飛んで消える HumanArcherArrow など）と当たり判定を外す
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace == "KevinIglesias") Object.DestroyImmediate(mb);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            var p = go.AddComponent<PlayerProjectile>();
            var so = new SerializedObject(p);
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("lifeTime").floatValue = lifeTime;
            so.FindProperty("stickOnHit").boolValue = stick;
            so.FindProperty("impactEffect").objectReferenceValue = impact;
            so.FindProperty("impactEffectScale").floatValue = impactScale;
            so.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                PlayerSetupCommon.EnsureFolder(Path.GetDirectoryName(savePath).Replace('\\', '/'));
                return PrefabUtility.SaveAsPrefabAsset(go, savePath).GetComponent<PlayerProjectile>();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
