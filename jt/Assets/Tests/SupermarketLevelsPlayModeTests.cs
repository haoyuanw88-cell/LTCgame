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
    public sealed class SupermarketLevelsPlayModeTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static object F(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        private static MonoBehaviour Game() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x => x.GetType().Name == "SupermarketGame");

        [UnityTest]
        public IEnumerator CopiedArrowsNavigateEveryAreaAndReturnToCheckout()
        {
            SceneManager.LoadScene("SupermarketGame"); yield return null; yield return null;
            var game = Game();
            Call(F(game, "levelSelection"), "PlaySelected"); Call(game, "StartGame");
            var camera = (Camera)F(game, "gameCamera");
            var arrows = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(x => x.sprite != null && (x.sprite.name == "tile_0185_0" || x.sprite.name == "tile_0186_0")).ToArray();
            Assert.Greater(arrows.Length, 2, "Exercise copied-area arrows.");
            foreach (var arrow in arrows)
                Assert.IsTrue(arrow.GetComponents<MonoBehaviour>().Any(x => x != null && x.GetType().Name == "CameraArrow"), arrow.name);
            int areas = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(x => x.name.StartsWith("background"));
            for (int step = 0; step < areas - 1; step++)
            {
                float before = camera.transform.position.x;
                var arrow = arrows.Where(x => x.sprite.name == "tile_0185_0")
                    .OrderBy(x => Mathf.Abs(camera.WorldToViewportPoint(x.bounds.center).x - .1f)).First();
                Vector3 viewport = camera.WorldToViewportPoint(arrow.bounds.center);
                Assert.That(viewport.x, Is.InRange(0f, 1f), "Left arrow must be inside the camera.");
                arrow.gameObject.SendMessage("OnMouseDown", SendMessageOptions.RequireReceiver);
                Assert.Less(camera.transform.position.x, before, arrow.name);
            }
            float leftmost = camera.transform.position.x;
            Call(game, "MoveCamera", true); Assert.AreEqual(leftmost, camera.transform.position.x);
            for (int step = 0; step < areas - 1; step++)
            {
                float before = camera.transform.position.x;
                var arrow = arrows.Where(x => x.sprite.name == "tile_0186_0")
                    .OrderBy(x => Mathf.Abs(camera.WorldToViewportPoint(x.bounds.center).x - .9f)).First();
                Vector3 viewport = camera.WorldToViewportPoint(arrow.bounds.center);
                Assert.That(viewport.x, Is.InRange(0f, 1f), "Right arrow must be inside the camera.");
                arrow.gameObject.SendMessage("OnMouseDown", SendMessageOptions.RequireReceiver);
                Assert.Greater(camera.transform.position.x, before, arrow.name);
            }
            Assert.That(camera.transform.position.x, Is.EqualTo((float)F(game, "checkoutCameraX")).Within(.01f));
            Call(game, "CancelCurrentAssessment");
        }

        [UnityTest]
        public IEnumerator SelectionPurchaseCompletionAndReplayReset()
        {
            SceneManager.LoadScene("SupermarketGame"); yield return null; yield return null;
            var game = Game(); var selection = F(game, "levelSelection");
            Assert.IsTrue((bool)P(selection, "IsOpen"));
            Assert.IsFalse((bool)P(game, "IsGameStarted"));
            Call(game, "StartGame");
            Assert.IsFalse((bool)P(game, "IsGameStarted"));
            Call(selection, "PlaySelected");
            Assert.IsFalse((bool)P(selection, "IsOpen"));
            Assert.IsTrue((bool)P(game, "IsTutorialOpen"));
            Assert.IsFalse((bool)P(game, "IsGameStarted"));
            Call(game, "StartGame");
            Assert.IsFalse((bool)P(game, "IsTutorialOpen"));
            Assert.IsTrue((bool)P(game, "IsGameStarted"));
            var products = (IList)F(game, "requiredProducts");
            var level = P(game, "CurrentLevel");
            var originals = (IList)F(level, "products");
            Assert.AreNotSame(products[0], originals[0]);
            var all = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var wrong = all.First(x => (bool)Call(game, "IsPurchasable", x) && !products.Cast<object>().Any(p => (Sprite)F(p, "productImage") == x.sprite));
            int remaining = (int)F(products[0], "remaining");
            Call(game, "Buy", wrong);
            Assert.AreEqual(remaining, F(products[0], "remaining"));
            foreach (var product in products)
            {
                var sprite = (Sprite)F(product, "productImage");
                var target = all.First(x => x.sprite == sprite && (bool)Call(game, "IsPurchasable", x));
                for (int i = 0; i < (int)F(product, "quantity"); i++) Call(game, "Buy", target);
            }
            Assert.IsTrue((bool)P(game, "IsCleared"));
            Assert.IsTrue(((GameObject)F(selection, "completionPanel")).activeSelf);
            Call(selection, "Open"); Call(selection, "PlaySelected");
            Assert.IsFalse((bool)P(game, "IsCleared"));
            Assert.IsFalse((bool)P(game, "IsTutorialOpen"));
            Assert.IsTrue((bool)P(game, "IsGameStarted"), "Replay should start without another tutorial.");
            var reset = (IList)F(game, "requiredProducts");
            Assert.AreEqual(F(reset[0], "quantity"), F(reset[0], "remaining"));
            Assert.IsFalse(((GameObject)F(selection, "completionPanel")).activeSelf);
            Call(game, "CancelCurrentAssessment");
        }

        [UnityTest]
        public IEnumerator CatalogPaginationAndNextLevel()
        {
            SceneManager.LoadScene("SupermarketGame"); yield return null; yield return null;
            var game = Game(); var selection = F(game, "levelSelection");
            var catalog = F(selection, "catalog"); var original = F(catalog, "levels");
            var copy = (IList)Activator.CreateInstance(original.GetType());
            var template = ((IList)original)[0];
            var created = new System.Collections.Generic.List<ScriptableObject>();
            try
            {
                for (int i = 0; i < 10; i++) { var clone = UnityEngine.Object.Instantiate((ScriptableObject)template); created.Add(clone); copy.Add(clone); }
                catalog.GetType().GetField("levels").SetValue(catalog, copy);
                Call(selection, "Open"); Call(selection, "ChangePage", 1);
                Assert.IsFalse(((UnityEngine.UI.Button)F(selection, "nextButton")).interactable);
                var buttons = (UnityEngine.UI.Button[])F(selection, "levelButtons");
                Assert.IsTrue(buttons[1].gameObject.activeSelf); Assert.IsFalse(buttons[2].gameObject.activeSelf);
                Call(selection, "SelectLevel", 8); Call(selection, "PlaySelected");
                Assert.AreSame(copy[8], P(game, "CurrentLevel"));
                Call(game, "StartGame");
                var all = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                foreach (var product in (IList)F(game, "requiredProducts"))
                    for (int i = 0; i < (int)F(product, "quantity"); i++)
                        Call(game, "Buy", all.First(x => x.sprite == (Sprite)F(product, "productImage") && (bool)Call(game, "IsPurchasable", x)));
                Call(selection, "PlayNextLevel");
                Assert.AreSame(copy[9], P(game, "CurrentLevel"));
                Assert.IsFalse((bool)P(game, "IsTutorialOpen"));
                Assert.IsTrue((bool)P(game, "IsGameStarted"), "Next level should start without another tutorial.");
            }
            finally
            {
                catalog.GetType().GetField("levels").SetValue(catalog, original);
                Call(game, "CancelCurrentAssessment");
                foreach (var item in created) UnityEngine.Object.Destroy(item);
            }
        }
    }
}
