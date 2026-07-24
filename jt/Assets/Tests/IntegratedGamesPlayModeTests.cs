using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LTC.Tests
{
    public sealed class IntegratedGamesPlayModeTests
    {
        private readonly List<string> runtimeErrors = new();

        [SetUp]
        public void SetUp()
        {
            runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CaptureRuntimeError;
        }

        [UnityTest]
        public IEnumerator CardsGame_StartsWithoutErrors()
        {
            yield return LoadAndValidate("CardsGame", "GameController");
        }

        [UnityTest]
        public IEnumerator PipeGame_StartsWithoutErrors()
        {
            yield return LoadAndValidate("PipeGame", "PipeManager");
        }

        [UnityTest]
        public IEnumerator SupermarketGame_StartsWithoutErrors()
        {
            yield return LoadAndValidate("SupermarketGame", "SupermarketGame");
        }

        [UnityTest]
        public IEnumerator TextPuzzleGame_StartsWithoutErrors()
        {
            yield return LoadAndValidate("TextPuzzleGame", "SeniorTrueFalseQuiz");
        }

        private IEnumerator LoadAndValidate(string sceneName, string requiredController)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName),
                $"場景 {sceneName} 未正確載入。");

            var controllerFound = false;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include))
            {
                if (behaviour != null && behaviour.GetType().Name == requiredController)
                {
                    controllerFound = true;
                    break;
                }
            }

            Assert.That(controllerFound, Is.True,
                $"場景 {sceneName} 找不到主要控制程式 {requiredController}。");

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                AssertNoMissingScripts(root);
            }

            Assert.That(runtimeErrors, Is.Empty,
                $"場景 {sceneName} 啟動時發生錯誤：\n{string.Join("\n", runtimeErrors)}");
        }

        private static void AssertNoMissingScripts(GameObject gameObject)
        {
            foreach (var component in gameObject.GetComponents<Component>())
            {
                Assert.That(component, Is.Not.Null,
                    $"物件 {GetHierarchyPath(gameObject)} 含有遺失的 Script/Component。");
            }

            foreach (Transform child in gameObject.transform)
            {
                AssertNoMissingScripts(child.gameObject);
            }
        }

        private void CaptureRuntimeError(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                runtimeErrors.Add($"{type}: {message}");
            }
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            var path = gameObject.name;
            var parent = gameObject.transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
    }
}
