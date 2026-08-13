using System;
using System.Collections.Generic;
using UnityEngine;

public enum PipeFlowColor
{
    None = 0,
    Blue = 1,
    Red = 2
}

[CreateAssetMenu(menuName = "FreeNet/Pipe Level", fileName = "PipeLevel")]
public class PipeLevelData : ScriptableObject
{
    [Min(1)] public int width = 12;
    [Min(1)] public int height = 12;
    [Min(0.1f)] public float cellSize = 2f;
    public Sprite rotationLockSprite;
    public Sprite blueEndpointTileSprite;
    public Sprite redEndpointTileSprite;
    public List<PipeLevelPiece> pieces = new List<PipeLevelPiece>();

    public Vector3 CellToWorld(int x, int y)
    {
        return new Vector3(x * cellSize, y * cellSize, 0f);
    }

    public PipeLevelPiece GetPiece(int x, int y)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            PipeLevelPiece piece = pieces[i];
            if (piece != null && piece.x == x && piece.y == y)
            {
                return piece;
            }
        }

        return null;
    }

    public void SetPiece(PipeLevelPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        RemovePiece(piece.x, piece.y);
        pieces.Add(piece);
        SortPieces();
    }

    public bool RemovePiece(int x, int y)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            PipeLevelPiece piece = pieces[i];
            if (piece != null && piece.x == x && piece.y == y)
            {
                pieces.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void SortPieces()
    {
        pieces.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int yCompare = a.y.CompareTo(b.y);
            return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
        });
    }

    public void ClampPiecesToBounds()
    {
        pieces.RemoveAll(piece =>
            piece == null ||
            piece.x < 0 ||
            piece.y < 0 ||
            piece.x >= width ||
            piece.y >= height);
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.1f, cellSize);

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
            {
                pieces[i].Validate();
            }
        }

        ClampPiecesToBounds();
        SortPieces();
    }
}

[Serializable]
public class PipeLevelPiece
{
    public string displayName = "Pipe";
    public int x;
    public int y;
    public bool isStartingPipe;
    public bool isEndingPipe;
    public bool isRotationLocked;
    public PipeFlowColor pipeColor = PipeFlowColor.Blue;
    public bool[] openings = new bool[4];

    public Sprite emptySprite;
    public Sprite waterSprite;
    public Sprite redWaterSprite;
    public Vector3 scale = Vector3.one;
    public float rotationZ;
    public Material material;
    public string sortingLayerName = "Default";
    public int sortingOrder;

    public bool hasCollider = true;
    public Vector2 colliderOffset = Vector2.zero;
    public Vector2 colliderSize = Vector2.one;

    public AudioClip rotateSound;

    public PipeLevelPiece Clone()
    {
        PipeLevelPiece clone = (PipeLevelPiece)MemberwiseClone();
        clone.openings = PipeLevelUtility.CopyOpenings(openings);
        return clone;
    }

    public void CopyFromBrush(PipeBrush brush, int targetX, int targetY, int clockwiseRotationSteps)
    {
        if (brush == null)
        {
            return;
        }

        displayName = brush.displayName;
        x = targetX;
        y = targetY;
        isStartingPipe = brush.isStartingPipe;
        isEndingPipe = brush.isEndingPipe;
        isRotationLocked = brush.isRotationLocked;
        pipeColor = PipeLevelUtility.NormalizeFlowColor(brush.pipeColor);
        openings = PipeLevelUtility.RotateOpenings(brush.openings, clockwiseRotationSteps);
        emptySprite = brush.emptySprite;
        waterSprite = brush.waterSprite;
        redWaterSprite = brush.redWaterSprite;
        scale = brush.scale;
        rotationZ = brush.rotationZ - 90f * PipeLevelUtility.NormalizeRotationSteps(clockwiseRotationSteps);
        material = brush.material;
        sortingLayerName = brush.sortingLayerName;
        sortingOrder = brush.sortingOrder;
        hasCollider = brush.hasCollider;
        colliderOffset = brush.colliderOffset;
        colliderSize = brush.colliderSize;
        rotateSound = brush.rotateSound;
        Validate();
    }

    public void CopyFromBlock(Blockin block)
    {
        if (block == null)
        {
            return;
        }

        displayName = block.name;
        x = block.x;
        y = block.y;
        isStartingPipe = block.isStartingPipe;
        isEndingPipe = block.isEndingPipe;
        isRotationLocked = block.IsRotationLocked();
        pipeColor = PipeLevelUtility.NormalizeFlowColor(block.pipeColor);
        openings = PipeLevelUtility.CopyOpenings(block.openings);
        emptySprite = block.emptySprite;
        waterSprite = block.waterSprite;
        redWaterSprite = block.redWaterSprite;
        scale = block.transform.localScale;
        rotationZ = block.transform.eulerAngles.z;
        rotateSound = block.rotateSound;

        SpriteRenderer spriteRenderer = block.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (emptySprite == null)
            {
                emptySprite = spriteRenderer.sprite;
            }

            material = spriteRenderer.sharedMaterial;
            sortingLayerName = spriteRenderer.sortingLayerName;
            sortingOrder = spriteRenderer.sortingOrder;
        }

