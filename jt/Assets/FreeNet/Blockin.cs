using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AudioSource))]
[ExecuteInEditMode]
public class Blockin : MonoBehaviour
{
    public PipeFlowColor pipeColor = PipeFlowColor.Blue;
    public PipeFlowColor currentFlowColor = PipeFlowColor.None;
    public bool hasWater = false;
    public bool isStartingPipe = false;
    public bool isEndingPipe = false;
    public bool isRotationLocked = false;

    public int x;
    public int y;
    [Min(0.1f)] public float cellSize = 2f;

    [Tooltip("0: Up, 1: Right, 2: Down, 3: Left")]
    public bool[] openings = new bool[4];

    [Header("Sprites")]
    public Sprite emptySprite;
    public Sprite waterSprite;
    public Sprite redWaterSprite;
    public Sprite rotationLockSprite;
    public string rotationLockChildName = "chain_shade3.0";
    public int rotationLockSortingOrderOffset = 10;

    [Header("Audio")]
    public AudioSource myAudioSource;
    public AudioClip rotateSound;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer rotationLockRenderer;
    private Vector3 normalLocalScale;
    private bool hasNormalLocalScale;

    private void OnValidate()
    {
        EnsureRequiredComponents();
        cellSize = Mathf.Max(0.1f, cellSize);
        x = Mathf.RoundToInt(transform.position.x / cellSize);
        y = Mathf.RoundToInt(transform.position.y / cellSize);
        transform.position = new Vector3(x * cellSize, y * cellSize, 0f);
        UpdateVisual();
    }

    private void Reset()
    {
        EnsureRequiredComponents();
    }

    private void Start()
    {
        EnsureRequiredComponents();

        if (Application.isPlaying)
        {
            if (isStartingPipe)
            {
                SetFlowColor(pipeColor);
            }

            UpdateVisual();
        }
    }

    private void OnMouseDown()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PipeManager manager = Object.FindAnyObjectByType<PipeManager>();
        if (manager == null || manager.isGameOver || manager.IsTutorialActive)
        {
            return;
        }

        if (isStartingPipe || isEndingPipe || IsRotationLocked())
        {
            return;
        }

