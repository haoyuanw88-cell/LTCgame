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
    }
}
