#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class LTCAndroidBuildSetup
{
    private const string MenuPath = "LTC/Android/套用手機發布設定";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        PlayerSettings.companyName = "LTC Team";
        PlayerSettings.productName = "樂齡認知遊戲";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "tw.edu.ncut.ltcgame");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.statusBarHidden = true;
        PlayerSettings.runInBackground = false;

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        // Unity 6 requires at least the public symbol table when diagnostics are enabled.
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        AssetDatabase.SaveAssets();
        Debug.Log("[LTC Android] 手機發布設定已套用：橫向、自適應 UI、ARM64、Android 8.0+。 ");
    }

    [MenuItem("LTC/Android/建立測試 APK")]
    public static void BuildTestApk()
    {
        Apply();
        Directory.CreateDirectory("Builds/Android");
        string[] scenes = GetEnabledScenesWithLoginFirst();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Android/LTCGame-1.0.0-test.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };
        BuildPipeline.BuildPlayer(options);
    }

    [MenuItem("LTC/Windows/切換回 Windows 平台")]
    public static void SwitchBackToWindows()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        Debug.Log("[LTC Windows] 已切回 Windows 64 位元；Android 專用元件不會被載入。");
    }

    [MenuItem("LTC/Android/建立 ARMv7 相容模擬器測試 APK")]
    public static void BuildX86EmulatorApk()
    {
        Directory.CreateDirectory("Builds/Android");
        AndroidArchitecture previousArchitectures = PlayerSettings.Android.targetArchitectures;
        ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        try
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenesWithLoginFirst(),
                locationPathName = "Builds/Android/LTCGame-1.0.0-emulator-armv7.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("Emulator APK build failed: " + report.summary.result);
        }
        finally
        {
            PlayerSettings.Android.targetArchitectures = previousArchitectures;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, previousBackend);
        }
    }

    private static string[] GetEnabledScenesWithLoginFirst()
    {
        System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();
        const string login = "Assets/new LTC/場景轉換/LoginScene.unity";
        scenes.Add(login);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && scene.path != login)
                scenes.Add(scene.path);
        }
        return scenes.ToArray();
    }
}
#endif
