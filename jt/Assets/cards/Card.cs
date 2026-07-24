using System.Collections;
using UnityEngine;

public enum CardRewardType
{
    None,
    Heal,
    Attack
}

[RequireComponent(typeof(SpriteRenderer))]
public class Card : MonoBehaviour
{
    [Header("Card")]
    [SerializeField] private Sprite cardBack;
    [SerializeField] private Sprite cardFace;
    [SerializeField] private Vector2 faceSymbolMaxSize = new Vector2(0.95f, 0.95f);
    [SerializeField] private int cardID;
    [SerializeField] private CardRewardType rewardType;
    [SerializeField] private int pointValue = 1;

    [Header("Audio")]
    public AudioClip flipSound;

    private GameController gameController;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer symbolRenderer;
    private AudioSource audioSource;
    private bool isFaceUp;
    private bool isMatched;
    private bool isFlipping;
    private Vector3 originalScale = Vector3.one;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        gameController = FindAnyObjectByType<GameController>();
        audioSource = GetComponent<AudioSource>();
        originalScale = transform.localScale;
    }

    private void Start()
    {
        SetCardBack();
    }

    private void OnMouseDown()
    {
        if (isMatched || isFaceUp || isFlipping || gameController == null || !gameController.CanClickCards())
        {
            return;
        }

        FlipCard();
        gameController.CardClicked(this);
    }

    public void Initialize(int id, Sprite face, Sprite back, CardRewardType type, int value)
    {
        StopAllCoroutines();
        cardID = id;
        cardFace = face;
        cardBack = back;
        rewardType = type;
        pointValue = value;
        isMatched = false;
        isFaceUp = false;
        isFlipping = false;
        gameObject.SetActive(true);
        transform.localScale = originalScale;
        SetCardBack();
    }

    public void SetBaseScale(Vector3 scale)
    {
        originalScale = scale;
        transform.localScale = originalScale;
    }

    public int GetCardID()
    {
        return cardID;
    }

    public CardRewardType GetRewardType()
    {
        return rewardType;
    }

    public int GetPointValue()
    {
        return pointValue;
    }

    public bool IsMatched()
    {
        return isMatched;
    }

    public void FlipCard()
    {
        if (!gameObject.activeInHierarchy || isFlipping)
        {
            return;
        }

        if (audioSource != null && flipSound != null)
        {
            audioSource.PlayOneShot(flipSound);
        }

        StopAllCoroutines();
        StartCoroutine(FlipAnimation());
    }

    public void TurnBack()
    {
        if (isFaceUp && !isMatched)
        {
            FlipCard();
        }
    }

    public void SetCardFace()
    {
        isFaceUp = true;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = cardBack;
        }

        ShowFaceSymbol(true);
    }

    public void SetCardBack()
    {
        isFaceUp = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = cardBack;
        }

        ShowFaceSymbol(false);
    }

    public void SetMatched()
    {
        isMatched = true;
    }

    public IEnumerator FlyOutAnimation()
    {
        isMatched = true;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + new Vector3(0f, -10f, 0f);
        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / duration);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private void UpdateSprite()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = cardBack;
        }

        ShowFaceSymbol(isFaceUp);
    }

    private void ShowFaceSymbol(bool show)
    {
        EnsureSymbolRenderer();

        if (symbolRenderer == null)
        {
            return;
        }

        symbolRenderer.gameObject.SetActive(show && cardFace != null);
        if (!show || cardFace == null)
        {
            return;
        }

        symbolRenderer.sprite = cardFace;
        symbolRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        symbolRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        FitSymbolToCard();
    }

    private void EnsureSymbolRenderer()
    {
        if (symbolRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("FaceSymbol");
        if (existing != null)
        {
            symbolRenderer = existing.GetComponent<SpriteRenderer>();
        }

        if (symbolRenderer == null)
        {
            GameObject symbolObject = new GameObject("FaceSymbol");
            symbolObject.transform.SetParent(transform, false);
            symbolRenderer = symbolObject.AddComponent<SpriteRenderer>();
        }

        symbolRenderer.gameObject.SetActive(false);
    }

    private void FitSymbolToCard()
    {
        if (spriteRenderer == null || symbolRenderer == null || cardFace == null)
        {
            return;
        }

        Bounds faceBounds = cardFace.bounds;
        if (faceBounds.size.x <= 0f || faceBounds.size.y <= 0f)
        {
            return;
        }

        float scale = Mathf.Min(
            faceSymbolMaxSize.x / faceBounds.size.x,
            faceSymbolMaxSize.y / faceBounds.size.y);

        symbolRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        symbolRenderer.transform.localPosition = -faceBounds.center * scale;
        symbolRenderer.transform.localRotation = Quaternion.identity;
    }

    private IEnumerator FlipAnimation()
    {
        isFlipping = true;
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 flatScale = new Vector3(0f, originalScale.y, originalScale.z);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, flatScale, elapsed / duration);
            yield return null;
        }

        isFaceUp = !isFaceUp;
        UpdateSprite();

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(flatScale, originalScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = originalScale;
        isFlipping = false;
    }
}
