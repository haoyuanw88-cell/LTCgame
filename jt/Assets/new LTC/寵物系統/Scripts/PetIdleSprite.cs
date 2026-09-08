using UnityEngine;

/// <summary>
/// Plays a 4x4 walking sprite sheet and lets a pet wander inside a small area.
/// The sheet stays in Resources so artists can replace the artwork without
/// changing the PetGarden scene or this component.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PetIdleSprite : MonoBehaviour
{
    [Tooltip("Resources path without the extension, e.g. Pets/rabbit_walk_sheet.")]
    [SerializeField] private string resourcePath = "Pets/rabbit_walk_sheet";
    [Tooltip("The source image faces right. Turn this off for a left-facing sheet.")]
    [SerializeField] private bool sourceFacesRight = true;
    [SerializeField, Min(0.1f)] private float targetHeight = 2.05f;
    [SerializeField, Min(1f)] private float framesPerSecond = 10f;
    [SerializeField, Min(0.05f)] private float walkSpeed = 0.75f;
    [SerializeField] private Vector2 walkMin = new Vector2(-4.2f, -2.2f);
    [SerializeField] private Vector2 walkMax = new Vector2(4.2f, 1.1f);
    [SerializeField, Min(0f)] private float minimumPause = 0.8f;
    [SerializeField, Min(0f)] private float maximumPause = 2.3f;
    [SerializeField] private int sortingOrder = 2;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private int frameIndex;
    private float nextFrameAt;
    private Vector3 destination;
    private float resumeWalkingAt;
    private bool isWalking;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;

        // Upgrade the two pets already placed in the scene from the earlier
        // four-frame idle sheets without requiring a manual Inspector pass.
        if (resourcePath.EndsWith("_idle_sheet"))
        {
            bool isCat = gameObject.name.Contains("貓");
            resourcePath = isCat ? "Pets/cat_walk_sheet" : "Pets/rabbit_walk_sheet";
            sourceFacesRight = !isCat;
        }

        BuildFrames();
    }

    private void OnEnable() => StartPause();

    private void Update()
    {
        if (frames == null || frames.Length == 0)
            return;

        if (!isWalking && Time.time >= resumeWalkingAt)
            ChooseDestination();
        if (!isWalking)
            return;

        Vector3 previous = transform.position;
        transform.position = Vector3.MoveTowards(previous, destination, walkSpeed * Time.deltaTime);
        UpdateFacing(destination.x - previous.x);
        PlayNextFrame();
        if (Vector3.SqrMagnitude(transform.position - destination) <= 0.0025f)
            StartPause();
    }

    private void ChooseDestination()
    {
        Vector3 candidate = transform.position;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            candidate = new Vector3(Random.Range(walkMin.x, walkMax.x), Random.Range(walkMin.y, walkMax.y), transform.position.z);
            if (Vector2.Distance(candidate, transform.position) >= 1f)
                break;
        }
        destination = candidate;
        isWalking = true;
        nextFrameAt = Time.time;
    }

    private void StartPause()
    {
        isWalking = false;
        frameIndex = 0;
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[frameIndex];
        resumeWalkingAt = Time.time + Random.Range(minimumPause, maximumPause);
    }

    private void PlayNextFrame()
    {
        if (Time.time < nextFrameAt)
            return;
        frameIndex = (frameIndex + 1) % frames.Length;
        spriteRenderer.sprite = frames[frameIndex];
        nextFrameAt = Time.time + 1f / framesPerSecond;
    }

    private void UpdateFacing(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) < 0.001f)
            return;
        bool movingLeft = horizontalMovement < 0f;
        spriteRenderer.flipX = sourceFacesRight ? movingLeft : !movingLeft;
    }

    private void BuildFrames()
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"Pet sprite sheet was not found: Resources/{resourcePath}", this);
            enabled = false;
            return;
        }

        const int columns = 4;
        const int rows = 4;
        int cellWidth = texture.width / columns;
        int cellHeight = texture.height / rows;
        frames = new Sprite[columns * rows];
        int index = 0;

        // Sprite rect origin is bottom-left, so process the visual top row first.
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect rect = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                frames[index++] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            }
        }

        spriteRenderer.sprite = frames[0];
        float spriteHeight = frames[0].bounds.size.y;
        if (spriteHeight > 0.001f)
        {
            float uniformScale = targetHeight / spriteHeight;
            transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
    }
}
