using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LTCPetGardenCollectionController : MonoBehaviour
{
    const string SceneName = "PetGarden";
    readonly List<GameObject> spawnedPets = new List<GameObject>();

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
        // The two showcase pets authored in the original scene are no longer free defaults.
        // Every visible pet now comes from blind-box ownership data.
        foreach (PetWander legacyPet in FindObjectsByType<PetWander>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            legacyPet.gameObject.SetActive(false);
        foreach (PetIdleSprite legacyPet in FindObjectsByType<PetIdleSprite>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            legacyPet.gameObject.SetActive(false);

        LTCPetCollectionService.Changed += RebuildGarden;
        RebuildGarden();
    }

    void OnDestroy() { LTCPetCollectionService.Changed -= RebuildGarden; }

    void RebuildGarden()
    {
        foreach (GameObject pet in spawnedPets)
            if (pet != null) Destroy(pet);
        spawnedPets.Clear();

        IReadOnlyList<LTCPetDefinition> owned = LTCPetCollectionService.GetOwnedPets();
        for (int i = 0; i < owned.Count; i++)
        {
            LTCPetDefinition definition = owned[i];
            GameObject pet = new GameObject("盲盒寵物_" + definition.displayName);
            pet.transform.SetParent(transform, false);
            pet.transform.position = InitialPosition(i, owned.Count);
            var walker = pet.AddComponent<LTCPetSpriteWalker>();
            walker.Initialize(definition, i);
            spawnedPets.Add(pet);
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
        if (frames == null || frames.Length == 0) return;
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
        pauseUntil = Time.time + Random.Range(0.8f, 2.4f);
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
        float scale = targetHeight / rendererComponent.sprite.bounds.size.y;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    void UpdateDepth()
    {
        rendererComponent.sortingOrder = 5000 + Mathf.RoundToInt(-transform.position.y * 100f);
    }
}
