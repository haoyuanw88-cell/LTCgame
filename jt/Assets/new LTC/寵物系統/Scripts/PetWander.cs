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

    public void SetGardenVisualSize(float maxWorldSize)
    {
        targetHeight = Mathf.Clamp(maxWorldSize, .7f, 2.05f);
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        Bounds visible = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        Vector3 worldScale = spriteRenderer.transform.lossyScale;
        float currentSize = Mathf.Max(
            visible.size.x * Mathf.Abs(worldScale.x),
            visible.size.y * Mathf.Abs(worldScale.y));
        float factor = targetHeight / Mathf.Max(.01f, currentSize);
        Vector3 scale = spriteRenderer.transform.localScale;
        spriteRenderer.transform.localScale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
        separationRadius = Mathf.Max(.78f, targetHeight * 1.08f);
        SetupIdleVisual();
    }

    public void SetGardenStart(Vector3 center, float maxWorldSize)
    {
        transform.position = center;
        SetGardenVisualSize(maxWorldSize);

        // All pets share the complete garden and may freely pass through
        // one another. There are no lanes, avoidance or physics collisions.
        walkMin = new Vector2(-4.2f, -2.2f);
        walkMax = new Vector2(4.2f, 1.1f);
        walkSpeed = Random.Range(.88f, 1.08f);
        minimumPause = .2f;
        maximumPause = .8f;
        minimumPauseAfterWalk = .65f;
        maximumPauseAfterWalk = 1.5f;
        StartPause(false);

        // Align the actual opaque artwork to the initial slot, not its root pivot.
        if (idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.flipX = false;
            spriteRenderer.transform.localScale = idleScale;
            spriteRenderer.transform.localPosition = idleOffset;
            displayingIdle = true;
        }
        ClampRenderedSize();
        Bounds visible = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        Vector3 visibleCenter = spriteRenderer.transform.TransformPoint(visible.center);
        transform.position += new Vector3(center.x - visibleCenter.x, center.y - visibleCenter.y, 0f);
        destination = transform.position;
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
        // Match the head between poses, then cap the idle pose to the same
        // world-space size so a wide front sprite cannot cover nearby pets.
        float factor = LTCPetCollectionService.HasUnifiedSheet(PetId) || PetId == "rabbit" ? 1f : LTCPetCollectionService.GetHeadWidth(authoredWalkingSprite) /
            Mathf.Max(.01f, LTCPetCollectionService.GetHeadWidth(idleSprite));
        Vector3 parentScale = spriteRenderer.transform.parent != null
            ? spriteRenderer.transform.parent.lossyScale : Vector3.one;
        float idleWorldSize = Mathf.Max(
            idle.size.x * Mathf.Abs(parentScale.x * walkingScale.x),
            idle.size.y * Mathf.Abs(parentScale.y * walkingScale.y));
        factor = Mathf.Min(factor, targetHeight / Mathf.Max(.01f, idleWorldSize));
        idleScale = new Vector3(walkingScale.x * factor, walkingScale.y * factor, walkingScale.z);
        idleOffset = walkingOffset;
        idleOffset.y += walk.min.y * walkingScale.y - idle.min.y * idleScale.y;
    }

    // Animator updates after Update; apply the separate front pose afterwards.
    private void LateUpdate()
    {
        if (spriteRenderer == null) return;
        if (idleSprite != null && !isWalking)
        {
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.flipX = false;
            spriteRenderer.transform.localScale = idleScale;
            spriteRenderer.transform.localPosition = idleOffset;
            displayingIdle = true;
        }
        ClampRenderedSize();
    }
    private void ClampRenderedSize()
    {
        if (spriteRenderer.sprite == null) return;
        Bounds visible = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        Vector3 worldScale = spriteRenderer.transform.lossyScale;
        float worldSize = Mathf.Max(
            visible.size.x * Mathf.Abs(worldScale.x),
            visible.size.y * Mathf.Abs(worldScale.y));
        if (worldSize <= targetHeight * 1.001f) return;

        float factor = targetHeight / Mathf.Max(.01f, worldSize);
        Vector3 scale = spriteRenderer.transform.localScale;
        scale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
        spriteRenderer.transform.localScale = scale;
        if (isWalking)
            walkingScale = scale;
        else
            idleScale = scale;
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
        if (PetCollectionBook.ModalOpen) return;
        UpdateDepthSorting();


        if (!isWalking)
        {
            if (Time.time >= resumeWalkingAt)
                ChooseDestination();
            return;
        }

        Vector3 beforeMove = transform.position;
        Vector2 heading = ((Vector2)(destination - beforeMove)).normalized;


        float step = walkSpeed * Time.deltaTime;
        Vector3 nextPosition = beforeMove + (Vector3)(heading * step);
        nextPosition.x = Mathf.Clamp(nextPosition.x, walkMin.x, walkMax.x);
        nextPosition.y = Mathf.Clamp(nextPosition.y, walkMin.y, walkMax.y);
        transform.position = nextPosition;
        UpdateFacing(nextPosition.x - beforeMove.x);

        if (collectionFrames != null && collectionFrames.Length > 0 && Time.time >= nextCollectionFrame)
        {
            collectionFrame = (collectionFrame + 1) % collectionFrames.Length;
            spriteRenderer.sprite = collectionFrames[collectionFrame];
            nextCollectionFrame = Time.time + .11f;
        }

        if (Vector2.Distance(transform.position, destination) <= Mathf.Max(.08f, step * 1.5f))
            StartPause(true);
    }

    private void ChooseDestination()
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(walkMin.x, walkMax.x),
                Random.Range(walkMin.y, walkMax.y),
                transform.position.z);
            if (Vector2.Distance(candidate, transform.position) < 1f) continue;
            BeginWalk(candidate);
            return;
        }
        resumeWalkingAt = Time.time + .1f;
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
        // Pets do not collide or block paths. Only avoid selecting an occupied destination.
        return DestinationHasSpace(end);
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

    private bool DestinationHasSpace(Vector3 position)
    {
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled) continue;
            if (Vector2.Distance(position, other.transform.position) < separationRadius * 1.05f)
                return false;
        }
        return true;
    }

    private Vector2 CalculateSeparation(Vector3 position)
    {
        Vector2 steering = Vector2.zero;
        int myIndex = Mathf.Max(0, ActivePets.IndexOf(this));
        Vector2 myCenter = CurrentVisualCenter() + ((Vector2)position - (Vector2)transform.position);
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled) continue;

            Vector2 delta = myCenter - other.CurrentVisualCenter();
            float distance = delta.magnitude;
            float safeDistance = Mathf.Max(.9f, (targetHeight + other.targetHeight) * .62f);
            if (distance >= safeDistance) continue;
            if (distance < .001f)
                delta = (myIndex + i) % 2 == 0 ? Vector2.right : Vector2.left;
            else
                delta /= distance;
            steering += delta * (1f - distance / safeDistance);
        }
        return Vector2.ClampMagnitude(steering, 1f);
    }

    private Vector2 CurrentVisualCenter()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return transform.position;
        Bounds visible = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        return spriteRenderer.transform.TransformPoint(visible.center);
    }

    private Bounds CurrentVisibleBounds()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return new Bounds(transform.position, Vector3.zero);
        Bounds local = LTCPetCollectionService.GetVisibleBounds(spriteRenderer.sprite);
        Vector3 scale = spriteRenderer.transform.lossyScale;
        return new Bounds(spriteRenderer.transform.TransformPoint(local.center),
            Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f)));
    }

    private void ResolveVisualCrowding()
    {
        Bounds mine = CurrentVisibleBounds();
        Vector2 correction = Vector2.zero;
        int myIndex = Mathf.Max(0, ActivePets.IndexOf(this));
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled) continue;
            Bounds theirs = other.CurrentVisibleBounds();
            Vector2 delta = mine.center - theirs.center;
            float overlapX = (mine.size.x + theirs.size.x) * .5f + .035f - Mathf.Abs(delta.x);
            float overlapY = (mine.size.y + theirs.size.y) * .5f + .035f - Mathf.Abs(delta.y);
            if (overlapX <= 0f || overlapY <= 0f) continue;

            if (overlapX < overlapY)
                correction.x += (Mathf.Abs(delta.x) > .001f ? Mathf.Sign(delta.x) : myIndex < i ? -1f : 1f) * overlapX;
            else
                correction.y += (Mathf.Abs(delta.y) > .001f ? Mathf.Sign(delta.y) : myIndex < i ? -1f : 1f) * overlapY;
        }

        correction = Vector2.ClampMagnitude(correction, .28f);
        if (correction.sqrMagnitude < .000001f) return;
        Vector3 position = transform.position + (Vector3)correction;
        position.x = Mathf.Clamp(position.x, walkMin.x, walkMax.x);
        position.y = Mathf.Clamp(position.y, walkMin.y, walkMax.y);
        transform.position = position;
    }

    private void ApplyGentleSeparation()
    {
        Vector2 steering = CalculateSeparation(transform.position);
        if (steering.sqrMagnitude < .0001f) return;
        Vector3 position = transform.position + (Vector3)(steering * separationSpeed * .52f * Time.deltaTime);
        position.x = Mathf.Clamp(position.x, walkMin.x, walkMax.x);
        position.y = Mathf.Clamp(position.y, walkMin.y, walkMax.y);
        transform.position = position;
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
