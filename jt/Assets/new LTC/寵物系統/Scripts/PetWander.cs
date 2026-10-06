using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime movement only. Visual frames are authored in an AnimatorController,
/// so the player never creates Sprite objects or loads texture sheets at runtime.
/// </summary>
public sealed class PetWander : MonoBehaviour
{
    public bool IsWalking => isWalking;
    public string PetId => !string.IsNullOrEmpty(collectionId) ? collectionId : name.Contains("貓") ? "cat" : name.Contains("兔") ? "rabbit" : name;
    public string DisplayName => !string.IsNullOrEmpty(collectionName) ? collectionName : PetId == "cat" ? "貓咪" : PetId == "rabbit" ? "兔子" : name;
    public Sprite Portrait => spriteRenderer == null ? null : spriteRenderer.sprite;
    public bool IsCollectionPet => !string.IsNullOrEmpty(collectionId);
    string collectionId, collectionName;
    Sprite[] collectionFrames;
    Bounds collectionVisibleBounds;
    int collectionFrame;
    float nextCollectionFrame;
    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly List<PetWander> ActivePets = new List<PetWander>();
    private const int PetForegroundBaseOrder = 5000;

    [Header("Movement area")]
    [SerializeField] private Vector2 walkMin = new Vector2(-4.2f, -2.2f);
    [SerializeField] private Vector2 walkMax = new Vector2(4.2f, 1.1f);
    [SerializeField, Min(0.05f)] private float walkSpeed = 0.75f;
    [SerializeField, Min(0f)] private float minimumPause = 0.8f;
    [SerializeField, Min(0f)] private float maximumPause = 2.3f;
    [SerializeField, Min(0f)] private float minimumPauseAfterWalk = 3f;
    [SerializeField, Min(0f)] private float maximumPauseAfterWalk = 5f;

    [Header("Avoid other pets")]
    [SerializeField, Min(0.1f)] private float separationRadius = 1.15f;
    [SerializeField, Min(0.1f)] private float separationSpeed = 3f;

    [Header("Visuals")]
    [Tooltip("Enable when the original sprite sheet faces right.")]
    [SerializeField] private bool sourceFacesRight = true;
    [SerializeField, Min(0.1f)] private float targetHeight = 2.05f;
    [SerializeField] private int sortingOrder = 2;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector3 destination;
    private float resumeWalkingAt;
    private bool isWalking;
    private bool usesWalkingParameter;

    private void Awake()
    {
        // Always use the authored visual child. Older scene versions may still
        // contain a renderer/animator on the pet root; selecting those can draw
        // two pets or drive a disabled animator instead of the visible artwork.
        Transform artwork = transform.Find("Visual/Artwork");
        spriteRenderer = artwork != null
            ? artwork.GetComponent<SpriteRenderer>()
            : GetComponentInChildren<SpriteRenderer>(true);
        animator = artwork != null
            ? artwork.GetComponent<Animator>()
            : GetComponentInChildren<Animator>(true);

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        Animator rootAnimator = GetComponent<Animator>();
        if (rootRenderer != null && rootRenderer != spriteRenderer)
            rootRenderer.enabled = false;
        if (rootAnimator != null && rootAnimator != animator)
            rootAnimator.enabled = false;

        if (spriteRenderer == null)
        {
            Debug.LogError("Pet visual hierarchy is incomplete. A child SpriteRenderer and Animator are required.", this);
            enabled = false;
            return;
        }

        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.enabled = true;
        if (animator != null) animator.enabled = true;
        foreach (AnimatorControllerParameter parameter in animator != null ? animator.parameters : new AnimatorControllerParameter[0])
        {
            if (parameter.nameHash == WalkingHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                usesWalkingParameter = true;
                break;
            }
        }
        if (animator != null) FitVisualToTargetHeight();
    }

