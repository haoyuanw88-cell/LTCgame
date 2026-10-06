using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class LTCPetDefinition
{
    public string id;
    public string displayName;
    public int sheetColumn;
    public int sheetRow;
    public string rarity;
}

public sealed class LTCPetDrawResult
{
    public bool success;
    public string message;
    public LTCPetDefinition pet;
    public bool usedFreeTicket;
}

public static class LTCPetCollectionService
{
    public const int DrawCost = 100;
    const string OwnedPrefix = "LTC_PetOwned_";
    const string FreeTicketKey = "LTC_PetBlindBoxTickets";
    const string FirstGameTicketKey = "LTC_FirstGamePetTicketGranted";

    static readonly LTCPetDefinition[] Catalog =
    {
        NewPet("rabbit", "兔子", 0, 0),
        NewPet("dog", "小狗", 1, 0),
        NewPet("cat", "小貓", 2, 0),
        NewPet("shiba", "柴犬", 3, 0),
        NewPet("penguin", "企鵝", 4, 0),
        NewPet("chick", "小雞", 0, 1),
        NewPet("turtle", "烏龜", 1, 1),
        NewPet("hamster", "倉鼠", 2, 1),
        NewPet("snow_ferret", "雪貂", 3, 1),
        NewPet("fox", "小狐狸", 4, 1),
        NewPet("panda", "熊貓", 0, 2),
        NewPet("sheep", "綿羊", 1, 2),
        NewPet("deer", "小鹿", 2, 2),
        NewPet("raccoon", "浣熊", 3, 2),
        NewPet("dragon", "小龍", 4, 2)
    };

    static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();
    static Texture2D CachedPetSheet;
    static Color32[] CachedPetPixels;

    // Pixel-accurate cell boundaries measured from the 1536 x 1024 source sheet.
    // The artwork is not laid out on an even grid, so equal division clips heads and ears.
    static readonly int[][] ReferenceColumnEdges =
    {
        new[] { 0, 88, 161, 233, 307 },
        new[] { 307, 403, 471, 545, 614 },
        new[] { 614, 703, 772, 840, 922 },
        new[] { 922, 1000, 1072, 1143, 1229 },
        new[] { 1229, 1309, 1381, 1452, 1536 }
    };

    static readonly int[][] ReferenceRowEdges =
    {
        new[] { 38, 123, 206, 290 },
        new[] { 343, 425, 505, 589 },
        new[] { 639, 735, 824, 915, 1010 }
    };

    public static event Action Changed;

    public static IReadOnlyList<LTCPetDefinition> AllPets => Catalog;
    public static int FreeTickets => PlayerPrefs.GetInt(FreeTicketKey, 0);
    public static int OwnedCount => Catalog.Count(pet => IsOwned(pet.id));

    public static bool IsOwned(string petId)
    {
        return !string.IsNullOrWhiteSpace(petId) && PlayerPrefs.GetInt(OwnedPrefix + petId, 0) == 1;
    }

    public static IReadOnlyList<LTCPetDefinition> GetOwnedPets()
    {
        return Catalog.Where(pet => IsOwned(pet.id)).ToList();
    }

