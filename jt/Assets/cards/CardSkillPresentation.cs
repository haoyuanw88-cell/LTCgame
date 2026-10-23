using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Build-safe skill HUD and a frozen-screen, multi-cut space-rend effect.</summary>
public sealed class CardSkillPresentation : MonoBehaviour
{
    private GameController game;
    private GameObject hud;
    private GameObject effect;
    private Button button;
    private TMP_Text energyLabel;
    private TMP_Text stateLabel;
    private TMP_Text effectTitle;
    private Image energyFill;
    private Image buttonFace;
    private RawImage frozenScreen;
    private Texture2D capture;
    private Material rendMaterial;
    private AudioSource sound;
    private AudioClip rendSound;
    private int effectVersion;
    public bool IsEffectVisible => effect != null && effect.activeSelf;
    public Button SkillButton => button;

    public void Initialize(GameController controller)
    {
        game = controller;
        if (hud != null) return;
        var canvasObject = new GameObject("Card Skill Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31999;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.matchWidthOrHeight = .5f;

        hud = new GameObject("空間斬技能", typeof(RectTransform));
        hud.transform.SetParent(canvasObject.transform, false);
        var rect = hud.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(232, 272);
        rect.anchoredPosition = new Vector2(-28, 30);
        var border = Panel("金色外框", hud.transform, new Color(.84f, .64f, .25f), new Vector2(224, 142), new Vector2(0, 48));
        buttonFace = Panel("技能按鈕", border.transform, new Color(.08f, .20f, .29f), new Vector2(216, 134), Vector2.zero);
        buttonFace.raycastTarget = true;
        button = buttonFace.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonFace;
        var colors = button.colors;
        colors.disabledColor = new Color(.7f, .7f, .7f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => game.TryUseSkill());
        Text("技能名稱", button.transform, "空間斬", 31, new Vector2(178, 44), new Vector2(0, 27));
        energyLabel = Text("能量數字", button.transform, "0 / 10", 34, new Vector2(204, 40), new Vector2(0, -12));
        stateLabel = Text("技能狀態", hud.transform, "配對／回合 +1", 30, new Vector2(232, 40), new Vector2(0, -48));
        var bar = Panel("能量槽底", button.transform, new Color(.025f, .065f, .10f), new Vector2(164, 12), new Vector2(0, -46));
        energyFill = Panel("能量槽", bar.transform, new Color(.26f, .84f, 1f), new Vector2(164, 12), Vector2.zero);
        energyFill.rectTransform.pivot = new Vector2(0, .5f);
        energyFill.rectTransform.anchoredPosition = new Vector2(-82, 0);
        Text("技能效果說明", hud.transform, "傷害 25%\n回血 30%", 30, new Vector2(232, 76), new Vector2(0, -106));

        effect = new GameObject("空間撕裂特效", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        effect.transform.SetParent(canvasObject.transform, false);
        Stretch(effect.GetComponent<RectTransform>());
        var effectCanvas = effect.GetComponent<Canvas>();
        effectCanvas.overrideSorting = true;
        effectCanvas.sortingOrder = 32000; // Keep the existing pause controls above the effect.
        var imageObject = new GameObject("凍結畫面與空間裂痕", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(effect.transform, false);
        frozenScreen = imageObject.GetComponent<RawImage>();
        Stretch(frozenScreen.rectTransform);
        frozenScreen.raycastTarget = true;
        var shader = Resources.Load<Shader>("CardSpaceRend");
        if (shader != null && shader.isSupported)
        {
            rendMaterial = new Material(shader);
            frozenScreen.material = rendMaterial;
        }
        effectTitle = Text("技能演出標題", effect.transform, "空  間  斬", 52, new Vector2(760, 90), new Vector2(0, 235));
        effectTitle.color = new Color(.7f, .94f, 1f);
        effect.SetActive(false);
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 0f;
        rendSound = MakeRendSound();
        Refresh();
    }

    private void Update()
    {
        if (game != null && hud != null) Refresh();
        if (sound != null)
        {
            if (CognitiveGamePauseMenu.IsGamePaused) sound.Pause();
            else if (IsEffectVisible && !sound.isPlaying) sound.UnPause();
        }
    }

    private void Refresh()
    {
        hud.SetActive(game.IsAssessmentRunning && (game.levelSelection == null || !game.levelSelection.IsOpen));
        button.interactable = game.CanUseSkill;
        energyLabel.text = game.SkillEnergy + " / " + GameController.SkillEnergyCost;
        energyFill.rectTransform.sizeDelta = new Vector2(164f * game.SkillEnergy / GameController.SkillEnergyCost, 12);
        bool ready = game.SkillEnergy >= GameController.SkillEnergyCost;
        stateLabel.text = ready ? (game.CanUseSkill ? "點擊施放" : "等待結算") : "配對／回合 +1";
        float pulse = ready ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
        buttonFace.color = Color.Lerp(new Color(.08f, .20f, .29f), new Color(.05f, .49f, .57f), pulse);
    }

    public IEnumerator PlayEffect()
    {
        CancelEffect();
        int version = effectVersion;
        // Capture all world sprites and UI once; the shader then splits this frozen image.
        yield return new WaitForEndOfFrame();
        if (version != effectVersion) yield break;
        capture = ScreenCapture.CaptureScreenshotAsTexture();
        frozenScreen.texture = capture;
        frozenScreen.color = Color.white;
        if (rendMaterial != null) rendMaterial.SetFloat("_Progress", 0f);
        effect.SetActive(true);
        sound.PlayOneShot(rendSound, .35f);
        const float duration = 2.6f;
        float elapsed = 0f;
        while (elapsed < duration && version == effectVersion)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (rendMaterial != null) rendMaterial.SetFloat("_Progress", progress);
            else frozenScreen.color = Color.Lerp(new Color(.36f, .46f, .62f), Color.white, progress);
            float alpha = Mathf.Clamp01(progress / .12f) * (1f - Mathf.InverseLerp(.72f, .9f, progress));
            effectTitle.color = new Color(.7f, .94f, 1f, alpha);
            effectTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f, 1.06f, progress);
            yield return null;
        }
        if (version == effectVersion) CancelEffect();
    }

    public void CancelEffect()
    {
        effectVersion++;
        if (effect != null) effect.SetActive(false);
        if (sound != null) sound.Stop();
        if (frozenScreen != null) frozenScreen.texture = null;
        if (capture != null) Destroy(capture);
        capture = null;
    }

    private void OnDestroy()
    {
        CancelEffect();
        if (rendMaterial != null) Destroy(rendMaterial);
        if (rendSound != null) Destroy(rendSound);
    }

    private TMP_Text Text(string name, Transform parent, string value, float size, Vector2 dimensions, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = game.statusFont;
        label.text = value;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, .94f, .73f);
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = dimensions;
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    private static Image Panel(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static AudioClip MakeRendSound()
    {
        const int rate = 22050;
        var samples = new float[rate * 3];
        var random = new System.Random(29);
        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)rate;
            float burst = 0f;
            for (int cut = 0; cut < 10; cut++)
            {
                float age = time - (.36f + cut * .14f);
                if (age >= 0 && age < .13f) burst += Mathf.Exp(-age * 38f);
            }
            float impactAge = time - 1.95f;
            float impact = impactAge >= 0 ? Mathf.Exp(-impactAge * 8f) : 0f;
            float noise = (float)random.NextDouble() * 2f - 1f;
            samples[i] = Mathf.Clamp(noise * burst * .25f + (noise * .3f + Mathf.Sin(time * 320f) * .5f) * impact, -.9f, .9f);
        }
        var clip = AudioClip.Create("空間斬音效", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
