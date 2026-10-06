using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LTCPetGardenCollectionController : MonoBehaviour
{
    const string SceneName = "PetGarden";
    readonly Dictionary<string, GameObject> spawnedPets = new Dictionary<string, GameObject>();
    readonly HashSet<string> authoredPets = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { TryCreate(scene); }

    static void TryCreate(Scene scene)
    {
        if (scene.name != SceneName || FindFirstObjectByType<LTCPetGardenCollectionController>() != null) return;
        new GameObject("LTC Pet Collection").AddComponent<LTCPetGardenCollectionController>();
    }

    void Start()
    {
        // Preserve the scene's original, high-resolution pets and their animation setup.
        foreach (PetWander legacyPet in FindObjectsByType<PetWander>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            string petName = legacyPet.gameObject.name.ToLowerInvariant();
            if (petName.Contains("兔") || petName.Contains("rabbit")) authoredPets.Add("rabbit");
            if (petName.Contains("貓") || petName.Contains("猫") || petName.Contains("cat")) authoredPets.Add("cat");
            legacyPet.gameObject.SetActive(true);
        }

        LTCPetCollectionService.Changed += RebuildGarden;
        RebuildGarden();
    }

    void OnDestroy() { LTCPetCollectionService.Changed -= RebuildGarden; }

    void RebuildGarden()
    {
        IReadOnlyList<LTCPetDefinition> owned = LTCPetCollectionService.GetOwnedPets();
        for (int i = 0; i < owned.Count; i++)
        {
            LTCPetDefinition definition = owned[i];
            // A draw must not reset existing pets, their positions or animation phase.
            if (authoredPets.Contains(definition.id)) continue;
            if (spawnedPets.TryGetValue(definition.id, out GameObject existing) && existing != null) continue;
            GameObject pet = new GameObject("盲盒寵物_" + definition.displayName);
            pet.transform.SetParent(transform, false);
            pet.transform.position = InitialPosition(i, owned.Count);
            var walker = pet.AddComponent<LTCPetSpriteWalker>();
            walker.Initialize(definition, i);
            spawnedPets[definition.id] = pet;
        }
    }

    static Vector3 InitialPosition(int index, int count)
    {
        float t = count <= 1 ? 0.5f : (float)index / (count - 1);
        float x = Mathf.Lerp(-4f, 4f, t);
        float y = -1.7f + (index % 3) * 0.75f;
        return new Vector3(x, y, 0f);
    }
}

[RequireComponent(typeof(SpriteRenderer))]
sealed class LTCPetSpriteWalker : MonoBehaviour
{
    SpriteRenderer rendererComponent;
    Sprite[] frames;
    int frameIndex;
    float nextFrameAt;
    Vector3 destination;
    float pauseUntil;
    float speed;
    bool walking;
    bool sourceFacesRight;

    public void Initialize(LTCPetDefinition definition, int stableIndex)
    {
        rendererComponent = GetComponent<SpriteRenderer>();
        frames = LTCPetCollectionService.GetFrames(definition);
        rendererComponent.sprite = frames.Length == 0 ? null : frames[0];
        rendererComponent.sortingOrder = 5000 + stableIndex;
        sourceFacesRight = definition.id != "cat";
        speed = Random.Range(0.55f, 0.85f);
        FitHeight(1.55f);
        StartPause();
    }

    void Update()
    {
        if (PetCollectionBook.ModalOpen || frames == null || frames.Length == 0) return;
        UpdateDepth();
        if (!walking)
        {
            if (Time.time >= pauseUntil) ChooseDestination();
            return;
        }

        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(before, destination, speed * Time.deltaTime);
        float dx = destination.x - before.x;
        if (Mathf.Abs(dx) > 0.001f)
            rendererComponent.flipX = sourceFacesRight ? dx < 0f : dx > 0f;
        Animate();

        if ((transform.position - destination).sqrMagnitude < 0.003f)
            StartPause();
    }

    void ChooseDestination()
    {
        destination = new Vector3(Random.Range(-4.2f, 4.2f), Random.Range(-2.1f, 1.0f), 0f);
        walking = true;
        nextFrameAt = Time.time;
    }

    void StartPause()
    {
        walking = false;
        frameIndex = 0;
        if (frames != null && frames.Length > 0) rendererComponent.sprite = frames[0];
        pauseUntil = Time.time + Random.Range(3f, 5f);
    }

    void Animate()
    {
        if (Time.time < nextFrameAt) return;
        frameIndex = (frameIndex + 1) % frames.Length;
        rendererComponent.sprite = frames[frameIndex];
        nextFrameAt = Time.time + 0.11f;
    }

    void FitHeight(float targetHeight)
    {
        if (rendererComponent.sprite == null || rendererComponent.sprite.bounds.size.y <= 0.001f) return;
        Sprite sprite = rendererComponent.sprite;
        // Transparent animation padding must not make newly collected pets tiny.
        Color32[] pixels = sprite.texture.GetPixels32();
        int minY = sprite.texture.height;
        int maxY = -1;
        for (int y = 0; y < sprite.texture.height; y++)
            for (int x = 0; x < sprite.texture.width; x++)
                if (pixels[y * sprite.texture.width + x].a > 20)
                {
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
        float visibleHeight = maxY >= minY ? (maxY - minY + 1) / sprite.pixelsPerUnit : sprite.bounds.size.y;
        float scale = targetHeight / visibleHeight;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    void UpdateDepth()
    {
        rendererComponent.sortingOrder = 5000 + Mathf.RoundToInt(-transform.position.y * 100f);
    }
}