        RotatePipe(manager);
    }

    public bool IsRotationLocked()
    {
        return isRotationLocked || FindRotationLockRenderer() != null;
    }

    public void ClearFlow()
    {
        currentFlowColor = PipeFlowColor.None;
        hasWater = false;
    }

    public void SetFlowColor(PipeFlowColor color)
    {
        currentFlowColor = PipeLevelUtility.NormalizeFlowColor(color);
        hasWater = true;
    }

    public bool CanAcceptFlow(PipeFlowColor color)
    {
        PipeFlowColor normalizedColor = PipeLevelUtility.NormalizeFlowColor(color);
        if (isStartingPipe || isEndingPipe)
        {
            return PipeLevelUtility.NormalizeFlowColor(pipeColor) == normalizedColor;
        }

        return currentFlowColor == PipeFlowColor.None || currentFlowColor == normalizedColor;
    }

    private void RotatePipe(PipeManager manager)
    {
        transform.Rotate(0f, 0f, -90f);

        bool last = openings[3];
        for (int i = 3; i > 0; i--)
        {
            openings[i] = openings[i - 1];
        }

        openings[0] = last;

        if (myAudioSource != null && rotateSound != null)
        {
            myAudioSource.PlayOneShot(rotateSound);
        }

        manager.AddMoveCount();
        manager.CheckConnections();
    }

    public void UpdateVisual()
    {
        EnsureRequiredComponents();

        if (spriteRenderer != null)
        {
            PipeFlowColor visibleFlowColor = GetVisibleFlowColor();
            Sprite activeSprite = hasWater ? GetWaterSprite(visibleFlowColor) : emptySprite;
            if (activeSprite != null)
            {
                spriteRenderer.sprite = activeSprite;
            }

            ApplySpriteScale(activeSprite, visibleFlowColor);
        }

        UpdateRotationLockVisual();
    }

    private void EnsureRequiredComponents()
    {
        pipeColor = PipeLevelUtility.NormalizeFlowColor(pipeColor);

        if (!hasNormalLocalScale)
        {
            normalLocalScale = transform.localScale;
            hasNormalLocalScale = true;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (myAudioSource == null)
        {
            myAudioSource = GetComponent<AudioSource>();
        }

        if (myAudioSource != null)
        {
            myAudioSource.playOnAwake = false;
            myAudioSource.spatialBlend = 0f;
        }
    }

    private PipeFlowColor GetVisibleFlowColor()
    {
        if (currentFlowColor != PipeFlowColor.None)
        {
            return currentFlowColor;
        }

        return hasWater ? PipeLevelUtility.NormalizeFlowColor(pipeColor) : PipeFlowColor.None;
    }

    private Sprite GetWaterSprite(PipeFlowColor color)
    {
        if (color == PipeFlowColor.Red && redWaterSprite != null)
        {
            return redWaterSprite;
        }

        if (color == PipeFlowColor.Blue && waterSprite != null)
        {
            return waterSprite;
        }

        return color == PipeFlowColor.Red ? waterSprite : emptySprite;
    }

    private void ApplySpriteScale(Sprite activeSprite, PipeFlowColor visibleFlowColor)
    {
        if (!hasNormalLocalScale)
        {
            normalLocalScale = transform.localScale;
            hasNormalLocalScale = true;
        }

        float redScale = visibleFlowColor == PipeFlowColor.Red ? GetRedSpriteScale(activeSprite) : 1f;
        if (!Mathf.Approximately(redScale, 1f))
        {
            transform.localScale = new Vector3(redScale, redScale, normalLocalScale.z);
        }
        else
        {
            transform.localScale = normalLocalScale;
        }
    }

    private float GetRedSpriteScale(Sprite sprite)
    {
        if (sprite == null)
        {
            return 1f;
        }

        switch (GetBaseSpriteName(sprite.name))
        {
            case "red29":
            case "red30":
                return 0.7f;
            case "red31":
            case "red32":
            case "red33":
                return 0.9f;
            case "red42":
                return 0.6f;
            default:
                return 1f;
        }
    }

    private string GetBaseSpriteName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return string.Empty;
        }

        int underscoreIndex = spriteName.IndexOf('_');
        return underscoreIndex > 0 ? spriteName.Substring(0, underscoreIndex) : spriteName;
    }

    private void UpdateRotationLockVisual()
    {
        rotationLockRenderer = FindRotationLockRenderer();
        bool shouldShowLock = isRotationLocked || rotationLockRenderer != null;

        if (!shouldShowLock)
        {
            return;
        }

        bool createdLockRenderer = false;
        if (rotationLockRenderer == null && rotationLockSprite != null)
        {
            GameObject lockObject = new GameObject(rotationLockChildName);
            lockObject.transform.SetParent(transform, false);
            rotationLockRenderer = lockObject.AddComponent<SpriteRenderer>();
            createdLockRenderer = true;
        }

        if (rotationLockRenderer == null)
        {
            return;
        }

        if (rotationLockRenderer.sprite == null && rotationLockSprite != null)
        {
            rotationLockRenderer.sprite = rotationLockSprite;
            createdLockRenderer = true;
        }

        if (spriteRenderer != null)
        {
            rotationLockRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
            rotationLockRenderer.sortingOrder = spriteRenderer.sortingOrder + rotationLockSortingOrderOffset;
            if (rotationLockRenderer.sharedMaterial == null)
            {
                rotationLockRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            }
        }

        if (createdLockRenderer)
        {
            FitGeneratedRotationLockRenderer();
        }
    }

    private void FitGeneratedRotationLockRenderer()
    {
        if (rotationLockRenderer == null || rotationLockRenderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = rotationLockRenderer.sprite.bounds.size;
        float maxSpriteSize = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxSpriteSize <= 0f)
        {
            return;
        }

        float targetSize = Mathf.Max(0.1f, cellSize) * 1.05f;
        float uniformScale = targetSize / maxSpriteSize;
        rotationLockRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

        Vector3 spriteCenter = rotationLockRenderer.sprite.bounds.center;
        Vector3 fittedPosition = -spriteCenter * uniformScale;
        rotationLockRenderer.transform.localPosition = new Vector3(fittedPosition.x, fittedPosition.y, -0.01f);
    }

    private SpriteRenderer FindRotationLockRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.transform == transform)
            {
                continue;
            }

            string childName = renderer.gameObject.name;
            if (childName.Contains("chain") || childName.Contains("chain_shade3") || childName == rotationLockChildName)
            {
                return renderer;
            }
        }

        return null;
    }
}