        BoxCollider2D boxCollider = block.GetComponent<BoxCollider2D>();
        hasCollider = boxCollider != null;
        if (boxCollider != null)
        {
            colliderOffset = boxCollider.offset;
            colliderSize = boxCollider.size;
        }

        Validate();
    }

    public void Validate()
    {
        openings = PipeLevelUtility.CopyOpenings(openings);
        pipeColor = PipeLevelUtility.NormalizeFlowColor(pipeColor);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Pipe";
        }

        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            sortingLayerName = "Default";
        }

        if (scale == Vector3.zero)
        {
            scale = Vector3.one;
        }

        if (colliderSize == Vector2.zero)
        {
            colliderSize = Vector2.one;
        }
    }
}

[Serializable]
public class PipeBrush
{
    public string displayName = "Pipe";
    public bool isStartingPipe;
    public bool isEndingPipe;
    public bool isRotationLocked;
    public PipeFlowColor pipeColor = PipeFlowColor.Blue;
    public bool[] openings = new bool[4];

    public Sprite emptySprite;
    public Sprite waterSprite;
    public Sprite redWaterSprite;
    public Vector3 scale = Vector3.one;
    public float rotationZ;
    public Material material;
    public string sortingLayerName = "Default";
    public int sortingOrder;

    public bool hasCollider = true;
    public Vector2 colliderOffset = Vector2.zero;
    public Vector2 colliderSize = Vector2.one;

    public AudioClip rotateSound;

    public void CopyFromPiece(PipeLevelPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        displayName = piece.displayName;
        isStartingPipe = piece.isStartingPipe;
        isEndingPipe = piece.isEndingPipe;
        isRotationLocked = piece.isRotationLocked;
        pipeColor = PipeLevelUtility.NormalizeFlowColor(piece.pipeColor);
        openings = PipeLevelUtility.CopyOpenings(piece.openings);
        emptySprite = piece.emptySprite;
        waterSprite = piece.waterSprite;
        redWaterSprite = piece.redWaterSprite;
        scale = piece.scale;
        rotationZ = piece.rotationZ;
        material = piece.material;
        sortingLayerName = piece.sortingLayerName;
        sortingOrder = piece.sortingOrder;
        hasCollider = piece.hasCollider;
        colliderOffset = piece.colliderOffset;
        colliderSize = piece.colliderSize;
        rotateSound = piece.rotateSound;
        Validate();
    }

    public void CopyFromBlock(Blockin block)
    {
        PipeLevelPiece piece = new PipeLevelPiece();
        piece.CopyFromBlock(block);
        CopyFromPiece(piece);
    }

    public void Validate()
    {
        openings = PipeLevelUtility.CopyOpenings(openings);
        pipeColor = PipeLevelUtility.NormalizeFlowColor(pipeColor);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Pipe";
        }

        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            sortingLayerName = "Default";
        }

        if (scale == Vector3.zero)
        {
            scale = Vector3.one;
        }

        if (colliderSize == Vector2.zero)
        {
            colliderSize = Vector2.one;
        }
    }
}

public static class PipeLevelUtility
{
    public static PipeFlowColor NormalizeFlowColor(PipeFlowColor color)
    {
        return color == PipeFlowColor.Red ? PipeFlowColor.Red : PipeFlowColor.Blue;
    }

    public static string FlowColorName(PipeFlowColor color)
    {
        return NormalizeFlowColor(color) == PipeFlowColor.Red ? "Red" : "Blue";
    }

    public static bool[] CopyOpenings(bool[] source)
    {
        bool[] result = new bool[4];
        if (source == null)
        {
            return result;
        }

        int count = Mathf.Min(4, source.Length);
        for (int i = 0; i < count; i++)
        {
            result[i] = source[i];
        }

        return result;
    }

    public static bool[] RotateOpenings(bool[] source, int clockwiseRotationSteps)
    {
        bool[] result = CopyOpenings(source);
        int steps = NormalizeRotationSteps(clockwiseRotationSteps);

        for (int step = 0; step < steps; step++)
        {
            bool last = result[3];
            for (int i = 3; i > 0; i--)
            {
                result[i] = result[i - 1];
            }

            result[0] = last;
        }

        return result;
    }

    public static int NormalizeRotationSteps(int steps)
    {
        int normalized = steps % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }

    public static string OpeningsKey(bool[] openings)
    {
        bool[] copy = CopyOpenings(openings);
        return string.Format("{0}{1}{2}{3}", copy[0] ? 1 : 0, copy[1] ? 1 : 0, copy[2] ? 1 : 0, copy[3] ? 1 : 0);
    }
}
