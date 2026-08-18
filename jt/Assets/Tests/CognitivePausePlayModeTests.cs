using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LTC.Tests
{
    public sealed class CognitivePausePlayModeTests
    {
        [UnityTest]
        public IEnumerator StroopPause_RestartsSameConditionAtPreviousBoundary()
        {
            yield return VerifyPause("js", "ColorMatchStroopGameManager");
        }

        [UnityTest]
        public IEnumerator NumberOrderPause_DiscardsPartialRoundAndKeepsDifficulty()
        {
            yield return VerifyPause("mb", "NumberOrderPoolGameManager");
        }

        [UnityTest]
        public IEnumerator NumberSumPause_DiscardsPartialRoundAndKeepsDifficulty()
        {
            yield return VerifyPause("mb2", "NumberSumGameManager");
        }

        private static IEnumerator VerifyPause(string sceneName, string managerTypeName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(.15f);

            MonoBehaviour menu = FindBehaviour("CognitiveGamePauseMenu");
            Assert.That(menu, Is.Not.Null, sceneName + " 缺少暫停控制器");
            Assert.That(GetField<Button>(menu, "pauseButton"), Is.Not.Null);
            Assert.That(GetField<Button>(menu, "resumeButton"), Is.Not.Null);
            Assert.That(GetField<Button>(menu, "homeButton"), Is.Not.Null);
            GameObject pausePanel = GetField<GameObject>(menu, "pausePanel");
            Assert.That(pausePanel, Is.Not.Null);
            Assert.That(pausePanel.activeSelf, Is.False);

            MonoBehaviour manager = FindBehaviour(managerTypeName);
            Assert.That(manager, Is.Not.Null, sceneName + " 找不到 " + managerTypeName);

            int checkpointTrials = GetField<int>(manager, "pauseCheckpointTrialCount");
            float checkpointTime = GetField<float>(manager, "pauseCheckpointTimeLeft");
            string sessionId = GetField<string>(manager, "assessmentSessionId");

            int? roundBefore = TryGetField<int>(manager, "round");
            bool? conflictBefore = TryGetField<bool>(manager, "currentHighConflict");
            bool? matchBefore = TryGetField<bool>(manager, "currentAnswerIsCorrect");

            if (managerTypeName != "ColorMatchStroopGameManager")
            {
                Button partialButton = FirstActiveNumberButton(manager);
                Assert.That(partialButton, Is.Not.Null, "找不到可用的數字按鈕");
                partialButton.onClick.Invoke();
                Assert.That(GetTrialCheckpoint(sessionId), Is.GreaterThan(checkpointTrials),
                    "測試未產生可回溯的半題事件");
            }

            Invoke(menu, "OpenPauseMenu");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(pausePanel.activeSelf, Is.True);
            float timeWhilePaused = GetField<float>(manager, "timeLeft");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(GetField<float>(manager, "timeLeft"), Is.EqualTo(timeWhilePaused).Within(.01f),
                "暫停期間計時器仍在前進");

            Invoke(menu, "ResumeGame");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(pausePanel.activeSelf, Is.False);
            Assert.That(GetField<float>(manager, "timeLeft"), Is.EqualTo(checkpointTime).Within(.05f),
                "返回遊戲後沒有回復到上一題完成時的剩餘時間");
            Assert.That(GetTrialCheckpoint(sessionId), Is.EqualTo(checkpointTrials),
                "半題事件沒有被移除");

            if (roundBefore.HasValue)
                Assert.That(TryGetField<int>(manager, "round"), Is.EqualTo(roundBefore), "重新出題後關卡難度改變");
            if (conflictBefore.HasValue)
                Assert.That(TryGetField<bool>(manager, "currentHighConflict"), Is.EqualTo(conflictBefore), "干擾難度改變");
            if (matchBefore.HasValue)
                Assert.That(TryGetField<bool>(manager, "currentAnswerIsCorrect"), Is.EqualTo(matchBefore), "題型條件改變");

            Invoke(manager, "CancelCurrentAssessment");
            Time.timeScale = 1f;
        }

        private static MonoBehaviour FindBehaviour(string typeName)
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
            return null;
        }

        private static Button FirstActiveNumberButton(MonoBehaviour manager)
        {
            object list = GetField<object>(manager, "numberButtons");
            if (!(list is IEnumerable enumerable)) return null;
            foreach (object item in enumerable)
                if (item is Button button && button.gameObject.activeInHierarchy && button.interactable) return button;
            return null;
        }

        private static int GetTrialCheckpoint(string sessionId)
        {
            Type service = FindType("LTCCognitiveAssessment.CognitiveAssessmentService");
            MethodInfo method = service.GetMethod("GetTrialCheckpoint", BindingFlags.Public | BindingFlags.Static);
            return (int)method.Invoke(null, new object[] { sessionId });
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            Assert.Fail("找不到類型 " + fullName);
            return null;
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, target.GetType().Name + " 找不到方法 " + methodName);
            method.Invoke(target, null);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " 找不到欄位 " + name);
            return (T)field.GetValue(target);
        }

        private static T? TryGetField<T>(object target, string name) where T : struct
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field == null ? null : (T?)field.GetValue(target);
        }
    }
}
