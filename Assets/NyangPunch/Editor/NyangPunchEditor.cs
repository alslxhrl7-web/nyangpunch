using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NyangPunch.EditorTools
{
    /// <summary>Import settings for the exported sprites (9-slice borders for ui_*), and a one-click scene builder.</summary>
    public class SpriteImport : AssetPostprocessor
    {
        const string Dir = "Assets/NyangPunch/Resources/Sprites/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            // Actor sizes come from sprite_meta.json, so PPU only matters for 9-slice borders:
            // ui_* are exported at 2x, and Images use pixelsPerUnitMultiplier = 2 to draw borders at 1 game unit per 2 px.
            ti.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);
            if (name == "ui_fill" || name == "ui_ring") ti.spriteBorder = new Vector4(32, 32, 32, 32);
            else if (name == "ui_pill" || name == "ui_pill_ring") ti.spriteBorder = new Vector4(46, 46, 46, 46);
            else if (name == "ui_cell") ti.spriteBorder = new Vector4(26, 26, 26, 26);
            else ti.spriteBorder = Vector4.zero;
        }

        [MenuItem("NyangPunch/Reimport Sprites")]
        public static void ReimportSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/NyangPunch/Resources/Sprites" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("NyangPunch: sprites reimported");
        }
    }

    /// <summary>Editor test mode: every Play starts from a fresh save so the current balance is what gets tested.</summary>
    [InitializeOnLoad]
    public static class FreshSaveOnPlay
    {
        const string Pref = "NyangPunch.FreshSaveOnPlay";
        const string MenuPath = "NyangPunch/테스트: 플레이마다 새로 시작";
        public static bool Enabled { get => EditorPrefs.GetBool(Pref, true); set => EditorPrefs.SetBool(Pref, value); }

        static FreshSaveOnPlay()
        {
            // Play always starts the game scene, even if an empty scene is open in the editor.
            EditorApplication.delayCall += () => EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBuilder.ScenePath);
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.ExitingEditMode || !Enabled) return;
                PlayerPrefs.DeleteKey(NyangPunch.Defs.SaveKey);
                PlayerPrefs.Save();
                Debug.Log("NyangPunch 테스트 모드: 새 저장으로 시작합니다 (메뉴 " + MenuPath + "에서 끌 수 있음)");
            };
        }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }
    }

    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/NyangPunch/Scenes/Main.unity";

        [MenuItem("NyangPunch/Build Main Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/NyangPunch/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            var c = cam.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color32(0xdf, 0xe4, 0xf4, 255);
            c.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            new GameObject("NyangPunch", typeof(NyangPunch.Game));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "냥펀치";
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log("NyangPunch: main scene built at " + ScenePath);
        }

        [MenuItem("NyangPunch/Reset Save")]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteKey(NyangPunch.Defs.SaveKey);
            PlayerPrefs.Save();
            Debug.Log("NyangPunch: save cleared");
        }
    }
}
