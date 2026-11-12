using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NyangPunch.EditorTools
{
    /// <summary>
    /// WebGL build tuned for itch.io: Brotli files decompressed by the loader (itch does not send Content-Encoding headers),
    /// minimal template, portrait 540x960 canvas, and the TextMeshPro SDF shaders forced into the build
    /// because the Jua font asset is created at runtime and nothing references the shader at build time.
    /// Command line: Unity -batchmode -quit -projectPath ... -executeMethod NyangPunch.EditorTools.WebGLBuild.Build
    /// </summary>
    public static class WebGLBuild
    {
        public const string OutDir = "Builds/WebGL";

        [MenuItem("NyangPunch/Build WebGL (itch.io)")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("NyangPunch: 이 유니티 버전에는 WebGL 빌드 지원이 설치돼 있지 않습니다 (Unity Hub > 설치 > 모듈 추가 > WebGL Build Support).");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            PlayerSettings.productName = "냥펀치";
            PlayerSettings.companyName = "p로딩";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // loader decompresses in JS, so no server headers needed
            // keep the upload small (Claude in Chrome uploads at most 10 MB per file)
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            IncludeShaders("TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field", "UI/Default");

            var scenes = new[] { SceneBuilder.ScenePath };
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) WriteIndex();
            Debug.Log($"NyangPunch WebGL build: {s.result}, {s.totalSize / 1024 / 1024} MB, {s.totalTime.TotalSeconds:0}s, errors {s.totalErrors} -> {Path.GetFullPath(OutDir)}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Replaces Unity's fixed-size page with one that fills the window and shows a Korean loading bar.</summary>
        static void WriteIndex()
        {
            const string tpl = "Assets/NyangPunch/Editor/WebGLTemplate/index.template.html.txt";
            if (!File.Exists(tpl)) { Debug.LogWarning("NyangPunch: index template missing, keeping Unity's page"); return; }
            string F(string pattern) => Path.GetFileName(Directory.GetFiles(Path.Combine(OutDir, "Build"), pattern).FirstOrDefault() ?? "");
            var html = File.ReadAllText(tpl)
                .Replace("{{LOADER}}", F("*.loader.js"))
                .Replace("{{DATA}}", F("*.data*"))
                .Replace("{{FRAMEWORK}}", F("*.framework.js*"))
                .Replace("{{CODE}}", F("*.wasm*"));
            File.WriteAllText(Path.Combine(OutDir, "index.html"), html);
        }

        static void IncludeShaders(params string[] names)
        {
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning("NyangPunch: shader not found " + n); continue; }
                bool has = Enumerable.Range(0, arr.arraySize).Any(i => arr.GetArrayElementAtIndex(i).objectReferenceValue == sh);
                if (has) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
