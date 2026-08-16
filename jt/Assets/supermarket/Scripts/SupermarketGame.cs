using System;
using System.Collections;
using System.Collections.Generic;
using LTCCognitiveAssessment;
using UnityEngine;

[Serializable]
public class ShoppingRequirement
{
    [Tooltip("把 Project 視窗中的商品圖片（Sprite）拖到這裡")]
    public Sprite productImage;

    [Min(1)]
    [Tooltip("玩家需要購買的數量")]
    public int quantity = 1;

    [NonSerialized] public int remaining;
}

public class SupermarketGame : MonoBehaviour
{
    [Header("購物清單（可隨時修改）")]
    public List<ShoppingRequirement> requiredProducts = new List<ShoppingRequirement>();

    [Header("鏡頭")]
    public Camera gameCamera;
    public float productAreaCameraX = -25f;
    public float checkoutCameraX = 0f;

    [Header("背景音樂")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.35f;

    [Header("Shopping Panel")]
    public float panelHideDelay = 3f;
    public float panelHiddenYOffset = -2.3f;
    public float panelMoveSpeed = 7f;

    [Header("Tutorial")]
    public string tutorialPanelName = "BackTile_12_0 2";
    public string tutorialCloseButtonName = "tile_0016_0";

    private Vector3 panelOffset;
    private float currentPanelYOffset;
    private float targetPanelYOffset;
    private float nextPanelHideTime;
    private float startTime;
    private float clearTime;
    private int failures;
    private int successfulPurchases;
    private int trialIndex;
    private int randomSeed;
    private bool cleared;
    private bool gameStarted;
    private bool assessmentCompleted;
    private string assessmentSessionId;
    private GameObject tutorialPanel;
    private SpriteRenderer panelRenderer;
    private GUIStyle listStyle;
    private GUIStyle resultStyle;
    private GUIStyle resultBoxStyle;

    public bool IsCleared => cleared;
    public bool IsGameStarted => gameStarted;

    private void Awake()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;

        panelRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider(gameObject);

        if (gameCamera != null)
            panelOffset = transform.position - gameCamera.transform.position;

        foreach (ShoppingRequirement requirement in requiredProducts)
        {
            if (requirement != null)
                requirement.remaining = Mathf.Max(1, requirement.quantity);
        }

        SetupTutorial();
        AddClickTargets();
        StartMusic();

        if (tutorialPanel == null)
            StartGame();
    }

    private void Update()
    {
        if (gameStarted && !cleared && Time.time >= nextPanelHideTime)
            targetPanelYOffset = panelHiddenYOffset;
    }

    private void OnMouseDown()
    {
        if (gameStarted && !cleared)
            ShowShoppingPanel();
    }

    public void ShowShoppingPanel()
    {
        targetPanelYOffset = 0f;
        nextPanelHideTime = Time.time + panelHideDelay;
    }

    public void StartGame()
    {
        if (gameStarted)
            return;

        gameStarted = true;
        startTime = Time.time;
        randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        trialIndex = 0;
        successfulPurchases = 0;
        assessmentCompleted = false;
        assessmentSessionId = CognitiveAssessmentService.BeginGame(
            "supermarket_shopping",
            CognitiveProtocolRegistry.ProtocolVersion);
        ShowShoppingPanel();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }

    private void UpdatePanelPosition()
    {
        currentPanelYOffset = Mathf.MoveTowards(
            currentPanelYOffset,
            targetPanelYOffset,
            panelMoveSpeed * Time.deltaTime);

        transform.position = gameCamera.transform.position
            + panelOffset
            + Vector3.up * currentPanelYOffset;
    }

    private void SetupTutorial()
    {
        tutorialPanel = FindObjectByName(tutorialPanelName);
        if (tutorialPanel == null)
            return;

        tutorialPanel.SetActive(true);
        gameStarted = false;
        targetPanelYOffset = 0f;
        currentPanelYOffset = 0f;
        nextPanelHideTime = float.PositiveInfinity;

        GameObject closeButton = FindObjectByName(tutorialCloseButtonName);
        if (closeButton == null || !IsChildOf(closeButton.transform, tutorialPanel.transform))
            return;

        EnsureCollider(closeButton);
        TutorialStartClick startClick = closeButton.GetComponent<TutorialStartClick>();
        if (startClick == null)
            startClick = closeButton.AddComponent<TutorialStartClick>();
        startClick.Setup(this);
    }

