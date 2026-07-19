using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CognitiveResultLayoutBaker
{
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    [MenuItem("Tools/LTC/整理三個遊戲結算畫面")]
    public static void BakeAll()
    {
        Bake<ColorMatchStroopGameManager>("Assets/new LTC/場景轉換/js.unity");
        Bake<NumberOrderPoolGameManager>("Assets/new LTC/場景轉換/mb.unity");
        Bake<NumberSumGameManager>("Assets/new LTC/場景轉換/mb2.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("三個遊戲的結算畫面已整理完成。");
    }

    static void Bake<T>(string scenePath) where T : MonoBehaviour
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        T manager = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (manager == null) throw new MissingReferenceException(scenePath + " 找不到遊戲管理器");

        GameObject panel;
        TMP_Text oldDetail;
        if (manager is ColorMatchStroopGameManager color) { panel = color.resultPanel; oldDetail = color.resultText; }
        else if (manager is NumberOrderPoolGameManager order) { panel = order.resultPanel; oldDetail = order.resultText; }
        else { var sum = manager as NumberSumGameManager; panel = sum.resultPanel; oldDetail = sum.resultText; }
        if (panel == null || oldDetail == null) throw new MissingReferenceException(scenePath + " 結算面板引用不完整");

        panel.transform.SetAsLastSibling();
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color32(250, 247, 239, 255);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_Text title = EnsureText(panel.transform, "結算標題", font, 42, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(.08f, .80f), new Vector2(.92f, .94f));
        TMP_Text summary = EnsureText(panel.transform, "結算摘要", font, 30, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(.08f, .68f), new Vector2(.92f, .80f));
        TMP_Text detail = ConfigureText(oldDetail, "結算詳細", font, 25, FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Vector2(.13f, .28f), new Vector2(.87f, .66f));
        TMP_Text note = EnsureText(panel.transform, "結算說明", font, 20, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(.08f, .14f), new Vector2(.92f, .27f));
        title.text = "本次測驗完成"; summary.text = "分數與獎勵"; detail.text = "詳細表現會顯示在這裡"; note.text = "本結果用於個人趨勢參考";

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform rt = buttons[i].GetComponent<RectTransform>();
            float width = Mathf.Min(.42f, .84f / Mathf.Max(1, buttons.Length));
            float center = .5f + (i - (buttons.Length - 1) * .5f) * width;
            rt.anchorMin = new Vector2(center - width * .45f, .025f);
            rt.anchorMax = new Vector2(center + width * .45f, .12f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.font = font; label.fontSize = 25; label.enableAutoSizing = true;
                label.fontSizeMin = 18; label.fontSizeMax = 28; label.raycastTarget = false;
                if (label.text == "Button" || label.text == "New Text") label.text = "返回主選單";
            }
        }

        if (manager is ColorMatchStroopGameManager c) { c.resultTitleText=title; c.resultSummaryText=summary; c.resultText=detail; c.resultNoteText=note; }
        else if (manager is NumberOrderPoolGameManager o) { o.resultTitleText=title; o.resultSummaryText=summary; o.resultText=detail; o.resultNoteText=note; }
        else { var s=manager as NumberSumGameManager; s.resultTitleText=title; s.resultSummaryText=summary; s.resultText=detail; s.resultNoteText=note; }

        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        Transform child = parent.Find(name);
        TMP_Text text;
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false); text = go.GetComponent<TextMeshProUGUI>();
        }
        else text = child.GetComponent<TMP_Text>();
        return ConfigureText(text, name, font, size, style, alignment, min, max);
    }

    static TMP_Text ConfigureText(TMP_Text text, string name, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        text.gameObject.name = name; text.font = font; text.fontSize = size; text.fontStyle = style;
        text.alignment = alignment; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false; text.color = new Color32(55, 61, 68, 255);
        RectTransform rt = text.rectTransform; rt.anchorMin=min; rt.anchorMax=max; rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
        return text;
    }
}
