using System.Linq;
using MonsterAction.Battle.Effects;
using MonsterAction.Battle.Flow;
using MonsterAction.Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// Battle の HUD（Canvas）プレハブを作る / シーンに置くエディタ拡張。
    ///
    /// 作られる構成（Assets/Game/Prefabs/UI/BattleHUD.prefab）：
    ///   BattleHUD            Canvas + PlayerStatusHUD
    ///   ├ StatusBars         左上
    ///   │ ├ HPBar            Back(Image) / Fill(Image: Filled)
    ///   │ └ StaminaBar       Back(Image) / Fill(Image: Filled)
    ///   ├ BossBar            ボスの名前と HP（ボスがいるときだけ表示）
    ///   ├ Reticle            集中モードの照準（普段は非表示）
    ///   ├ LockOnMarker       ロックオンマーカー（普段は非表示）
    ///   └ ResultPanel        QUEST FAILED / CLEAR（CanvasGroup で普段は透明）
    ///
    /// 一度作ったら、あとはプレハブを直接編集して見た目を変えてください。
    /// （このメニューを再実行すると作り直しになり、編集内容は消えます）
    /// </summary>
    public static class BattleHUDBuilder
    {
        public const string PrefabPath = "Assets/Game/Prefabs/UI/BattleHUD.prefab";
        private const string BattleScenePath = "Assets/Scenes/Battle.unity";
        private const string HomeScenePath = "Assets/Scenes/Home.unity";

        private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color HpColor = new Color(0.3f, 0.85f, 0.35f);
        private static readonly Color StaminaColor = new Color(1f, 0.82f, 0.2f);
        private static readonly Color BossHpColor = new Color(0.85f, 0.2f, 0.2f);
        private static readonly Color ReticleColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color LockOnColor = new Color(1f, 0.35f, 0.25f, 0.95f);

        // ================================================================
        //  メニュー
        // ================================================================

        [MenuItem("MonsterAction/Battle/Build Battle HUD Prefab", priority = 0)]
        public static void BuildPrefabMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("BattleHUD を作り直しますか？",
                    $"{PrefabPath}\nを上書きします（プレハブに加えた変更は消えます）。", "作り直す", "キャンセル"))
                return;
            BuildPrefab();
        }

        [MenuItem("MonsterAction/Battle/Setup HUD + BattleFlow In Scene", priority = 1)]
        public static void SetupSceneMenu()
        {
            EnsureHUDAndFlowInScene();
            EnsureScenesInBuild();
        }

        // ================================================================
        //  シーンへの配置
        // ================================================================

        /// <summary>シーンに BattleHUD と BattleFlow が無ければ置く（PlayerSetup からも呼ばれる）</summary>
        public static void EnsureHUDAndFlowInScene()
        {
            // 旧版（Canvas を自動生成していた頃）の HUD が残っていたら消す
            var hud = Object.FindFirstObjectByType<PlayerStatusHUD>();
            if (hud != null && hud.GetComponent<Canvas>() == null)
            {
                Undo.DestroyObjectImmediate(hud.gameObject);
                hud = null;
            }

            if (hud == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) prefab = BuildPrefab();
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, "Place BattleHUD");
                hud = go.GetComponent<PlayerStatusHUD>();
            }

            var flow = Object.FindFirstObjectByType<BattleFlowController>();
            if (flow == null)
            {
                var go = new GameObject("BattleFlow", typeof(BattleFlowController));
                Undo.RegisterCreatedObjectUndo(go, "Create BattleFlow");
                flow = go.GetComponent<BattleFlowController>();
            }

            var so = new SerializedObject(flow);
            so.FindProperty("resultUI").objectReferenceValue = hud.GetComponentInChildren<BattleResultUI>(true);
            so.ApplyModifiedProperties();

            // ヒットエフェクト（火花・ダメージ数字・赤フラッシュ・カメラ揺れ）
            if (Object.FindFirstObjectByType<HitEffects>() == null)
            {
                var go = new GameObject("HitEffects", typeof(HitEffects));
                Undo.RegisterCreatedObjectUndo(go, "Create HitEffects");
            }

            EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        }

        /// <summary>Home と Battle をビルドのシーン一覧に入れる（SceneManager.LoadScene に必要）</summary>
        public static void EnsureScenesInBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;
            foreach (var path in new[] { HomeScenePath, BattleScenePath })
            {
                if (scenes.Any(s => s.path == path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
            }
            if (!changed) return;
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[BattleHUD] Home / Battle をビルドのシーン一覧に追加しました");
        }

        // ================================================================
        //  プレハブ生成
        // ================================================================

        public static GameObject BuildPrefab()
        {
            PlayerSetupCommon.EnsureFolder("Assets/Game/Prefabs/UI");

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- Canvas ----
            var root = new GameObject("BattleHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            try
            {
                // ---- 左上のゲージ ----
                var bars = CreateRect("StatusBars", root.transform, TopLeft, new Vector2(40f, -40f), new Vector2(440f, 50f));
                var hpFill = CreateBar("HPBar", bars, new Vector2(0f, 0f), new Vector2(420f, 20f), HpColor, sprite);
                var staminaFill = CreateBar("StaminaBar", bars, new Vector2(0f, -28f), new Vector2(360f, 12f), StaminaColor, sprite);

                // ---- ボスの名前と HP（画面上の中央） ----
                var topCenter = (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                var bossBar = CreateRect("BossBar", root.transform, topCenter, new Vector2(0f, -30f), new Vector2(820f, 60f));
                var bossContent = CreateRect("Content", bossBar, Stretch, Vector2.zero, Vector2.zero);
                var bossName = CreateText("BossName", bossContent, new Vector2(0f, 12f), new Vector2(820f, 30f), font, 26, FontStyle.Bold, "BOSS");
                var bossFill = CreateBar("HPBar", bossContent, new Vector2(10f, -36f), new Vector2(800f, 16f), BossHpColor, sprite);
                var bossHud = bossBar.gameObject.AddComponent<BossStatusHUD>();
                var bossSo = new SerializedObject(bossHud);
                bossSo.FindProperty("content").objectReferenceValue = bossContent.gameObject;
                bossSo.FindProperty("bossNameText").objectReferenceValue = bossName;
                bossSo.FindProperty("hpFill").objectReferenceValue = bossFill;
                bossSo.ApplyModifiedPropertiesWithoutUndo();

                // ---- 照準（画面中央の十字） ----
                var reticle = CreateRect("Reticle", root.transform, Center, Vector2.zero, new Vector2(40f, 40f));
                CreateImage("Dot", reticle, Center, Vector2.zero, new Vector2(4f, 4f), ReticleColor);
                CreateImage("Up", reticle, Center, new Vector2(0f, 14f), new Vector2(2f, 12f), ReticleColor);
                CreateImage("Down", reticle, Center, new Vector2(0f, -14f), new Vector2(2f, 12f), ReticleColor);
                CreateImage("Left", reticle, Center, new Vector2(-14f, 0f), new Vector2(12f, 2f), ReticleColor);
                CreateImage("Right", reticle, Center, new Vector2(14f, 0f), new Vector2(12f, 2f), ReticleColor);
                reticle.gameObject.SetActive(false);

                // ---- ロックオンマーカー（菱形） ----
                var marker = CreateRect("LockOnMarker", root.transform, Center, Vector2.zero, new Vector2(24f, 24f));
                var diamond = CreateImage("Diamond", marker, Center, Vector2.zero, new Vector2(16f, 16f), LockOnColor);
                diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                CreateImage("Hole", diamond.rectTransform, Center, Vector2.zero, new Vector2(10f, 10f), new Color(0f, 0f, 0f, 0.5f));
                marker.gameObject.SetActive(false);

                // ---- 結果パネル ----
                var panel = CreateImage("ResultPanel", root.transform, Stretch, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
                panel.gameObject.AddComponent<CanvasGroup>();
                var resultUI = panel.gameObject.AddComponent<BattleResultUI>();
                var title = CreateText("Title", panel.rectTransform, new Vector2(0f, 40f), new Vector2(1400f, 160f), font, 110, FontStyle.Bold, "QUEST FAILED");
                var message = CreateText("Message", panel.rectTransform, new Vector2(0f, -70f), new Vector2(1400f, 60f), font, 36, FontStyle.Normal, "5 秒後に拠点へ戻ります");

                var resultSo = new SerializedObject(resultUI);
                resultSo.FindProperty("titleText").objectReferenceValue = title;
                resultSo.FindProperty("messageText").objectReferenceValue = message;
                resultSo.ApplyModifiedPropertiesWithoutUndo();

                // ---- HUD スクリプトに参照を入れる ----
                var hud = root.AddComponent<PlayerStatusHUD>();
                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("hpFill").objectReferenceValue = hpFill;
                hudSo.FindProperty("staminaFill").objectReferenceValue = staminaFill;
                hudSo.FindProperty("reticle").objectReferenceValue = reticle.gameObject;
                hudSo.FindProperty("lockOnMarker").objectReferenceValue = marker;
                hudSo.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[BattleHUD] 作成完了: {PrefabPath}");
                EditorGUIUtility.PingObject(prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ================================================================
        //  UI 部品
        // ================================================================

        private static readonly (Vector2 min, Vector2 max, Vector2 pivot) TopLeft = (new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        private static readonly (Vector2 min, Vector2 max, Vector2 pivot) Center = (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        private static readonly (Vector2 min, Vector2 max, Vector2 pivot) Stretch = (Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

        private static RectTransform CreateRect(string name, Transform parent, (Vector2 min, Vector2 max, Vector2 pivot) anchor,
                                                Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor.min;
            rt.anchorMax = anchor.max;
            rt.pivot = anchor.pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image CreateImage(string name, Transform parent, (Vector2 min, Vector2 max, Vector2 pivot) anchor,
                                         Vector2 pos, Vector2 size, Color color)
        {
            var rt = CreateRect(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>背景 + 中身（Filled）のゲージ。中身の Image を返す</summary>
        private static Image CreateBar(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Sprite sprite)
        {
            var back = CreateImage(name, parent, TopLeft, pos, size, BackColor);
            var fill = CreateImage("Fill", back.rectTransform, Stretch, Vector2.zero, Vector2.zero, color);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f); // 枠の分だけ内側に
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.sprite = sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        private static Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size, Font font,
                                       int fontSize, FontStyle style, string text)
        {
            var rt = CreateRect(name, parent, Center, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return t;
        }
    }
}
