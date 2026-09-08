using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime movement only. Visual frames are authored in an AnimatorController,
/// so the player never creates Sprite objects or loads texture sheets at runtime.
/// </summary>
public sealed class PetWander : MonoBehaviour
{
    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly List<PetWander> ActivePets = new List<PetWander>();
    private const int PetForegroundBaseOrder = 5000;

    [Header("Movement area")]
    [SerializeField] private Vector2 walkMin = new Vector2(-4.2f, -2.2f);
    [SerializeField] private Vector2 walkMax = new Vector2(4.2f, 1.1f);
    [SerializeField, Min(0.05f)] private float walkSpeed = 0.75f;
    [SerializeField, Min(0f)] private float minimumPause = 0.8f;
    [SerializeField, Min(0f)] private float maximumPause = 2.3f;

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

        if (spriteRenderer == null || animator == null)
        {
            Debug.LogError("Pet visual hierarchy is incomplete. A child SpriteRenderer and Animator are required.", this);
            enabled = false;
            return;
        }

        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.enabled = true;
        animator.enabled = true;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == WalkingHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                usesWalkingParameter = true;
                break;
            }
        }
        FitVisualToTargetHeight();
    }

    private void OnEnable()
    {
        if (!ActivePets.Contains(this))
            ActivePets.Add(this);
        StartPause();
    }

    private void OnDisable()
    {
        ActivePets.Remove(this);
    }

    private void Update()
    {
        ResolvePetOverlap();
        UpdateDepthSorting();

        if (!isWalking)
        {
            if (Time.time >= resumeWalkingAt)
                ChooseDestination();
            return;
        }

        Vector3 beforeMove = transform.position;
        transform.position = Vector3.MoveTowards(beforeMove, destination, walkSpeed * Time.deltaTime);
        UpdateFacing(destination.x - beforeMove.x);

        if (Vector3.SqrMagnitude(transform.position - destination) <= 0.0025f)
            StartPause();
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
        else
            animator.speed = 1f;
    }

    private void StartPause()
    {
        isWalking = false;
        if (usesWalkingParameter)
        {
            animator.SetBool(WalkingHash, false);
        }
        else
        {
            animator.Play(0, 0, 0f);
            animator.speed = 0f;
        }
        resumeWalkingAt = Time.time + Random.Range(minimumPause, maximumPause);
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

    private void ResolvePetOverlap()
    {
        Vector2 pushDirection = Vector2.zero;
        for (int i = 0; i < ActivePets.Count; i++)
        {
            PetWander other = ActivePets[i];
            if (other == null || other == this || !other.isActiveAndEnabled)
                continue;

            Vector2 delta = (Vector2)(transform.position - other.transform.position);
            float distance = delta.magnitude;
            if (distance >= separationRadius)
                continue;

            Vector2 away;
            if (distance <= 0.001f)
                away = ActivePets.IndexOf(this) < i ? Vector2.left : Vector2.right;
            else
                away = delta / distance;

            pushDirection += away * (separationRadius - distance);
        }

        if (pushDirection.sqrMagnitude <= 0.000001f)
            return;

        Vector3 position = transform.position;
        Vector2 push = Vector2.ClampMagnitude(pushDirection * separationSpeed, separationSpeed) * Time.deltaTime;
        position.x = Mathf.Clamp(position.x + push.x, walkMin.x, walkMax.x);
        position.y = Mathf.Clamp(position.y + push.y, walkMin.y, walkMax.y);
        transform.position = position;

        // Nudge the current route away too, otherwise the pet immediately walks
        // back into the same collision and appears to vibrate.
        if (isWalking)
        {
            Vector2 routeCorrection = pushDirection.normalized * separationRadius * 0.35f;
            destination.x = Mathf.Clamp(destination.x + routeCorrection.x, walkMin.x, walkMax.x);
            destination.y = Mathf.Clamp(destination.y + routeCorrection.y, walkMin.y, walkMax.y);
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
