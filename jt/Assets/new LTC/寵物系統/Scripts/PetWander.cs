using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wandering and local avoidance, with authored or collection sprite animation.
/// </summary>
public sealed class PetWander : MonoBehaviour
{
    public bool IsWalking => isWalking;
    public string PetId => !string.IsNullOrEmpty(collectionId) ? collectionId : name.Contains("貓") ? "cat" : name.Contains("兔") ? "rabbit" : name;
    public string DisplayName => !string.IsNullOrEmpty(collectionName) ? collectionName : PetId == "cat" ? "貓咪" : PetId == "rabbit" ? "兔子" : name;
    public Sprite Portrait => idleSprite != null ? idleSprite : spriteRenderer == null ? null : spriteRenderer.sprite;
    public bool IsCollectionPet => !string.IsNullOrEmpty(collectionId);
    string collectionId, collectionName;
    Sprite[] collectionFrames;
    Bounds collectionVisibleBounds;
    Sprite idleSprite, authoredWalkingSprite;
    Vector3 walkingScale, walkingOffset, idleScale, idleOffset;
    bool displayingIdle;
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
    private Vector2 blockedHeading;
    private float blockedHeadingUntil;
    private float nextAvoidanceRetry;

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
        if (LTCPetCollectionService.HasUnifiedSheet(PetId))
        {
            foreach (LTCPetDefinition pet in LTCPetCollectionService.AllPets)
                if (pet.id == PetId)
                {
                    if (animator != null) animator.enabled = false;
                    usesWalkingParameter = false;
                    ConfigureCollectionPet(pet, LTCPetCollectionService.GetFrames(pet));
                    return;
                }
        }
        if (animator != null) FitVisualToTargetHeight();
        if (animator != null) SetupIdleVisual();
    }

    public void ConfigureCollectionPet(LTCPetDefinition definition, Sprite[] frames)
    {
        collectionId = definition.id;
        collectionName = definition.displayName;
        collectionFrames = frames;
        sourceFacesRight = LTCPetCollectionService.HasUnifiedSheet(definition.id) || definition.id != "cat";
        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
            Color32[] pixels = frames[0].texture.GetPixels32();
            int min = frames[0].texture.height, max = -1, minX = frames[0].texture.width, maxX = -1;
            for (int y = 0; y < frames[0].texture.height; y++)
                for (int x = 0; x < frames[0].texture.width; x++)
                    if (pixels[y * frames[0].texture.width + x].a > 20) { min = Mathf.Min(min, y); max = Mathf.Max(max, y); minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            float visibleHeight = max >= min ? (max - min + 1) / frames[0].pixelsPerUnit : frames[0].bounds.size.y;
            float visibleWidth = (maxX - minX + 1f) / frames[0].pixelsPerUnit;
            float scale = targetHeight / Mathf.Max(visibleHeight, visibleWidth);
            // Authored starter pets may have a scaled Visual parent. Normalize
            // the visible character in WORLD units, not just its child scale.
            Vector3 inherited = spriteRenderer.transform.parent != null
                ? spriteRenderer.transform.parent.lossyScale : Vector3.one;
            spriteRenderer.transform.localScale = new Vector3(
                scale / Mathf.Max(.001f, Mathf.Abs(inherited.x)),
                scale / Mathf.Max(.001f, Mathf.Abs(inherited.y)), 1f);
            float ppu = frames[0].pixelsPerUnit;
            collectionVisibleBounds = new Bounds(new Vector3((minX + maxX + 1f) * .5f / ppu - frames[0].bounds.extents.x,
                (min + max + 1f) * .5f / ppu - frames[0].bounds.extents.y, 0f),
                new Vector3((maxX - minX + 1f) / ppu, visibleHeight, .01f));
        }
        PetHungerService.RemainingSeconds(PetId);
        SetupIdleVisual();
        StartPause(false);
    }

    private void SetupIdleVisual()
    {
        authoredWalkingSprite = spriteRenderer.sprite;
        walkingScale = spriteRenderer.transform.localScale;
        walkingOffset = spriteRenderer.transform.localPosition;
        idleSprite = PetId == "rabbit" ? authoredWalkingSprite : LTCPetCollectionService.GetIdleSprite(PetId);
        if (idleSprite == null) return;
        Bounds walk = LTCPetCollectionService.GetVisibleBounds(authoredWalkingSprite);
        Bounds idle = LTCPetCollectionService.GetVisibleBounds(idleSprite);
        // Sitting front poses are tall while side-view cats/ferrets are long.
        // Match the head, not the total bounding box, to avoid a sudden growth.
        float factor = LTCPetCollectionService.HasUnifiedSheet(PetId) || PetId == "rabbit" ? 1f : LTCPetCollectionService.GetHeadWidth(authoredWalkingSprite) /
            Mathf.Max(.01f, LTCPetCollectionService.GetHeadWidth(idleSprite));
        idleScale = new Vector3(walkingScale.x * factor,walkingScale.y * factor,walkingScale.z);
        idleOffset = walkingOffset;
        idleOffset.y += walk.min.y * walkingScale.y - idle.min.y * idleScale.y;
    }

    // Animator updates after Update; apply the separate front pose afterwards.
    private void LateUpdate()
    {
        if (spriteRenderer == null || idleSprite == null) return;
        if (!isWalking)
        {
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.flipX = false;
            spriteRenderer.transform.localScale = idleScale;
            spriteRenderer.transform.localPosition = idleOffset;
            displayingIdle = true;
        }
    }

    private void RestoreWalkingVisual()
    {
        if (!displayingIdle) return;
        spriteRenderer.sprite = collectionFrames != null && collectionFrames.Length > 0 ? collectionFrames[0] : authoredWalkingSprite;
        spriteRenderer.transform.localScale = walkingScale;
        spriteRenderer.transform.localPosition = walkingOffset;
        displayingIdle = false;
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
            // Collision is a route change, not the end of a walk. Keep the
            // walking animation and do not reset its frame or rest timer.
            if (Time.time >= nextAvoidanceRetry)
                RedirectAfterCollision(destination - beforeMove);
            nextPosition = Vector3.MoveTowards(beforeMove, destination, walkSpeed * Time.deltaTime);
            if (!CanMoveTo(nextPosition)) return; // Fully surrounded: never walk through a pet.
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

            if (Vector2.Distance(candidate, transform.position) >= 1f && CanTraverse(transform.position, candidate))
            {
                BeginWalk(candidate);
                return;
            }
        }
        // Random destinations can all be behind obstacles. Search headings
        // instead of accepting the last unsafe random candidate.
        if (!TryFindClearDirection(Vector2.right, out candidate))
        {
            resumeWalkingAt = Time.time + .2f;
            return;
        }
        BeginWalk(candidate);
    }

    private void BeginWalk(Vector3 candidate)
    {
        destination = candidate;
        RestoreWalkingVisual();
        isWalking = true;
        if (usesWalkingParameter)
            animator.SetBool(WalkingHash, true);
        else if (animator != null)
            animator.speed = 1f;
    }

    private bool RedirectAfterCollision(Vector3 rejectedDirection)
    {
        blockedHeading = ((Vector2)rejectedDirection).normalized;
        blockedHeadingUntil = Time.time + 1f;
        nextAvoidanceRetry = Time.time + .12f;
        Vector3 candidate;
        if (!TryFindClearDirection(blockedHeading, out candidate)) return false;
        destination = candidate;
        UpdateFacing(destination.x - transform.position.x);
        return true;
    }

    private bool TryFindClearDirection(Vector2 reference, out Vector3 candidate)
    {
        candidate = transform.position;
        if (reference.sqrMagnitude < .001f) reference = Vector2.right;
        float baseAngle = Mathf.Atan2(reference.y, reference.x);
        // Stable left/right preference avoids frame-to-frame random shaking.
        float side = ActivePets.IndexOf(this) % 2 == 0 ? 1f : -1f;
        for (int distancePass = 0; distancePass < 3; distancePass++)
        {
            float distance = distancePass == 0 ? 1.5f : distancePass == 1 ? .6f : .2f;
            for (int step = 0; step < 24; step++)
            {
                int offset = step == 0 ? 0 : (step + 1) / 2 * (step % 2 == 1 ? 1 : -1);
                float angle = baseAngle + offset * 15f * side * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Time.time < blockedHeadingUntil && Vector2.Dot(direction, blockedHeading) > .85f) continue;
                Vector3 end = transform.position + (Vector3)(direction * distance);
                end.x = Mathf.Clamp(end.x, walkMin.x, walkMax.x);
                end.y = Mathf.Clamp(end.y, walkMin.y, walkMax.y);
                Vector2 actualDirection = (Vector2)(end - transform.position);
                if (actualDirection.magnitude < .1f) continue;
                // Clamping at the garden edge must not bend a safe heading
                // back into the direction that just hit another pet.
                if (Time.time < blockedHeadingUntil && Vector2.Dot(actualDirection.normalized, blockedHeading) > .85f) continue;
                if (!CanTraverse(transform.position, end)) continue;
                candidate = end;
                return true;
            }
        }
        return false;
    }

    private bool CanTraverse(Vector3 start, Vector3 end)
    {
        // Check the whole route, not just its destination. Short steps prevent
        // choosing an endpoint on the other side of an intervening pet.
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / .08f));
        for (int i = 1; i <= steps; i++)
            if (!CanMoveTo(Vector3.Lerp(start, end, (float)i / steps))) return false;
        return true;
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
        Transform visual = spriteRenderer.transform;
        Vector3 scale = visual.lossyScale;
        Bounds bounds = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        return new Bounds(visual.TransformPoint(bounds.center),
            Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f)));
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
        Vector3 rejectedDirection = destination - position;
        Vector2 push = pushDirection;
        position.x = Mathf.Clamp(position.x + push.x, walkMin.x, walkMax.x);
        position.y = Mathf.Clamp(position.y + push.y, walkMin.y, walkMax.y);
        transform.position = position;

        // Keep walking, but replace the blocked route after separation.
        if (isWalking)
        {
            RedirectAfterCollision(rejectedDirection);
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

        Transform visualContainer = spriteRenderer.transform.parent;
        Bounds visible = LTCPetCollectionService.GetVisibleBounds(currentSprite);
        Vector3 worldScale = spriteRenderer.transform.lossyScale;
        float worldSize = Mathf.Max(visible.size.x * Mathf.Abs(worldScale.x), visible.size.y * Mathf.Abs(worldScale.y));
        float factor = targetHeight / Mathf.Max(.01f, worldSize);
        Vector3 scale = visualContainer.localScale;
        visualContainer.localScale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
    }
}
