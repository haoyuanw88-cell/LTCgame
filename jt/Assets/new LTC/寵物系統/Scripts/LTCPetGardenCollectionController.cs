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
            var artwork = new GameObject("Artwork", typeof(SpriteRenderer));
            artwork.transform.SetParent(pet.transform, false);
            var frames = LTCPetCollectionService.GetFrames(definition);
            artwork.GetComponent<SpriteRenderer>().sprite = frames.Length > 0 ? frames[0] : null;
            var walker = pet.AddComponent<PetWander>();
            walker.ConfigureCollectionPet(definition, frames);
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

