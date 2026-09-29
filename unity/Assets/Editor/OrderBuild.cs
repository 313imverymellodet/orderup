using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity -batchmode -projectPath unity -executeMethod OrderBuild.WebGL -quit
public static class OrderBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("ORDER UP/Setup Scene")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Scenes");
        if (!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UnlitAlpha.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), "Assets/Resources/UnlitAlpha.mat");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(40, 20, 10, 255);
        camGo.transform.position = new Vector3(0, 12, -6);
        camGo.transform.rotation = Quaternion.Euler(53, 0, 0);
        camGo.AddComponent<AudioListener>();
        new GameObject("Game").AddComponent<Game>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    [MenuItem("ORDER UP/Build WebGL")]
    public static void WebGL()
    {
        Setup();
        PlayerSettings.companyName = "OrderUp";
        PlayerSettings.productName = "Order Up";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.template = "PROJECT:OrderUp";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

        // One quality level tuned for phones: hard shadows, 2x MSAA.
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../dist"));
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        Debug.Log("ORDER BUILD RESULT: " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
