using UnityEngine;

public static class PipeLevelSceneBuilder
{
    public const string DefaultRootName = "LevelPipes";
    private const float EndpointTileScale = 1.7f;

    public static Transform BuildLevel(PipeLevelData level, Transform parent, bool clearExistingPipes, AudioSource sharedAudioSource, AudioClip defaultRotateSound)
    {
        if (level == null)
        {
            return parent;
        }

        Transform root = parent != null ? parent : FindOrCreateRoot();

        if (clearExistingPipes)
        {
            ClearScenePipes(root, true);
        }
        else
        {
            ClearScenePipes(root, false);
        }

        for (int i = 0; i < level.pieces.Count; i++)
        {
            PipeLevelPiece piece = level.pieces[i];
            if (piece == null)
            {
                continue;
            }

            CreatePipe(level, piece, root, sharedAudioSource, defaultRotateSound);
        }

        return root;
    }

    public static void ClearScenePipes(Transform parent, bool includeWholeScene)
    {
        Blockin[] pipes = includeWholeScene
            ? Object.FindObjectsByType<Blockin>(FindObjectsInactive.Include)
            : parent != null
                ? parent.GetComponentsInChildren<Blockin>(true)
                : new Blockin[0];

        for (int i = pipes.Length - 1; i >= 0; i--)
        {
            Blockin pipe = pipes[i];
            if (pipe == null || !pipe.gameObject.scene.IsValid())
            {
                continue;
            }

            DestroyObject(pipe.gameObject);
        }
    }

    private static Transform FindOrCreateRoot()
    {
        GameObject existing = GameObject.Find(DefaultRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        return new GameObject(DefaultRootName).transform;
    }

    private static Blockin CreatePipe(PipeLevelData level, PipeLevelPiece piece, Transform parent, AudioSource sharedAudioSource, AudioClip defaultRotateSound)
    {
        GameObject pipeObject = new GameObject(GetObjectName(piece));
        pipeObject.transform.SetParent(parent, false);
        pipeObject.transform.position = level.CellToWorld(piece.x, piece.y);
        pipeObject.transform.rotation = Quaternion.Euler(0f, 0f, piece.rotationZ);
        pipeObject.transform.localScale = piece.scale;

        SpriteRenderer spriteRenderer = pipeObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = piece.emptySprite;
        if (piece.material != null)
        {
            spriteRenderer.sharedMaterial = piece.material;
        }

        if (!string.IsNullOrWhiteSpace(piece.sortingLayerName))
        {
            spriteRenderer.sortingLayerName = piece.sortingLayerName;
        }

        spriteRenderer.sortingOrder = piece.sortingOrder;
        CreateEndpointTile(level, piece, pipeObject.transform, spriteRenderer);

        BoxCollider2D boxCollider = pipeObject.AddComponent<BoxCollider2D>();
        boxCollider.offset = piece.colliderOffset;
        boxCollider.size = piece.colliderSize == Vector2.zero ? Vector2.one : piece.colliderSize;

        AudioSource audioSource = pipeObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        Blockin block = pipeObject.AddComponent<Blockin>();
        block.cellSize = level.cellSize;
        block.pipeColor = piece.pipeColor;
        if (piece.isStartingPipe)
        {
            block.SetFlowColor(piece.pipeColor);
        }
        else
        {
            block.ClearFlow();
        }

        block.isStartingPipe = piece.isStartingPipe;
        block.isEndingPipe = piece.isEndingPipe;
        block.isRotationLocked = piece.isRotationLocked;
        block.x = piece.x;
        block.y = piece.y;
        block.openings = PipeLevelUtility.CopyOpenings(piece.openings);
        block.emptySprite = piece.emptySprite;
        block.waterSprite = piece.waterSprite;
        block.redWaterSprite = piece.redWaterSprite;
        block.rotationLockSprite = level.rotationLockSprite;
        block.rotateSound = piece.rotateSound != null ? piece.rotateSound : defaultRotateSound;

        block.myAudioSource = sharedAudioSource != null ? sharedAudioSource : audioSource;

        pipeObject.transform.position = level.CellToWorld(piece.x, piece.y);
        block.UpdateVisual();
        return block;
    }

    private static void CreateEndpointTile(PipeLevelData level, PipeLevelPiece piece, Transform pipeTransform, SpriteRenderer pipeRenderer)
    {
        if (level == null || piece == null || pipeTransform == null || !piece.isEndingPipe)
        {
            return;
        }

        Sprite tileSprite = PipeLevelUtility.NormalizeFlowColor(piece.pipeColor) == PipeFlowColor.Red
            ? level.redEndpointTileSprite
            : level.blueEndpointTileSprite;
        if (tileSprite == null)
        {
            return;
        }

        GameObject tileObject = new GameObject("EndpointTile_" + PipeLevelUtility.FlowColorName(piece.pipeColor));
        tileObject.transform.SetParent(pipeTransform, false);
        tileObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        tileObject.transform.localRotation = Quaternion.identity;
        tileObject.transform.localScale = InverseScale(pipeTransform.localScale) * EndpointTileScale;

        SpriteRenderer tileRenderer = tileObject.AddComponent<SpriteRenderer>();
        tileRenderer.sprite = tileSprite;
        if (pipeRenderer != null)
        {
            tileRenderer.sharedMaterial = pipeRenderer.sharedMaterial;
            tileRenderer.sortingLayerName = pipeRenderer.sortingLayerName;
            tileRenderer.sortingOrder = pipeRenderer.sortingOrder - 1;
        }
    }

    private static Vector3 InverseScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }

    private static string GetObjectName(PipeLevelPiece piece)
    {
        if (piece == null || string.IsNullOrWhiteSpace(piece.displayName))
        {
            return "Pipe";
        }

        return piece.displayName;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
