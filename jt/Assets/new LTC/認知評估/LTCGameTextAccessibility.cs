using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Raises small in-game text to a readable landscape phone/tablet size.</summary>
public sealed class LTCGameTextAccessibility : MonoBehaviour
{
    static readonly HashSet<string> GameScenes = new HashSet<string>
    {
        "js", "mb", "mb2", "gopher", "CardsGame", "PipeGame", "SupermarketGame", "TextPuzzleGame"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameScenes.Contains(scene.name) || FindFirstObjectByType<LTCGameTextAccessibility>() != null) return;
        new GameObject("Readable Game Text").AddComponent<LTCGameTextAccessibility>();
    }

    IEnumerator Start()
    {
        // Other game controllers may finish building their text in Start.
        yield return null;
        EnlargeSmallText();
        yield return new WaitForSecondsRealtime(.5f);
        EnlargeSmallText();
    }

    void EnlargeSmallText()
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null || text.fontSize >= 30f || text.fontSize < 14f) continue;
            if (!text.gameObject.scene.IsValid() || text.gameObject.scene != gameObject.scene) continue;
            text.fontSize = 38f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 30f;
            text.fontSizeMax = 38f;
            text.enableWordWrapping = true;
        }
    }
}
