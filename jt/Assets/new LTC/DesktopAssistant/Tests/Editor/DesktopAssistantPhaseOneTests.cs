using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant.Tests
{
    /// <summary>
    /// 第一階段的資產完整性測試。使用型別名稱檢查，避免測試組件改變既有 Assembly-CSharp 架構。
    /// </summary>
    public sealed class DesktopAssistantPhaseOneTests
    {
        const string PrefabPath = "Assets/new LTC/DesktopAssistant/Resources/DesktopAssistant.prefab";
        const string ScenePath = "Assets/new LTC/DesktopAssistant/Scenes/DesktopAssistantDemo.unity";
        const string SettingsPath = "Assets/new LTC/DesktopAssistant/Config/DesktopAssistantSettings.asset";
        const string PhaseTwoPrefabPath = "Assets/new LTC/DesktopAssistant/Phase2/Resources/DesktopCapturePreview.prefab";
        const string PhaseTwoScenePath = "Assets/new LTC/DesktopAssistant/Phase2/Scenes/DesktopCapturePhase2Demo.unity";
        const string PhaseThreeScenePath = "Assets/new LTC/DesktopAssistant/Phase3/Scenes/VisionAnalysisPhase3Demo.unity";
        const string VisionSettingsPath = "Assets/new LTC/DesktopAssistant/Phase3/Config/VisionAnalysisSettings.asset";
        const string PhaseFourScenePath = "Assets/new LTC/DesktopAssistant/Phase4/Scenes/ProactiveReminderPhase4Demo.unity";
        const string ReminderSettingsPath = "Assets/new LTC/DesktopAssistant/Phase4/Config/ProactiveReminderSettings.asset";
        const string PhaseFiveScenePath = "Assets/new LTC/DesktopAssistant/Phase5/Scenes/DesktopOverlayPhase5Demo.unity";
        const string PhaseSixScenePath = "Assets/new LTC/DesktopAssistant/Phase6/Scenes/DesktopAssistantFinal.unity";
        const string OverlaySettingsPath = "Assets/new LTC/DesktopAssistant/Phase5/Config/DesktopOverlaySettings.asset";

        [Test]
        public void PhaseOneAssetsExist()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), "找不到助理 Prefab。");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "找不到獨立 Demo 場景。");
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(SettingsPath), "找不到助理設定檔。");
        }

        [Test]
        public void PrefabContainsRequiredModules()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            string[] componentTypes = prefab.GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .Select(component => component.GetType().FullName)
                .ToArray();

            CollectionAssert.Contains(componentTypes, "LTC.DesktopAssistant.DesktopAssistantController");
            CollectionAssert.Contains(componentTypes, "LTC.DesktopAssistant.AssistantCharacterView");
            CollectionAssert.Contains(componentTypes, "LTC.DesktopAssistant.AssistantDragHandler");
            CollectionAssert.Contains(componentTypes, "LTC.DesktopAssistant.AssistantStateMenu");
            Assert.IsNotNull(prefab.GetComponentInChildren<Canvas>(true));
            Assert.IsNotNull(prefab.GetComponentInChildren<Button>(true));
            CollectionAssert.DoesNotContain(componentTypes, "TMPro.TMP_InputField",
                "精簡介面不應顯示聊天輸入框。");
            CollectionAssert.DoesNotContain(componentTypes, "TMPro.TMP_Dropdown",
                "更換動物應使用按鍵而非下拉選單。");
            CollectionAssert.DoesNotContain(componentTypes, "LTC.DesktopAssistant.AssistantPetSelector",
                "目前版本不應顯示或掛載動物選擇功能。");

            Transform character = prefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "CharacterImage");
            Assert.IsNotNull(character, "找不到可點擊的動物圖片。");
            Assert.IsNotNull(character.GetComponent<Button>(), "動物圖片必須可以點擊開啟功能選單。");

            Transform stateEffect = prefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "StateEffect");
            Assert.IsTrue(stateEffect == null || !stateEffect.gameObject.activeSelf,
                "正式遊戲畫面不應顯示狀態符號圖案。");
        }

        [Test]
        public void DemoSceneIsNotAddedToBuildSettings()
        {
            bool isIncluded = EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath);
            Assert.IsFalse(isIncluded, "第一階段 Demo 不應改變既有 Build Settings。");
        }

        [Test]
        public void RuntimeBootstrapSupportsEveryGameScene()
        {
            System.Type bootstrap = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("LTC.DesktopAssistant.DesktopAssistantRuntimeBootstrap"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(bootstrap, "找不到全域自動載入器。");
            System.Reflection.MethodInfo supportsScene = bootstrap.GetMethod("SupportsScene");
            Assert.IsNotNull(supportsScene);

            string[] games =
            {
                "js", "mb", "mb2", "gopher", "CardsGame", "PipeGame",
                "SupermarketGame", "TextPuzzleGame"
            };
            foreach (string game in games)
                Assert.IsTrue((bool)supportsScene.Invoke(null, new object[] { game }), game + " 未啟用助理。");

            Assert.IsFalse((bool)supportsScene.Invoke(null, new object[] { "GameScene" }));
            Assert.IsFalse((bool)supportsScene.Invoke(null, new object[] { "shop" }));
        }

        [Test]
        public void PhaseTwoCaptureAssetsExistAndRemainOutsideBuildSettings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhaseTwoPrefabPath);
            Assert.IsNotNull(prefab, "找不到第二階段擷取預覽 Prefab。");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseTwoScenePath),
                "找不到第二階段擷取 Demo 場景。");
            string[] componentTypes = prefab.GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .Select(component => component.GetType().FullName)
                .ToArray();
            CollectionAssert.Contains(componentTypes,
                "LTC.DesktopAssistant.Capture.DesktopCaptureController");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(scene => scene.path == PhaseTwoScenePath),
                "第二階段 Demo 不應改變既有 Build Settings。");
        }

        [Test]
        public void WindowsCaptureServiceAlwaysListsEntireDesktop()
        {
            System.Type serviceType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "LTC.DesktopAssistant.Capture.Windows.WindowsGdiScreenCaptureService"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(serviceType, "找不到 Windows 擷取服務。");
            object service = System.Activator.CreateInstance(serviceType);
            object targets = serviceType.GetMethod("GetAvailableTargets").Invoke(service, null);
            int count = (int)targets.GetType().GetProperty("Count").GetValue(targets);
            Assert.GreaterOrEqual(count, 1, "至少必須包含整個桌面來源。");
        }

        [Test]
        public void FrameChangeDetectorSkipsIdenticalFrames()
        {
            System.Reflection.Assembly assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(value => value.GetType(
                    "LTC.DesktopAssistant.Capture.FrameChangeDetector") != null);
            Assert.IsNotNull(assembly);
            System.Type detectorType = assembly.GetType(
                "LTC.DesktopAssistant.Capture.FrameChangeDetector");
            System.Type frameType = assembly.GetType(
                "LTC.DesktopAssistant.Capture.ScreenCaptureFrame");
            object detector = System.Activator.CreateInstance(detectorType);
            byte[] pixels = new byte[16 * 9 * 4];
            for (int index = 0; index < pixels.Length; index += 4)
            {
                pixels[index] = 40;
                pixels[index + 1] = 80;
                pixels[index + 2] = 120;
                pixels[index + 3] = 255;
            }
            object frame = System.Activator.CreateInstance(frameType,
                new object[] { 16, 9, pixels, "Test" });
            System.Reflection.MethodInfo hasChanged = detectorType.GetMethod("HasChanged");
            Assert.IsTrue((bool)hasChanged.Invoke(detector, new[] { frame, (object)0.025f }));
            Assert.IsFalse((bool)hasChanged.Invoke(detector, new[] { frame, (object)0.025f }),
                "完全相同的第二張畫面應被略過。");
        }

        [Test]
        public void PhaseThreeAssetsExistAndRemainOutsideBuildSettings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseThreeScenePath),
                "找不到第三階段分析 Demo 場景。");
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(VisionSettingsPath),
                "找不到視覺分析設定檔。");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(scene => scene.path == PhaseThreeScenePath),
                "第三階段 Demo 不應改變既有 Build Settings。");
        }

        [Test]
        public void BackendAnalysisServiceContainsNoApiKeyOrSecretField()
        {
            System.Type serviceType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "LTC.DesktopAssistant.AI.BackendVisionAnalysisService"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(serviceType);
            string[] forbidden = { "apikey", "api_key", "secret", "openai" };
            foreach (System.Reflection.FieldInfo field in serviceType.GetFields(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Static |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                string normalized = field.Name.Replace("_", string.Empty).ToLowerInvariant();
                Assert.IsFalse(forbidden.Any(value => normalized.Contains(value.Replace("_", string.Empty))),
                    "Unity 後端服務不應包含秘密欄位：" + field.Name);
            }
        }

        [Test]
        public void PhaseFourAssetsExistAndRemainOutsideBuildSettings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseFourScenePath),
                "找不到第四階段主動提醒 Demo 場景。");
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(ReminderSettingsPath),
                "找不到主動提醒設定檔。");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(scene => scene.path == PhaseFourScenePath),
                "第四階段 Demo 不應改變既有 Build Settings。");
        }

        [Test]
        public void ReminderCooldownBlocksGlobalAndDuplicateSpam()
        {
            System.Type registryType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "LTC.DesktopAssistant.Reminders.ReminderCooldownRegistry"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(registryType, "找不到提醒冷卻模組。");
            object registry = System.Activator.CreateInstance(registryType);
            System.Reflection.MethodInfo canShow = registryType.GetMethod("CanShow");
            System.Reflection.MethodInfo markShown = registryType.GetMethod("MarkShown");
            object[] firstArgs = { "event:a", 100f, 10f, 60f, 0f };
            Assert.IsTrue((bool)canShow.Invoke(registry, firstArgs));
            markShown.Invoke(registry, new object[] { "event:a", 100f });
            object[] globalArgs = { "event:b", 105f, 10f, 60f, 0f };
            Assert.IsFalse((bool)canShow.Invoke(registry, globalArgs), "全域冷卻應阻擋其他提醒。");
            object[] duplicateArgs = { "event:a", 120f, 10f, 60f, 0f };
            Assert.IsFalse((bool)canShow.Invoke(registry, duplicateArgs), "相同提醒冷卻應阻擋重複事件。");
            object[] elapsedArgs = { "event:a", 161f, 10f, 60f, 0f };
            Assert.IsTrue((bool)canShow.Invoke(registry, elapsedArgs), "冷卻結束後應可再次提醒。");
        }

        [Test]
        public void PhaseFiveAndSixAssetsExistOutsideGameBuildSettings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseFiveScenePath),
                "找不到第五階段透明桌面場景。");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseSixScenePath),
                "找不到第六階段最終場景。");
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(OverlaySettingsPath),
                "找不到 Windows 覆蓋設定檔。");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(scene => scene.path == PhaseFiveScenePath),
                "第五階段場景不應加入既有遊戲 Build Settings。");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(scene => scene.path == PhaseSixScenePath),
                "第六階段場景不應加入既有遊戲 Build Settings。");
        }

        [Test]
        public void FinalDesktopModulesCompileAndAreDiscoverable()
        {
            string[] requiredTypes =
            {
                "LTC.DesktopAssistant.Windows.WindowsDesktopOverlayController",
                "LTC.DesktopAssistant.Windows.DesktopAssistantSettingsPanel",
                "LTC.DesktopAssistant.Windows.RuntimeHealthMonitor",
                "LTC.DesktopAssistant.Windows.AssistantReminderBridge"
            };
            foreach (string requiredType in requiredTypes)
            {
                System.Type found = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(requiredType))
                    .FirstOrDefault(type => type != null);
                Assert.IsNotNull(found, "找不到最終桌面模組：" + requiredType);
            }
        }

        [Test]
        public void AssistantPetNamesComeFromCurrentPetCatalog()
        {
            System.Type preferencesType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("LTC.DesktopAssistant.AssistantPreferences"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(preferencesType, "找不到助理寵物偏好設定。");
            System.Reflection.MethodInfo getName = preferencesType.GetMethod("GetPetDisplayName");
            Assert.IsNotNull(getName);
            Assert.AreEqual("兔子", getName.Invoke(null, new object[] { "rabbit", "寵物" }));
            Assert.AreEqual("小貓", getName.Invoke(null, new object[] { "cat", "寵物" }));
            Assert.AreEqual("小龍", getName.Invoke(null, new object[] { "dragon", "寵物" }));
            Assert.AreEqual("寵物", getName.Invoke(null, new object[] { "missing", "寵物" }));
        }
    }
}
