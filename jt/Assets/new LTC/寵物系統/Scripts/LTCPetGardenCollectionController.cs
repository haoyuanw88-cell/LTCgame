using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LTCPetGardenCollectionController : MonoBehaviour
{
    const string SceneName = "PetGarden";
    readonly Dictionary<string, GameObject> spawnedPets = new Dictionary<string, GameObject>();
    readonly HashSet<string> authoredPets = new HashSet<string>();
    readonly Dictionary<string, PetWander> authoredPetObjects = new Dictionary<string, PetWander>();

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
        // Keep the authored pets, but treat every pet as a non-physical garden character.
        foreach (PetWander legacyPet in FindObjectsByType<PetWander>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            string petName = legacyPet.gameObject.name.ToLowerInvariant();
            string petId = null;
            if (petName.Contains("兔") || petName.Contains("rabbit")) petId = "rabbit";
            if (petName.Contains("貓") || petName.Contains("猫") || petName.Contains("cat")) petId = "cat";
            if (petId != null)
            {
                authoredPets.Add(petId);
                authoredPetObjects[petId] = legacyPet;
            }
            legacyPet.gameObject.SetActive(true);
            DisablePetPhysics(legacyPet.gameObject);
        }

        LTCPetCollectionService.Changed += RebuildGarden;
        RebuildGarden();
    }

    void OnDestroy() { LTCPetCollectionService.Changed -= RebuildGarden; }

    void RebuildGarden()
    {
        IReadOnlyList<LTCPetDefinition> owned = LTCPetCollectionService.GetOwnedPets();
        var ownedIds = new HashSet<string>();
        for (int i = 0; i < owned.Count; i++) ownedIds.Add(owned[i].id);

        foreach (KeyValuePair<string, PetWander> pair in authoredPetObjects)
            if (pair.Value != null) pair.Value.gameObject.SetActive(ownedIds.Contains(pair.Key));

        var removedIds = new List<string>();
        foreach (KeyValuePair<string, GameObject> pair in spawnedPets)
        {
            if (ownedIds.Contains(pair.Key)) continue;
            if (pair.Value != null) Destroy(pair.Value);
            removedIds.Add(pair.Key);
        }
        for (int i = 0; i < removedIds.Count; i++) spawnedPets.Remove(removedIds[i]);

        for (int i = 0; i < owned.Count; i++)
        {
            LTCPetDefinition definition = owned[i];
            if (authoredPets.Contains(definition.id)) continue;
            if (spawnedPets.TryGetValue(definition.id, out GameObject existing) && existing != null) continue;

            GameObject pet = new GameObject("盲盒寵物_" + definition.displayName);
            pet.transform.SetParent(transform, false);
            var artwork = new GameObject("Artwork", typeof(SpriteRenderer));
            artwork.transform.SetParent(pet.transform, false);
            Sprite[] frames = LTCPetCollectionService.GetFrames(definition);
            artwork.GetComponent<SpriteRenderer>().sprite = frames.Length > 0 ? frames[0] : null;
            PetWander walker = pet.AddComponent<PetWander>();
            walker.ConfigureCollectionPet(definition, frames);
            DisablePetPhysics(pet);
            spawnedPets[definition.id] = pet;
        }

        ArrangeGardenPets();
    }

    static void ArrangeGardenPets()
    {
        var pets = new List<PetWander>(FindObjectsByType<PetWander>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        pets.Sort((left, right) => string.CompareOrdinal(left.PetId, right.PetId));
        int count = pets.Count;
        if (count == 0) return;

        int columns = Mathf.Min(5, Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * 1.5f))));
        int rows = Mathf.CeilToInt((float)count / columns);
        float horizontalSpacing = columns <= 1 ? 0f : 7.3f / (columns - 1);
        float verticalSpacing = rows <= 1 ? 0f : 2.5f / (rows - 1);
        float petSize = count <= 4
            ? 1.65f
            : Mathf.Clamp(Mathf.Min(horizontalSpacing * .68f,
                rows <= 1 ? 1.45f : verticalSpacing * .78f), .82f, 1.45f);

        for (int row = 0, index = 0; row < rows; row++)
        {
            int rowCount = Mathf.Min(columns, count - index);
            float startX = -(rowCount - 1) * horizontalSpacing * .5f;
            float y = rows <= 1 ? -.45f : .75f - row * verticalSpacing;
            for (int column = 0; column < rowCount; column++, index++)
            {
                PetWander pet = pets[index];
                Vector3 slotCenter = new Vector3(startX + column * horizontalSpacing, y, 0f);
                pet.SetGardenStart(slotCenter, petSize);
                DisablePetPhysics(pet.gameObject);
            }
        }
    }

    static void DisablePetPhysics(GameObject pet)
    {
        foreach (Collider2D collider in pet.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
        foreach (Rigidbody2D body in pet.GetComponentsInChildren<Rigidbody2D>(true))
            body.simulated = false;
    }
}


