using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LTCChallengeUI
{
    public readonly Color Ink = new Color(.25f, .19f, .15f);
    public readonly Color Cream = new Color(1f, .975f, .91f);
    public readonly Color Sage = new Color(.83f, .91f, .69f);
    public readonly Color Rose = new Color(.96f, .75f, .74f);
    public readonly Color Blue = new Color(.77f, .89f, .91f);
    public LTCChallengeAssets Assets { get; }
    public LTCChallengeUI(LTCChallengeAssets assets) { Assets = assets; }

    public RectTransform Panel(Transform parent, string name, Color color, float x, float y, float xx, float yy, bool skinned = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        if (skinned) { image.sprite = Assets.panel; image.type = Image.Type.Sliced; }
        Place(go.GetComponent<RectTransform>(), x, y, xx, yy);
        return go.GetComponent<RectTransform>();
    }
    public TMP_Text Text(Transform parent, string name, string value, float size, float x, float y, float xx, float yy,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TMP_Text>();
        t.font = Assets.font; t.text = value; t.fontSize = size; t.color = Ink;
        t.alignment = alignment; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        Place(t.rectTransform, x, y, xx, yy);
        return t;
    }
    public Button Button(Transform parent, string name, string label, Color color, float x, float y, float xx, float yy, System.Action action)
    {
        var rt = Panel(parent, name, color, x, y, xx, yy);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        var colors = b.colors; colors.pressedColor = new Color(.78f, .82f, .73f); b.colors = colors;
        if (!string.IsNullOrEmpty(label)) Text(rt, "文字", label, 30, .04f, .06f, .96f, .94f);
        b.onClick.AddListener(() => action?.Invoke());
        return b;
    }
    public Image Sprite(Transform parent, string name, Sprite sprite, float x, float y, float xx, float yy)
    {
        var rt = Panel(parent, name, Color.white, x, y, xx, yy, false);
        var img = rt.GetComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        return img;
    }
    public static void Place(RectTransform rect, float x, float y, float xx, float yy)
    {
        rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(xx, yy); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