    private static GameObject FindObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform item in transforms)
        {
            if (item.name == objectName)
                return item.gameObject;
        }

        return null;
    }

    private bool IsTutorialObject(GameObject target)
    {
        return tutorialPanel != null && target != null && IsChildOf(target.transform, tutorialPanel.transform);
    }

    private static bool IsChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }

        return false;
    }

    private void LateUpdate()
    {
        // 購物清單面板固定跟著鏡頭，切換區域時仍保持在畫面相同位置。
        if (gameCamera != null)
            UpdatePanelPosition();
    }

    public void MoveCamera(bool goToProductArea)
    {
        if (!gameStarted || gameCamera == null || cleared)
            return;

        Vector3 position = gameCamera.transform.position;
        position.x = goToProductArea ? productAreaCameraX : checkoutCameraX;
        gameCamera.transform.position = position;
    }

    public void Buy(SpriteRenderer product)
    {
        if (!gameStarted || cleared || product == null)
            return;

        ShoppingRequirement match = requiredProducts.Find(
            item => item != null && item.productImage == product.sprite && item.remaining > 0);

        if (match == null)
        {
            failures++;
            RecordPurchaseTrial(product, false, "unlisted_or_extra_product");
            return;
        }

        RecordPurchaseTrial(product, true, "");
        successfulPurchases++;
        match.remaining--;

        bool allDone = requiredProducts.Count > 0;
        foreach (ShoppingRequirement item in requiredProducts)
        {
            if (item != null && item.productImage != null && item.remaining > 0)
            {
                allDone = false;
                break;
            }
        }

        if (allDone)
        {
            cleared = true;
            clearTime = Time.time - startTime;
            CompleteAssessment();
        }
    }

    private void RecordPurchaseTrial(SpriteRenderer product, bool correct, string errorType)
    {
        if (string.IsNullOrEmpty(assessmentSessionId))
            return;

        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = 1,
            stepIndex = trialIndex,
            eventKind = "response",
            randomSeed = randomSeed,
            difficulty = Mathf.Max(1, requiredProducts.Count),
            stimulusCount = requiredProducts.Count,
            condition = "shopping_list_selection",
            stimulus = BuildRemainingListSnapshot(),
            expectedAnswer = "required_product",
            userAnswer = ProductName(product),
            outcome = correct ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt(Mathf.Max(0f, Time.time - startTime) * 1000f),
            errorType = errorType
        });
    }

    private void CompleteAssessment()
    {
        if (assessmentCompleted || string.IsNullOrEmpty(assessmentSessionId))
            return;

        assessmentCompleted = true;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = ++trialIndex,
            roundIndex = 1,
            stepIndex = trialIndex,
            eventKind = "round_summary",
            randomSeed = randomSeed,
            difficulty = Mathf.Max(1, requiredProducts.Count),
            stimulusCount = requiredProducts.Count,
            condition = "shopping_completion",
            stimulus = "required=" + requiredProducts.Count + "|successes=" + successfulPurchases + "|failures=" + failures,
            expectedAnswer = "complete_list",
            userAnswer = "complete_list",
            outcome = TrialOutcome.Correct,
            reactionTimeMs = Mathf.RoundToInt(clearTime * 1000f),
            roundElapsedMs = Mathf.RoundToInt(clearTime * 1000f),
            actionCount = successfulPurchases + failures,
            errorCount = failures
        });
        CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.WorkingMemory,
            0f,
            requiredProducts.Count);
    }

    private string BuildRemainingListSnapshot()
    {
        List<string> items = new List<string>();
        foreach (ShoppingRequirement item in requiredProducts)
        {
            if (item == null || item.productImage == null)
                continue;
            items.Add(item.productImage.name + ":" + item.remaining);
        }

        return string.Join(",", items);
    }

    private static string ProductName(SpriteRenderer product)
    {
        if (product == null)
            return "";
        if (product.sprite != null)
            return product.sprite.name;
        return product.name;
    }

    private void AddClickTargets()
    {
        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>();
        foreach (SpriteRenderer sprite in sprites)
        {
            GameObject target = sprite.gameObject;
            string objectName = target.name.ToLowerInvariant();

            if (target == gameObject || IsTutorialObject(target))
                continue;

            if (objectName == "tile_0185_0" || objectName == "tile_0186_0")
            {
                EnsureCollider(target);
                CameraArrow arrow = target.GetComponent<CameraArrow>();
                if (arrow == null)
                    arrow = target.AddComponent<CameraArrow>();
                arrow.Setup(this, objectName == "tile_0185_0");
                continue;
            }

            if (IsDecoration(objectName))
                continue;

            EnsureCollider(target);
            ProductClick productClick = target.GetComponent<ProductClick>();
            if (productClick == null)
                productClick = target.AddComponent<ProductClick>();
            productClick.Setup(this);
        }
    }

    private static bool IsDecoration(string objectName)
    {
        return objectName.StartsWith("background")
            || objectName.StartsWith("counter")
            || objectName.StartsWith("conveyor")
            || objectName.StartsWith("cashier")
            || objectName.StartsWith("computer")
            || objectName.StartsWith("hat_")
            || objectName.StartsWith("speaker");
    }

    private static void EnsureCollider(GameObject target)
    {
        if (target.GetComponent<Collider2D>() == null)
            target.AddComponent<BoxCollider2D>();
    }

    private void StartMusic()
    {
        if (backgroundMusic == null)
            return;

        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.clip = backgroundMusic;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = musicVolume;
        source.spatialBlend = 0f;
        source.Play();
    }

    private void OnGUI()
    {
        if (!gameStarted || gameCamera == null || panelRenderer == null)
            return;

        CreateStyles();
        DrawShoppingList();

        if (cleared)
        {
            Rect box = new Rect(Screen.width * 0.25f, Screen.height * 0.32f,
                Screen.width * 0.5f, Screen.height * 0.28f);
            GUI.Box(box, string.Empty, resultBoxStyle);
            GUI.Label(box,
                $"過關！\n時間：{clearTime:0.00} 秒\n失敗次數：{failures}",
                resultStyle);
        }
    }

    private void DrawShoppingList()
    {
        Bounds bounds = panelRenderer.bounds;
        Vector3 topLeft = gameCamera.WorldToScreenPoint(
            new Vector3(bounds.min.x, bounds.max.y, transform.position.z));
        Vector3 bottomRight = gameCamera.WorldToScreenPoint(
            new Vector3(bounds.max.x, bounds.min.y, transform.position.z));

        Rect panel = new Rect(
            topLeft.x,
            Screen.height - topLeft.y,
            bottomRight.x - topLeft.x,
            topLeft.y - bottomRight.y);

        panel.x += panel.width * 0.12f;
        panel.y += panel.height * 0.12f;
        panel.width *= 0.76f;
        panel.height *= 0.76f;

        List<ShoppingRequirement> visibleItems = requiredProducts.FindAll(
            item => item != null && item.productImage != null);

        if (visibleItems.Count > 0)
        {
            float slotWidth = panel.width / visibleItems.Count;
            float iconSize = Mathf.Min(slotWidth * 0.65f, panel.height * 0.58f);
            float iconY = panel.y + panel.height * 0.06f;

            for (int i = 0; i < visibleItems.Count; i++)
            {
                ShoppingRequirement item = visibleItems[i];
                Rect iconRect = new Rect(
                    panel.x + slotWidth * i + (slotWidth - iconSize) * 0.5f,
                    iconY,
                    iconSize,
                    iconSize);

                DrawSprite(iconRect, item.productImage);

                Rect countRect = new Rect(
                    panel.x + slotWidth * i,
                    iconY + iconSize,
                    slotWidth,
                    panel.height * 0.22f);
                GUI.Label(countRect, $"× {item.remaining}", listStyle);
            }
        }

        Rect statusRect = new Rect(
            panel.x,
            panel.y + panel.height * 0.80f,
            panel.width,
            panel.height * 0.20f);
        GUI.Label(statusRect,
            $"失敗：{failures}　時間：{(cleared ? clearTime : Time.time - startTime):0.0}s",
            listStyle);
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height);

        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
    }

    private void CreateStyles()
    {
        if (listStyle != null)
            return;

        listStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 42f), 14, 28),
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        listStyle.normal.textColor = new Color(0.12f, 0.24f, 0.08f);

        resultStyle = new GUIStyle(listStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 24f), 24, 48)
        };

        resultBoxStyle = new GUIStyle(GUI.skin.box);
        resultBoxStyle.normal.background = MakeTexture(new Color(1f, 0.96f, 0.78f, 0.96f));
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}

