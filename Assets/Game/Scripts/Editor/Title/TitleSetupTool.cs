using System.IO;
using System.Linq;
using MonsterAction.Title;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// タイトルシーンを作る。
    ///   MonsterAction > Title > Setup Title Scene
    ///     ・Assets/Scenes/Title.unity を作って（あれば開いて）、カメラ・EventSystem・タイトル画面（TitleMenu）を置く
    ///     ・ビルドのシーン一覧の先頭を Title にする（ゲームを起動するとタイトルから始まる）
    ///   もう置いてあるものは作り直しません。見た目は Scene ビューで自由に変えてください。
    /// </summary>
    public static class TitleSetupTool
    {
        public const string TitleScenePath = "Assets/Scenes/Title.unity";

        [MenuItem("MonsterAction/Title/Setup Title Scene", priority = 0)]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene;
            if (File.Exists(TitleScenePath))
            {
                scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PlayerSetupCommon.EnsureFolder(Path.GetDirectoryName(TitleScenePath).Replace('\\', '/'));
                EditorSceneManager.SaveScene(scene, TitleScenePath);
            }

            EnsureCamera();
            HomeSetupTool.EnsureEventSystem();
            if (Object.FindFirstObjectByType<TitleMenu>() == null) BuildUI();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureTitleFirstInBuild();
            Debug.Log("[TitleSetup] 完了。Title シーンから Play すると、タイトル → Home の流れを試せます。");
        }

        private static void EnsureCamera()
        {
            if (Object.FindFirstObjectByType<Camera>() != null) return;
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            Undo.RegisterCreatedObjectUndo(go, "Create Camera");
        }

        /// <summary>ビルドのシーン一覧：Title を先頭に、Home / Battle も入れる</summary>
        public static void EnsureTitleFirstInBuild()
        {
            BattleHUDBuilder.EnsureScenesInBuild();
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != TitleScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================
        //  UI
        // ================================================================

        private static void BuildUI()
        {
            var root = HomeSetupTool.CreateCanvas("TitleCanvas", 0);
            Undo.RegisterCreatedObjectUndo(root, "Create Title UI");
            var menu = root.AddComponent<TitleMenu>();
            var center = new Vector2(0.5f, 0.5f);

            // 背景
            var bg = HomeSetupTool.Img("Background", root.transform, center, Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.12f, 1f));
            HomeSetupTool.Stretch(bg.rectTransform);
            var band = HomeSetupTool.Img("Band", root.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(2400f, 260f), new Color(0.35f, 0.08f, 0.06f, 0.55f));
            band.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.72f);
            band.rectTransform.sizeDelta = new Vector2(0f, 260f);

            // タイトル
            var title = HomeSetupTool.Txt("Title", root.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1800f, 200f), 130, "MONSTER ACTION", TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.86f, 0.5f);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(4f, -4f);

            // PRESS ANY BUTTON
            var press = HomeSetupTool.Txt("PressAnyButton", root.transform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(1200f, 60f), 40, "PRESS ANY BUTTON", TextAnchor.MiddleCenter);

            // メニュー
            var window = HomeSetupTool.Img("Menu", root.transform, new Vector2(0.5f, 0.27f), Vector2.zero, new Vector2(1280f, 440f), HomeSetupTool.WindowColor);
            var header = HomeSetupTool.Txt("Header", window.transform, new Vector2(0f, 1f), new Vector2(40f, -20f), new Vector2(800f, 50f), 30, "メニュー", TextAnchor.MiddleLeft);
            header.color = HomeSetupTool.HeaderColor;
            header.fontStyle = FontStyle.Bold;

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            list.gameObject.layer = LayerMask.NameToLayer("UI");
            HomeSetupTool.Place(list, window.transform, new Vector2(0f, 1f), new Vector2(40f, -85f), new Vector2(700f, 260f));
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var row = HomeSetupTool.BuildRowTemplate(list);

            var descBack = HomeSetupTool.Img("Description", window.transform, new Vector2(1f, 1f), new Vector2(-40f, -85f), new Vector2(460f, 260f), HomeSetupTool.PanelColor);
            var desc = HomeSetupTool.Txt("Text", descBack.transform, center, Vector2.zero, new Vector2(420f, 230f), 24, "", TextAnchor.UpperLeft);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            var message = HomeSetupTool.Txt("Message", window.transform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1200f, 40f), 26, "", TextAnchor.MiddleCenter);
            message.color = HomeSetupTool.HeaderColor;
            HomeSetupTool.Txt("Hint", window.transform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1200f, 36f), 22,
                "✕ / Enter / クリック：決定　　〇 / Esc：戻る", TextAnchor.MiddleCenter).color = new Color(0.8f, 0.8f, 0.8f);

            var so = new SerializedObject(menu);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("pressText").objectReferenceValue = press;
            so.FindProperty("menuRoot").objectReferenceValue = window.gameObject;
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("listRoot").objectReferenceValue = list;
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("descriptionText").objectReferenceValue = desc;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();

            row.gameObject.SetActive(false);
            window.gameObject.SetActive(false);
        }
    }
}