    public static LTCPetDrawResult Draw()
    {
        List<LTCPetDefinition> unowned = Catalog.Where(pet => !IsOwned(pet.id)).ToList();
        if (unowned.Count == 0)
            return Failed("你已經蒐集全部 15 種寵物！");

        bool useTicket = FreeTickets > 0;
        if (!useTicket && CoinData.TotalCoins < DrawCost)
            return Failed("金幣不足，需要 100 金幣才能抽取寵物盲盒。");

        LTCPetDefinition pet = unowned[UnityEngine.Random.Range(0, unowned.Count)];
        if (useTicket)
            PlayerPrefs.SetInt(FreeTicketKey, Mathf.Max(0, FreeTickets - 1));
        else
            CoinData.AddCoins(-DrawCost);

        PlayerPrefs.SetInt(OwnedPrefix + pet.id, 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return new LTCPetDrawResult
        {
            success = true,
            pet = pet,
            usedFreeTicket = useTicket,
            message = "恭喜獲得「" + pet.displayName + "」！"
        };
    }

    public static bool GrantFirstGameTicketOnce()
    {
        if (PlayerPrefs.GetInt(FirstGameTicketKey, 0) == 1) return false;
        PlayerPrefs.SetInt(FirstGameTicketKey, 1);
        PlayerPrefs.SetInt(FreeTicketKey, FreeTickets + 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    public static Sprite GetPreviewSprite(LTCPetDefinition pet)
    {
        Sprite[] frames = GetFrames(pet);
        return frames.Length == 0 ? null : frames[0];
    }

    public static Sprite[] GetFrames(LTCPetDefinition pet)
    {
        if (pet == null) return Array.Empty<Sprite>();
        if (FrameCache.TryGetValue(pet.id, out Sprite[] cached)) return cached;

        Texture2D texture = Resources.Load<Texture2D>("Pets/Gacha/pet_collection_sheet");
        if (texture == null)
        {
            Debug.LogError("找不到寵物盲盒圖集 Resources/Pets/Gacha/pet_collection_sheet");
            return Array.Empty<Sprite>();
        }

        int[] columnEdges = ReferenceColumnEdges[pet.sheetColumn];
        int[] rowEdges = ReferenceRowEdges[pet.sheetRow];
        int animationRows = rowEdges.Length - 1;
        float scaleX = texture.width / 1536f;
        float scaleY = texture.height / 1024f;
        var frames = new Sprite[animationRows * 4];
        int index = 0;

        for (int row = 0; row < animationRows; row++)
        {
            int top = Mathf.RoundToInt(rowEdges[row] * scaleY);
            int bottom = Mathf.RoundToInt(rowEdges[row + 1] * scaleY);
            for (int column = 0; column < 4; column++)
            {
                int left = Mathf.RoundToInt(columnEdges[column] * scaleX);
                int right = Mathf.RoundToInt(columnEdges[column + 1] * scaleX);
                int width = Mathf.Max(1, right - left);
                int height = Mathf.Max(1, bottom - top);
                int unityY = texture.height - bottom;
                frames[index] = CreateIsolatedPetFrame(texture, left, unityY, width, height,
                    pet.id + "_frame_" + index);
                index++;
            }
        }

        FrameCache[pet.id] = frames;
        return frames;
    }

    static Sprite CreateIsolatedPetFrame(Texture2D source, int searchX, int searchY,
        int searchWidth, int searchHeight, string spriteName)
    {
        if (CachedPetSheet != source || CachedPetPixels == null)
        {
            CachedPetSheet = source;
            CachedPetPixels = source.GetPixels32();
        }

        int textureWidth = source.width;
        int textureHeight = source.height;
        int centerX = searchX + searchWidth / 2;
        int centerY = searchY + searchHeight / 2;
        int seed = -1;
        int bestDistance = int.MaxValue;
        int maxSearchX = Mathf.Min(textureWidth, searchX + searchWidth);
        int maxSearchY = Mathf.Min(textureHeight, searchY + searchHeight);

        for (int y = Mathf.Max(0, searchY); y < maxSearchY; y++)
        {
            for (int x = Mathf.Max(0, searchX); x < maxSearchX; x++)
            {
                int candidate = y * textureWidth + x;
                if (CachedPetPixels[candidate].a <= 20) continue;
                int dx = x - centerX;
                int dy = y - centerY;
                int distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                seed = candidate;
            }
        }

        if (seed < 0)
        {
            Rect fallback = new Rect(searchX, searchY, searchWidth, searchHeight);
            Sprite fallbackSprite = Sprite.Create(source, fallback, new Vector2(0.5f, 0.5f), 100f);
            fallbackSprite.name = spriteName;
            return fallbackSprite;
        }

        var component = new HashSet<int>();
        var pending = new Queue<int>();
        component.Add(seed);
        pending.Enqueue(seed);
        int minX = seed % textureWidth;
        int maxX = minX;
        int minY = seed / textureWidth;
        int maxY = minY;

        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            int currentX = current % textureWidth;
            int currentY = current / textureWidth;
            minX = Mathf.Min(minX, currentX);
            maxX = Mathf.Max(maxX, currentX);
            minY = Mathf.Min(minY, currentY);
            maxY = Mathf.Max(maxY, currentY);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = currentX + offsetX;
                    int nextY = currentY + offsetY;
                    if (nextX < 0 || nextX >= textureWidth ||
                        nextY < Mathf.Max(0, searchY) || nextY >= maxSearchY)
                        continue;
                    int next = nextY * textureWidth + nextX;
                    if (CachedPetPixels[next].a <= 20 || !component.Add(next)) continue;
                    pending.Enqueue(next);
                }
            }
        }

        const int padding = 7;
        int outputWidth = maxX - minX + 1 + padding * 2;
        int outputHeight = maxY - minY + 1 + padding * 2;
        var isolated = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
        var isolatedPixels = new Color32[outputWidth * outputHeight];
        foreach (int sourceIndex in component)
        {
            int sourceX = sourceIndex % textureWidth;
            int sourceY = sourceIndex / textureWidth;
            int targetX = sourceX - minX + padding;
            int targetY = sourceY - minY + padding;
            isolatedPixels[targetY * outputWidth + targetX] = CachedPetPixels[sourceIndex];
        }

        isolated.SetPixels32(isolatedPixels);
        isolated.Apply(false, false);
        isolated.filterMode = FilterMode.Bilinear;
        isolated.wrapMode = TextureWrapMode.Clamp;
        isolated.name = spriteName + "_Texture";
        Sprite sprite = Sprite.Create(isolated, new Rect(0, 0, outputWidth, outputHeight),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        return sprite;
    }

    public static Sprite[] GetBlindBoxFrames()
    {
        const string cacheKey = "__blind_box";
        if (FrameCache.TryGetValue(cacheKey, out Sprite[] cached)) return cached;
        Texture2D source = Resources.Load<Texture2D>("Pets/Gacha/blind_box_sheet");
        if (source == null) return Array.Empty<Sprite>();
        Texture2D texture = CreateTransparentBlindBoxTexture(source);

        int cellWidth = texture.width / 4;
        int cellHeight = texture.height / 4;
        var frames = new Sprite[16];
        int index = 0;
        for (int row = 3; row >= 0; row--)
        {
            for (int column = 0; column < 4; column++)
            {
                frames[index++] = Sprite.Create(texture,
                    new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }
        FrameCache[cacheKey] = frames;
        return frames;
    }

    static Texture2D CreateTransparentBlindBoxTexture(Texture2D source)
    {
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            int brightest = Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, pixels[i].b));
            if (brightest < 70)
                pixels[i].a = (byte)(pixels[i].a * brightest / 70);
        }

        copy.SetPixels32(pixels);
        copy.Apply(false, false);
        copy.filterMode = FilterMode.Bilinear;
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.name = source.name + "_Transparent";
        return copy;
    }

    static LTCPetDefinition NewPet(string id, string name, int column, int row)
    {
        return new LTCPetDefinition
        {
            id = id, displayName = name, sheetColumn = column, sheetRow = row, rarity = "一般"
        };
    }

    static LTCPetDrawResult Failed(string message)
    {
        return new LTCPetDrawResult { success = false, message = message };
    }
}
