using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class LTCLevelSelection
{
    private const string ProgressPrefix = "LTC.LevelCleared.";
    private static Sprite padlock;
    private static Sprite clearStar;

    public static bool CanPlay<T>(string catalog, IReadOnlyList<T> levels, int index) where T : ScriptableObject
    {
        return index >= 0 && index < levels.Count &&
            (index == 0 || LTCGameSettings.UnlockAllLevels ||
             IsCleared(catalog, levels[index]) || IsCleared(catalog, levels[index - 1]));
    }

    public static bool IsCleared(string catalog, ScriptableObject level)
    {
        return level != null && PlayerPrefs.GetInt(ProgressKey(catalog, level), 0) == 1;
    }

    public static void MarkCleared(string catalog, ScriptableObject level)
    {
        if (level == null || IsCleared(catalog, level)) return;
        PlayerPrefs.SetInt(ProgressKey(catalog, level), 1);
        PlayerPrefs.Save();
    }

    private static string ProgressKey(string catalog, ScriptableObject level)
    {
        return ProgressPrefix + catalog + "." + level.name;
    }

    public static void UpdateTile(Button button, TMP_Text number, TMP_Text details, bool locked, bool cleared)
    {
        button.interactable = !locked;
        number.enabled = !locked;
        if (details != null) details.enabled = !locked;

        if (padlock == null) padlock = Resources.Load<Sprite>("LTCLevelPadlock");
        if (clearStar == null) clearStar = Resources.Load<Sprite>("LTCLevelClearStar");
        SetBadge(button.transform, "金色鎖頭", padlock, locked, Color.white,
            new Vector2(.29f, .16f), new Vector2(.71f, .84f));
        SetBadge(button.transform, "通關星星", clearStar, cleared, new Color32(207, 127, 18, 255),
            new Vector2(.72f, .64f), new Vector2(.87f, .87f));
    }

    private static void SetBadge(Transform parent, string name, Sprite sprite, bool visible, Color color,
        Vector2 min, Vector2 max)
    {
        Transform existing = parent.Find(name);
        if (existing == null && !visible) return;

        if (existing != null)
        {
            existing.gameObject.SetActive(visible);
            return;
        }

        var badge = new GameObject(name, typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(parent, false);
        var rect = (RectTransform)badge.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = badge.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }
}