public class ProductClick : MonoBehaviour
{
    private SupermarketGame game;
    private SpriteRenderer sprite;

    public void Setup(SupermarketGame owner)
    {
        game = owner;
        sprite = GetComponent<SpriteRenderer>();
    }

    private void OnMouseDown()
    {
        if (game == null || !game.IsGameStarted || game.IsCleared)
            return;

        game.Buy(sprite);
        StopAllCoroutines();
        StartCoroutine(ClickFlash());
    }

    private IEnumerator ClickFlash()
    {
        if (sprite == null)
            yield break;

        Color original = sprite.color;
        sprite.color = new Color(0.65f, 1f, 0.65f, original.a);
        yield return new WaitForSeconds(0.12f);
        sprite.color = original;
    }
}

public class TutorialStartClick : MonoBehaviour
{
    private SupermarketGame game;

    public void Setup(SupermarketGame owner)
    {
        game = owner;
    }

    private void OnMouseDown()
    {
        if (game != null)
            game.StartGame();
    }
}

public class CameraArrow : MonoBehaviour
{
    private SupermarketGame game;
    private bool goToProductArea;

    public void Setup(SupermarketGame owner, bool goToProducts)
    {
        game = owner;
        goToProductArea = goToProducts;
    }

    private void OnMouseDown()
    {
        if (game != null)
            game.MoveCamera(goToProductArea);
    }
}
