using System.Collections.Generic;
using MonsterAction.Battle.Player;
using UnityEditor;
using UnityEngine;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// PlayerA（片手剣）の Animator Controller と Prefab を作る。
    /// 共通部分は PlayerSetupCommon、ここには片手剣に固有の部分だけを書いています。
    /// 新しいタイプのセットアップを作るときの見本にしてください。
    /// </summary>
    public static class PlayerASetupTool
    {
        private const string ModelPrefabPath = PlayerSetupCommon.KevinRoot +
            "/Unity Demo Scenes/Human Melee Animations/Prefabs/Characters/HumanM_Dummy_Red - Sword and Shield.prefab";

        private const string GuardClipPath = "Assets/MyAssets/Mixamo/Sword And Shield Block Idle.fbx";

        public const string ControllerPath = PlayerSetupCommon.AnimatorDir + "/PlayerA_SwordShield.controller";
        public const string PrefabPath = PlayerSetupCommon.PrefabDir + "/PlayerA_SwordShield.prefab";

        [MenuItem("MonsterAction/Player/Build PlayerA (Sword & Shield)", priority = 0)]
        public static void Build()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(PrefabPath, ControllerPath)) return;
            PlayerSetupCommon.EnsureFolder(PlayerSetupCommon.AnimatorDir);

            // ---- Animator：共通ステート + 片手剣の攻撃3つ ----
            var idle = PlayerSetupCommon.LoadKevinClip("Male/Combat/1H/HumanM@CombatIdle1H01.fbx");
            var ctrl = PlayerSetupCommon.CreateCommonController(ControllerPath, idle);

            var attack1 = PlayerSetupCommon.LoadKevinClip("Male/Combat/1H/HumanM@Attack1H01_R.fbx");
            var attack2 = PlayerSetupCommon.LoadKevinClip("Male/Combat/1H/HumanM@Attack1H01_L.fbx");
            var attack3 = PlayerSetupCommon.LoadKevinClip("Male/Combat/Shield/HumanM@AttackShield01.fbx");
            PlayerSetupCommon.AddActionState(ctrl, "Attack1", attack1);
            PlayerSetupCommon.AddActionState(ctrl, "Attack2", attack2);
            PlayerSetupCommon.AddActionState(ctrl, "Attack3", attack3);

            // ガード：Mixamo の盾構え（ループ）
            PlayerSetupCommon.ConfigureMixamoClip(GuardClipPath, loop: true);
            var guard = ctrl.layers[0].stateMachine.AddState("Guard");
            guard.motion = PlayerSetupCommon.LoadClipAt(GuardClipPath);

            // ---- Prefab：共通の土台 + 剣の当たり判定 + コンボ設定 ----
            var root = PlayerSetupCommon.CreatePlayerRoot("PlayerA_SwordShield", ModelPrefabPath, ctrl,
                typeof(PlayerTypeA_SwordShield), out var model, out var player);

            WeaponHitbox hitbox = null;
            var sword = PlayerSetupCommon.FindChildByName(model, "Human_Sword");
            if (sword != null)
            {
                hitbox = sword.gameObject.AddComponent<WeaponHitbox>();
                FitHitboxToMesh(hitbox);
            }
            else
            {
                Debug.LogWarning("[PlayerASetup] Human_Sword が見つからないため、WeaponHitbox を手動で付けてください。");
            }

            ((PlayerTypeA_SwordShield)player).EditorSetup(hitbox, new List<AttackStep>
            {
                // 1段目：右からの斬り下ろし
                new AttackStep
                {
                    animState = "Attack1", clipLength = attack1.length, animSpeed = 1.2f, damage = 12f,
                    hitStart = 0.25f, hitEnd = 0.5f, comboInputStart = 0.1f, comboNext = 0.5f,
                    dodgeCancel = 0.45f, moveCancel = 0.8f, lungeDistance = 0.5f, lungeStart = 0.1f, lungeEnd = 0.35f,
                },
                // 2段目：左からの斬り返し
                new AttackStep
                {
                    animState = "Attack2", clipLength = attack2.length, animSpeed = 1.2f, damage = 14f,
                    hitStart = 0.25f, hitEnd = 0.5f, comboInputStart = 0.1f, comboNext = 0.5f,
                    dodgeCancel = 0.45f, moveCancel = 0.8f, lungeDistance = 0.5f, lungeStart = 0.1f, lungeEnd = 0.35f,
                },
                // 3段目：盾攻撃（ひるみ付き）
                new AttackStep
                {
                    animState = "Attack3", clipLength = attack3.length, animSpeed = 1.1f, damage = 18f, causesFlinch = true,
                    hitStart = 0.3f, hitEnd = 0.55f, comboInputStart = 0.2f, comboNext = 0.65f,
                    dodgeCancel = 0.55f, moveCancel = 0.85f, lungeDistance = 0.8f, lungeStart = 0.15f, lungeEnd = 0.4f,
                },
            });

            PlayerSetupCommon.SavePrefab(root, PrefabPath);
        }

        /// <summary>剣のメッシュの一番長い軸に沿ってカプセルを合わせる</summary>
        private static void FitHitboxToMesh(WeaponHitbox hitbox)
        {
            var mf = hitbox.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds b = mf.sharedMesh.bounds;
            Matrix4x4 toHitbox = hitbox.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

            Vector3 ext = b.extents;
            Vector3 axis = ext.x >= ext.y && ext.x >= ext.z ? new Vector3(ext.x, 0, 0)
                         : ext.y >= ext.z ? new Vector3(0, ext.y, 0)
                         : new Vector3(0, 0, ext.z);

            Vector3 a = toHitbox.MultiplyPoint3x4(b.center - axis);
            Vector3 bb = toHitbox.MultiplyPoint3x4(b.center + axis);

            float worldLen = (hitbox.transform.TransformPoint(a) - hitbox.transform.TransformPoint(bb)).magnitude;
            float worldRadius = Mathf.Clamp(worldLen * 0.12f, 0.08f, 0.2f);
            float scale = Mathf.Max(0.0001f, hitbox.transform.lossyScale.x);

            hitbox.pointA = a;
            hitbox.pointB = bb;
            hitbox.radius = worldRadius / scale;
        }
    }
}