    public void ConfigureCollectionPet(LTCPetDefinition definition, Sprite[] frames)
    {
        collectionId = definition.id;
        collectionName = definition.displayName;
        collectionFrames = frames;
        sourceFacesRight = definition.id != "cat";
        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
            Color32[] pixels = frames[0].texture.GetPixels32();
            int min = frames[0].texture.height, max = -1, minX = frames[0].texture.width, maxX = -1;
            for (int y = 0; y < frames[0].texture.height; y++)
                for (int x = 0; x < frames[0].texture.width; x++)
                    if (pixels[y * frames[0].texture.width + x].a > 20) { min = Mathf.Min(min, y); max = Mathf.Max(max, y); minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            float visibleHeight = max >= min ? (max - min + 1) / frames[0].pixelsPerUnit : frames[0].bounds.size.y;
            float scale = targetHeight / visibleHeight;
            spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            float ppu = frames[0].pixelsPerUnit;
            collectionVisibleBounds = new Bounds(new Vector3((minX + maxX + 1f) * .5f / ppu - frames[0].bounds.extents.x,
                (min + max + 1f) * .5f / ppu - frames[0].bounds.extents.y, 0f),
                new Vector3((maxX - minX + 1f) / ppu, visibleHeight, .01f));
        }
        PetHungerService.RemainingSeconds(PetId);
        StartPause(false);
    }

    private void OnEnable()
    {
        if (!ActivePets.Contains(this))
            ActivePets.Add(this);
        StartPause(false);
    }

    private void OnDisable()
    {
        ActivePets.Remove(this);
    }

    private void Update()
    {
        if(PetCollectionBook.ModalOpen)return;
        ResolvePetOverlap();
        UpdateDepthSorting();

        if (!isWalking)
        {
            if (Time.time >= resumeWalkingAt)
                ChooseDestination();
            return;
        }

        Vector3 beforeMove = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(beforeMove, destination, walkSpeed * Time.deltaTime);
        if (!CanMoveTo(nextPosition))
        {
            StartPause(true);
            return;
        }
        transform.position = nextPosition;
        UpdateFacing(destination.x - beforeMove.x);
        if (collectionFrames != null && collectionFrames.Length > 0 && Time.time >= nextCollectionFrame)
        {
            collectionFrame = (collectionFrame + 1) % collectionFrames.Length;
            spriteRenderer.sprite = collectionFrames[collectionFrame];
            nextCollectionFrame = Time.time + .11f;
        }

        if (Vector3.SqrMagnitude(transform.position - destination) <= 0.0025f)
            StartPause(true);
    }

    private void ChooseDestination()
    {
        Vector3 candidate = transform.position;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            candidate = new Vector3(
                Random.Range(walkMin.x, walkMax.x),
                Random.Range(walkMin.y, walkMax.y),
                transform.position.z);

            if (Vector2.Distance(candidate, transform.position) >= 1f && IsClearOfOtherPets(candidate))
                break;
        }

