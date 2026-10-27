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
    public sealed class CardSkillPlayModeTests
    {
        private MonoBehaviour game;
        private ScriptableObject level;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object target, string name)
        {
            var type = target.GetType();
            var field = type.GetField(name, Flags);
            return field != null ? field.GetValue(target) : type.GetProperty(name, Flags).GetValue(target);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Flags).Invoke(target, arguments);
        private void Charge(int amount = 10) { for (int i = 0; i < amount; i++) Call(game, "GainSkillEnergy"); }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("CardsGame");
            yield return null;
            yield return null;
            game = Resources.FindObjectsOfTypeAll<MonoBehaviour>().First(x => x != null &&
                x.GetType().Name == "GameController" && x.gameObject.scene.IsValid());
            var selection = Get(game, "levelSelection");
            var catalog = Get(selection, "catalog");
            var levels = (IList)Get(catalog, "levels");
            level = UnityEngine.Object.Instantiate((ScriptableObject)levels[0]);
            Set(level, "cardCount", 4);
            Set(level, "columns", 2);
            Set(level, "previewSeconds", .5f);
            Set(level, "playerHealth", 100);
            Set(level, "enemyHealth", 1000);
            Set(level, "enemyAttack", 0);
            ((GameObject)Get(selection, "panel")).SetActive(false);
            Call(game, "LoadLevel", level);
            Call(game, "CloseTeachingUI");
            yield return new WaitForSeconds(1.1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (game != null) Call(game, "CancelCurrentAssessment");
            if (level != null) UnityEngine.Object.Destroy(level);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PairAndRoundEachAwardOneEnergy()
        {
            Assert.AreEqual(0, Get(game, "SkillEnergy"));
            Assert.IsFalse((bool)Call(game, "TryUseSkill"));
            var cards = Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(x => x != null &&
                x.GetType().Name == "Card" && x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy).ToArray();
            var pair = cards.GroupBy(x => (int)Call(x, "GetCardID")).First(x => x.Count() >= 2).Take(2).ToArray();
            Call(pair[0], "FlipCard"); Call(game, "CardClicked", pair[0]);
            Call(pair[1], "FlipCard"); Call(game, "CardClicked", pair[1]);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, Get(game, "SkillEnergy"), "A matched pair awards one, not two, energy.");
            game.StartCoroutine((IEnumerator)Call(game, "ResolveTurn"));
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(2, Get(game, "SkillEnergy"), "A complete turn awards one additional energy.");
        }

        [UnityTest]
        public IEnumerator SkillConsumesTenDealsPercentageDamageAndCapsHealing()
        {
            Set(game, "playerHealth", 40);
            Charge(20);
            Assert.AreEqual(10, Get(game, "SkillEnergy"));
            Assert.IsTrue((bool)Get(game, "CanUseSkill"));
            Assert.IsTrue((bool)Call(game, "TryUseSkill"));
            Assert.IsFalse((bool)Call(game, "TryUseSkill"), "Double clicks cannot spend or cast twice.");
            Assert.IsFalse((bool)Call(game, "CanClickCards"));
            yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(750, Get(game, "enemyHealth"));
            Assert.AreEqual(70, Get(game, "playerHealth"));
            Assert.AreEqual(0, Get(game, "SkillEnergy"));
            Assert.IsFalse((bool)Get(game, "IsSkillPlaying"));
            Assert.IsTrue((bool)Call(game, "CanClickCards"));
            Set(game, "playerHealth", 95);
            Charge();
            Assert.IsTrue((bool)Call(game, "TryUseSkill"));
            yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(100, Get(game, "playerHealth"));
            Assert.AreEqual(500, Get(game, "enemyHealth"));
        }

        [UnityTest]
        public IEnumerator HalfPairPauseAndCancellationBlockSkillSafely()
        {
            Charge();
            var card = Resources.FindObjectsOfTypeAll<MonoBehaviour>().First(x => x != null &&
                x.GetType().Name == "Card" && x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy);
            Call(card, "FlipCard"); Call(game, "CardClicked", card);
            Assert.IsFalse((bool)Call(game, "TryUseSkill"));
            Assert.AreEqual(10, Get(game, "SkillEnergy"));
            ((IList)Get(game, "revealedCards")).Clear();
            Assert.IsTrue((bool)Call(game, "TryUseSkill"));
            yield return new WaitForSeconds(.2f);
            var presentation = game.GetComponents<MonoBehaviour>().First(x => x.GetType().Name == "CardSkillPresentation");
            Assert.IsTrue((bool)Get(presentation, "IsEffectVisible"));
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(1000, Get(game, "enemyHealth"));
            Assert.IsTrue((bool)Get(game, "IsSkillPlaying"));
            Time.timeScale = 1f;
            Call(Get(game, "levelSelection"), "Open");
            Assert.IsFalse((bool)Get(presentation, "IsEffectVisible"));
            Assert.IsFalse((bool)Get(game, "IsSkillPlaying"));
            yield return new WaitForSeconds(2.8f);
            Assert.AreEqual(1000, Get(game, "enemyHealth"), "Cancelled casts must not apply delayed damage.");
            ((GameObject)Get(Get(game, "levelSelection"), "panel")).SetActive(false);
            Call(game, "LoadLevel", level);
            Assert.AreEqual(0, Get(game, "SkillEnergy"));
        }

        [UnityTest]
        public IEnumerator LethalSkillCompletesBattleAndClearsOverlay()
        {
            Set(game, "enemyHealth", 200);
            Charge();
            Assert.IsTrue((bool)Call(game, "TryUseSkill"));
            yield return new WaitForSeconds(4f);
            Assert.AreEqual(0, Get(game, "enemyHealth"));
            Assert.IsTrue((bool)Get(game, "IsBattleEnded"));
            Assert.IsTrue(((GameObject)Get(Get(game, "levelSelection"), "completionPanel")).activeSelf);
            var presentation = game.GetComponents<MonoBehaviour>().First(x => x.GetType().Name == "CardSkillPresentation");
            Assert.IsFalse((bool)Get(presentation, "IsEffectVisible"));
        }
    }
}
