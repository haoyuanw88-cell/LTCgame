using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace LTC.Tests
{
 public sealed class CardLevelsPlayModeTests
 {
  private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
  private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
  private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
  private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
  private static MonoBehaviour Game() => Resources.FindObjectsOfTypeAll<MonoBehaviour>().First(b => b != null && b.GetType().Name == "GameController" && b.gameObject.scene.IsValid());
  private static int Cards() => Resources.FindObjectsOfTypeAll<MonoBehaviour>().Count(b => b != null && b.GetType().Name == "Card" && b.gameObject.scene.IsValid() && b.gameObject.activeInHierarchy);

  [UnityTest]
  public IEnumerator SelectionLoadsExactCountAndResetsEnemyAfterDeath()
  {
   SceneManager.LoadScene("CardsGame"); yield return null; yield return null;
   var game = Game(); var selection = Field(game, "levelSelection");
   Assert.IsNotNull(selection);
   Assert.IsTrue((bool)selection.GetType().GetProperty("IsOpen").GetValue(selection));
   Assert.IsFalse((bool)game.GetType().GetField("tutorialShowing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game));
   game.GetType().GetField("tutorialShownThisSession", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
   var levels = (IList)Field(Field(selection, "catalog"), "levels");
   Assert.Greater(levels.Count, 0);
   Call(selection, "SelectLevel", 0); Call(selection, "PlaySelected"); yield return null;
   Assert.IsTrue((bool)game.GetType().GetField("tutorialShowing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game));
   Assert.AreEqual(0, Cards(), "The battle must not start behind the introduction.");
   Assert.IsFalse((bool)Call(game, "CanClickCards"));
   Call(game, "ShowNextTeachingPage");
   Assert.AreEqual(1, game.GetType().GetField("teachingPageIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game));
   Call(game, "ShowPreviousTeachingPage");
   Call(game, "CloseTeachingUI"); yield return null;
   Assert.AreEqual((int)Field(levels[0], "cardCount"), Cards());
   Assert.AreEqual(Field(levels[0], "enemyHealth"), Field(game, "enemyMaxHealth"));
   Assert.AreEqual(Field(levels[0], "enemyAttack"), Field(game, "enemyAttackPower"));
   var enemy = (Animator)Field(game, "enemyAnimator");
   Vector3 origin = enemy.transform.position; Quaternion rotation = enemy.transform.rotation;
   Call(game, "CancelCurrentAssessment");
   game.StartCoroutine((IEnumerator)Call(game, "HandleEnemyDeath"));
   yield return new WaitForSeconds(.95f);
   Assert.IsFalse(enemy.gameObject.activeSelf);
   Assert.That(Quaternion.Angle(rotation, enemy.transform.rotation), Is.EqualTo(90).Within(.1));
   Call(selection, "Open"); Call(selection, "PlaySelected"); yield return null;
   Assert.IsTrue(enemy.gameObject.activeSelf);
   Assert.That(Vector3.Distance(origin, enemy.transform.position), Is.LessThan(.001f));
   Assert.That(Quaternion.Angle(rotation, enemy.transform.rotation), Is.LessThan(.001f));
   Assert.AreEqual(1f, enemy.GetComponent<SpriteRenderer>().color.a);
  }
  [UnityTest]
  public IEnumerator CustomSpriteAndOddCountAreApplied()
  {
   SceneManager.LoadScene("CardsGame"); yield return null; yield return null;
   var game = Game(); var level = ScriptableObject.CreateInstance(TypeOf("CardLevelData"));
   var texture = new Texture2D(16, 32);
   var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(.5f, .5f));
   Set(level, "enemySprite", sprite); Set(level, "cardCount", 15); Set(level, "enemyHealth", 37); Set(level, "enemyAttack", 6);
   ((GameObject)Field(Field(game, "levelSelection"), "panel")).SetActive(false);
   Call(game, "LoadLevel", level); Call(game, "CloseTeachingUI"); yield return null;
   Assert.AreEqual(16, Cards());
   var enemy = (Animator)Field(game, "enemyAnimator");
   Assert.AreSame(sprite, enemy.GetComponent<SpriteRenderer>().sprite);
   Assert.IsFalse(enemy.enabled);
   Assert.AreEqual(37, Field(game, "enemyMaxHealth")); Assert.AreEqual(6, Field(game, "enemyAttackPower"));
   Call(game, "CancelCurrentAssessment");
   UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(level);
  }
 }
}