        destination = candidate;
        isWalking = true;
        if (usesWalkingParameter)
            animator.SetBool(WalkingHash, true);
        else if (animator != null)
            animator.speed = 1f;
    }

    private void StartPause(bool afterWalking)
    {
        isWalking = false;
        if (usesWalkingParameter)
        {
            animator.SetBool(WalkingHash, false);
        }
        else if (animator != null)
        {
            animator.Play(0, 0, 0f);
            animator.speed = 0f;
        }
        if (collectionFrames != null && collectionFrames.Length > 0)
        {
            collectionFrame = 0;
            spriteRenderer.sprite = collectionFrames[0];
            nextCollectionFrame = Time.time;
        }
        float low = afterWalking ? minimumPauseAfterWalk : minimumPause;
        float high = afterWalking ? maximumPauseAfterWalk : maximumPause;
        resumeWalkingAt = Time.time + Random.Range(low, Mathf.Max(low, high));
    }

    private bool IsClearOfOtherPets(Vector3 candidate)
    {
        float minimumDistance = separationRadius * 1.25f;
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled)
                continue;

            if (Vector2.Distance(candidate, other.transform.position) < minimumDistance)
                return false;
        }
        return true;
    }

    // Test the actual visible rectangles, not a fixed distance between root pivots.
    // Includes vertical offsets from transparent padding and differently sized pets.
    private bool CanMoveTo(Vector3 position)
    {
        if (spriteRenderer == null) return true;
        Bounds mine = VisibleBounds();
        mine.center += position - transform.position;
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled || other.spriteRenderer == null) continue;
            Bounds theirs = other.VisibleBounds();
            Vector2 delta = mine.center - theirs.center;
            float xGap = (mine.size.x + theirs.size.x) * .5f + .08f;
            float yGap = (mine.size.y + theirs.size.y) * .5f + .08f;
            if (Mathf.Abs(delta.x) < xGap && Mathf.Abs(delta.y) < yGap) return false;
        }
        return true;
    }

    private Bounds VisibleBounds()
    {
        if (!IsCollectionPet) return spriteRenderer.bounds;
        Transform visual = spriteRenderer.transform;
        Vector3 scale = visual.lossyScale;
        return new Bounds(visual.TransformPoint(collectionVisibleBounds.center),
            Vector3.Scale(collectionVisibleBounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f)));
    }

    private void ResolvePetOverlap()
    {
        Vector2 pushDirection = Vector2.zero;
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled)
                continue;

            if (spriteRenderer == null || other.spriteRenderer == null) continue;
            Bounds mine = VisibleBounds();
            Bounds theirs = other.VisibleBounds();
            Vector2 delta = mine.center - theirs.center;
            float overlapX = (mine.size.x + theirs.size.x) * .5f + .08f - Mathf.Abs(delta.x);
            float overlapY = (mine.size.y + theirs.size.y) * .5f + .08f - Mathf.Abs(delta.y);
            if (overlapX <= 0f || overlapY <= 0f) continue;
            float signX = Mathf.Abs(delta.x) > .001f ? Mathf.Sign(delta.x) : ActivePets.IndexOf(this) < i ? -1f : 1f;
            float signY = Mathf.Abs(delta.y) > .001f ? Mathf.Sign(delta.y) : ActivePets.IndexOf(this) < i ? -1f : 1f;
            // Separate along the shortest axis without changing Z or scale.
            pushDirection += overlapX < overlapY ? new Vector2(signX * overlapX, 0f) : new Vector2(0f, signY * overlapY);
        }

        if (pushDirection.sqrMagnitude <= 0.000001f)
            return;

        Vector3 position = transform.position;
        Vector2 push = pushDirection;
        position.x = Mathf.Clamp(position.x + push.x, walkMin.x, walkMax.x);
        position.y = Mathf.Clamp(position.y + push.y, walkMin.y, walkMax.y);
        transform.position = position;

        // Nudge the current route away too, otherwise the pet immediately walks
        // back into the same collision and appears to vibrate.
        if (isWalking)
        {
            StartPause(true);
        }
    }

    private void UpdateDepthSorting()
    {
        if (spriteRenderer == null)
            return;

        // Lower objects appear in front of higher objects. A stable list index is
        // used only as a tie-breaker when two pets have nearly the same Y value.
        int verticalOrder = Mathf.RoundToInt(-transform.position.y * 100f) * 10;
        int stableTieBreaker = Mathf.Max(0, ActivePets.IndexOf(this));
        spriteRenderer.sortingOrder = PetForegroundBaseOrder + sortingOrder + verticalOrder + stableTieBreaker;
    }

    private void UpdateFacing(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) < 0.001f)
            return;

        bool movingLeft = horizontalMovement < 0f;
        spriteRenderer.flipX = sourceFacesRight ? movingLeft : !movingLeft;
    }

    private void FitVisualToTargetHeight()
    {
        Sprite currentSprite = spriteRenderer.sprite;
        if (currentSprite == null || currentSprite.bounds.size.y <= 0.001f)
            return;

        float scale = targetHeight / currentSprite.bounds.size.y;
        Transform visualContainer = spriteRenderer.transform.parent;
        visualContainer.localScale = new Vector3(scale, scale, 1f);
    }
}
