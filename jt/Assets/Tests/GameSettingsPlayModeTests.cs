using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LTC.Tests
{
    public sealed class GameSettingsPlayModeTests
    {
        private const string Key = "LTC_GameTutorialsEnabled";
        private bool hadPreference;
        private int previousPreference;
        private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b != null && b.GetType().Name == name);
        private static object Field(object obj, string name) => obj.GetType().GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        private static object Property(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
        private static void SetPreference(bool enabled) => TypeOf("LTCGameSettings")
            .GetProperty("TutorialsEnabled").SetValue(null, enabled);

        [SetUp]
        public void SavePreference()
        {
            hadPreference = PlayerPrefs.HasKey(Key);
            previousPreference = PlayerPrefs.GetInt(Key);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(Key, previousPreference);
            else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            foreach (string type in new[] { "GameController", "PipeManager" })
                TypeOf(type).GetField("tutorialShownThisSession", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, false);
        }

        [UnityTest]
        public IEnumerator SettingsOpensSavesAcrossSceneReloadAndReturns()
        {
            PlayerPrefs.DeleteKey(Key);
            Assert.IsTrue((bool)TypeOf("LTCGameSettings").GetProperty("TutorialsEnabled").GetValue(null),
                "Tutorials should be enabled for players without a saved preference.");
            SceneManager.LoadScene("GameScene"); yield return null; yield return null;
            var catalog = Find("CognitiveGameCatalogController");
            var root = catalog.transform.Find("Cognitive Catalog Canvas");
            root.Find("首次引導").gameObject.SetActive(false);
            root.Find("每日登入彈窗").gameObject.SetActive(false);
            root.Find("底部導覽/我的").GetComponent<Button>().onClick.Invoke();
            var entry = root.Find("我的頁/個人資料內容/設定").GetComponent<Button>();
            Assert.IsTrue(entry.interactable);
            entry.onClick.Invoke();
            var page = root.Find("設定頁");
            Assert.IsTrue(page.gameObject.activeInHierarchy);
            Assert.AreEqual(root.childCount - 1, page.GetSiblingIndex());
            var toggle = page.Find("設定內容/遊戲教學開關").GetComponent<Toggle>();
            Assert.IsTrue(toggle.isOn);
            toggle.isOn = false;
            Assert.AreEqual(0, PlayerPrefs.GetInt(Key));
            page.Find("返回我的頁").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(page.gameObject.activeSelf);
            Assert.IsTrue(root.Find("我的頁").gameObject.activeInHierarchy);
            SceneManager.LoadScene("GameScene"); yield return null; yield return null;
            root = Find("CognitiveGameCatalogController").transform.Find("Cognitive Catalog Canvas");
            root.Find("我的頁/個人資料內容/設定").GetComponent<Button>().onClick.Invoke();
            toggle = root.Find("設定頁/設定內容/遊戲教學開關").GetComponent<Toggle>();
            Assert.IsFalse(toggle.isOn, "Reloading must retain the saved setting.");
            toggle.isOn = true;
            Assert.AreEqual(1, PlayerPrefs.GetInt(Key));
        }

        [UnityTest]
        public IEnumerator DisabledTutorialsStartAllGames() { yield return CheckGames(false); }

        [UnityTest]
        public IEnumerator EnabledTutorialsStillGateAllGames() { yield return CheckGames(true); }

        private IEnumerator CheckGames(bool enabled)
        {
            SetPreference(enabled);
            SceneManager.LoadScene("CardsGame"); yield return null; yield return null;
            var game = Find("GameController");
            game.GetType().GetField("tutorialShownThisSession", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            Call(Field(game, "levelSelection"), "PlaySelected"); yield return null;
            Assert.AreEqual(enabled, Field(game, "tutorialShowing"), "Card tutorial");
            Assert.AreEqual(!enabled, Field(game, "gameFlowStarted"), "Cards must start when tutorial is skipped.");
            Call(game, "CancelCurrentAssessment");

            SceneManager.LoadScene("PipeGame"); yield return null; yield return null;
            game = Find("PipeManager");
            game.GetType().GetField("tutorialShownThisSession", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            Call(Field(game, "levelSelection"), "PlaySelected"); yield return null;
            Assert.AreEqual(enabled, Property(game, "IsTutorialActive"), "Pipe tutorial");
            if (!enabled) Assert.IsNotEmpty((string)Field(game, "assessmentSessionId"), "Pipe game must start assessment when tutorial is skipped.");
            Call(game, "CancelCurrentAssessment");

            SceneManager.LoadScene("SupermarketGame"); yield return null; yield return null;
            game = Find("SupermarketGame");
            Call(Field(game, "levelSelection"), "PlaySelected"); yield return null;
            Assert.AreEqual(enabled, Property(game, "IsTutorialOpen"), "Shopping tutorial");
            Assert.AreEqual(!enabled, Property(game, "IsGameStarted"), "Shopping must start when tutorial is skipped.");
            Call(game, "CancelCurrentAssessment");
        }
    }
}
