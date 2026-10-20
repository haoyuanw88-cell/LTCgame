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
    const string StarterPetsClearedKey = "LTC_StarterPetsClearedForTesting";

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
    static readonly Dictionary<string, Sprite> IdleCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite[]> UnifiedCache = new Dictionary<string, Sprite[]>();
    static readonly Dictionary<Sprite, Bounds> VisibleBoundsCache = new Dictionary<Sprite, Bounds>();
    static readonly Dictionary<Sprite, float> HeadWidthCache = new Dictionary<Sprite, float>();
    static Texture2D CachedPetSheet;
    static Color32[] CachedPetPixels;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeCaches()
    {
        // Enter Play Mode may skip domain reload, but runtime-created sprites
        // are still destroyed on exit. Never reuse their stale references.
        FrameCache.Clear();
        IdleCache.Clear();
        UnifiedCache.Clear();
        VisibleBoundsCache.Clear();
        HeadWidthCache.Clear();
        CachedPetSheet = null;
        CachedPetPixels = null;
    }

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
        if (string.IsNullOrWhiteSpace(petId)) return false;
        if (PlayerPrefs.GetInt(OwnedPrefix + petId, 0) == 1) return true;

        bool starter = petId == "rabbit" || petId == "cat";
        return starter && PlayerPrefs.GetInt(StarterPetsClearedKey, 0) == 0;
    }

    public static IReadOnlyList<LTCPetDefinition> GetOwnedPets()
    {
        return Catalog.Where(pet => IsOwned(pet.id)).ToList();
    }

    public static int ResetOwnedPetsForTesting()
    {
        int removed = OwnedCount;
        foreach (LTCPetDefinition pet in Catalog)
            PlayerPrefs.DeleteKey(OwnedPrefix + pet.id);

        // Rabbit and cat are normally free starters. This flag lets the temporary
        // reset button produce a genuinely empty collection for testing.
        PlayerPrefs.SetInt(StarterPetsClearedKey, 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return removed;
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
        PetHungerService.RemainingSeconds(pet.id);
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
        Sprite idle = pet == null ? null : GetIdleSprite(pet.id);
        if (idle != null) return idle;
        Sprite[] frames = GetFrames(pet);
        return frames.Length == 0 ? null : frames[0];
    }

    public static Sprite GetIdleSprite(string petId)
    {
        if (IdleCache.TryGetValue(petId, out Sprite cached) && cached != null) return cached;
        Sprite[] unified = GetUnifiedFrames(petId);
        if (unified != null) return IdleCache[petId] = unified[0];
        Texture2D individual = Resources.Load<Texture2D>("Pets/GachaHD/" + petId + "_idle");
        if (individual != null)
        {
            Sprite isolated = CreateIsolatedPetFrame(individual, 0, 0, individual.width, individual.height,
                petId + "_FrontIdle", true);
            IdleCache[petId] = isolated;
            return isolated;
        }
        int index = Array.FindIndex(Catalog, p => p.id == petId);
        Texture2D sheet = Resources.Load<Texture2D>("Pets/GachaHD/front_idle");
        if (index < 0 || sheet == null) return null;
        int column = index % 4, row = index / 4;
        int left = Mathf.RoundToInt(column * sheet.width / 4f);
        int right = Mathf.RoundToInt((column + 1) * sheet.width / 4f);
        // The generated front sheet has more space in the top row for rabbit ears.
        // Measured gaps, not an assumed equal grid, keep the feet and antlers intact.
        float[] rowEdges = { 0f, .28f, .52f, .744f, 1f };
        int top = Mathf.RoundToInt(rowEdges[row] * sheet.height);
        int bottom = Mathf.RoundToInt(rowEdges[row + 1] * sheet.height);
        Sprite result = CreateIsolatedPetFrame(sheet, left, sheet.height - bottom,
            right - left, bottom - top, petId + "_FrontIdle", true);
        IdleCache[petId] = result;
        return result;
    }

    public static Bounds GetVisibleBounds(Sprite sprite)
    {
        if (sprite == null) return new Bounds();
        if (VisibleBoundsCache.TryGetValue(sprite, out Bounds cached)) return cached;
        Bounds result = sprite.bounds;
        if (sprite.texture.isReadable)
        {
            Rect r = sprite.rect;
            int w = (int)r.width, h = (int)r.height;
            Color[] pixels = sprite.texture.GetPixels((int)r.x, (int)r.y, w, h);
            int minX=w, minY=h, maxX=-1, maxY=-1;
            for (int y=0; y<h; y++) for(int x=0; x<w; x++)
                if(pixels[y*w+x].a > .08f) { minX=Mathf.Min(minX,x); minY=Mathf.Min(minY,y); maxX=Mathf.Max(maxX,x); maxY=Mathf.Max(maxY,y); }
            if(maxX>=minX) result = new Bounds(new Vector3(((minX+maxX+1f)*.5f-sprite.pivot.x)/sprite.pixelsPerUnit,
                ((minY+maxY+1f)*.5f-sprite.pivot.y)/sprite.pixelsPerUnit,0f),
                new Vector3((maxX-minX+1f)/sprite.pixelsPerUnit,(maxY-minY+1f)/sprite.pixelsPerUnit,.01f));
        }
        VisibleBoundsCache[sprite]=result;
        return result;
    }

    public static float GetHeadWidth(Sprite sprite)
    {
        if (sprite == null) return .01f;
        if (HeadWidthCache.TryGetValue(sprite, out float cached)) return cached;
        Bounds visible = GetVisibleBounds(sprite);
        float result = visible.size.x;
        if (sprite.texture.isReadable)
        {
            Rect rect = sprite.rect;
            int w=(int)rect.width, h=(int)rect.height;
            Color[] pixels = sprite.texture.GetPixels((int)rect.x,(int)rect.y,w,h);
            int start = Mathf.Clamp(Mathf.RoundToInt((visible.min.y + visible.size.y * .52f) * sprite.pixelsPerUnit + sprite.pivot.y),0,h-1);
            int end = Mathf.Clamp(Mathf.RoundToInt((visible.min.y + visible.size.y * .85f) * sprite.pixelsPerUnit + sprite.pivot.y),0,h-1);
            int longest=0;
            for(int y=start;y<=end;y++)
            {
                int run=0;
                for(int x=0;x<w;x++)
                {
                    run=pixels[y*w+x].a > .08f ? run+1 : 0;
                    longest=Mathf.Max(longest,run);
                }
            }
            if(longest>0) result=longest/sprite.pixelsPerUnit;
        }
        HeadWidthCache[sprite]=result;
        return result;
    }

    public static Sprite[] GetFrames(LTCPetDefinition pet)
    {
        if (pet == null) return Array.Empty<Sprite>();
        if (FrameCache.TryGetValue(pet.id, out Sprite[] cached) && cached.All(s => s != null)) return cached;

        Sprite[] unified = GetUnifiedFrames(pet.id);
        if (unified != null)
        {
            // Idle and walking belong to the SAME sheet; never play front poses
            // in the walking loop, or stretch them to a separately generated head.
            var walking = new Sprite[12];
            Array.Copy(unified, 4, walking, 0, 12);
            FrameCache[pet.id] = walking;
            return walking;
        }

        // Individual high-resolution sheets avoid squeezing every pet into one atlas.
        Texture2D highResolution = Resources.Load<Texture2D>("Pets/GachaHD/" + pet.id);
        if (highResolution != null)
        {
            var highFrames = new Sprite[16];
            float[] highRowEdges = { 0f, .27f, .51f, .75f, 1f };
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                {
                    int left = Mathf.RoundToInt(column * highResolution.width / 4f);
                    int right = Mathf.RoundToInt((column + 1) * highResolution.width / 4f);
                    int top = Mathf.RoundToInt(highRowEdges[row] * highResolution.height);
                    int bottom = Mathf.RoundToInt(highRowEdges[row + 1] * highResolution.height);
                    highFrames[row * 4 + column] = CreateIsolatedPetFrame(highResolution,
                        left, highResolution.height - bottom, right - left, bottom - top,
                        pet.id + "_HD_" + (row * 4 + column), true);
                }
            FrameCache[pet.id] = highFrames;
            return highFrames;
        }

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

    public static bool HasUnifiedSheet(string petId)
    {
        return Resources.Load<Texture2D>("Pets/GachaUnified/" + petId) != null;
    }

    public static Sprite[] GetUnifiedFrames(string petId)
    {
        if (UnifiedCache.TryGetValue(petId, out Sprite[] cached) && cached.All(s => s != null)) return cached;
        Texture2D source = Resources.Load<Texture2D>("Pets/GachaUnified/" + petId);
        if (source == null) return null;
        var frames = new Sprite[16];
        for (int row=0; row<4; row++) for (int col=0; col<4; col++)
        {
            int left=Mathf.RoundToInt(col*source.width/4f);
            int right=Mathf.RoundToInt((col+1)*source.width/4f);
            int top=Mathf.RoundToInt(row*source.height/4f);
            int bottom=Mathf.RoundToInt((row+1)*source.height/4f);
            frames[row*4+col]=CreateIsolatedPetFrame(source,left,source.height-bottom,
                right-left,bottom-top,petId+"_Unified_"+(row*4+col),true);
        }
        UnifiedCache[petId]=frames;
        return frames;
    }

    static Sprite CreateIsolatedPetFrame(Texture2D source, int searchX, int searchY,
        int searchWidth, int searchHeight, string spriteName, bool highResolution = false)
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
                    if (nextX < Mathf.Max(0, searchX) || nextX >= maxSearchX ||
                        nextY < Mathf.Max(0, searchY) || nextY >= maxSearchY)
                        continue;
                    int next = nextY * textureWidth + nextX;
                    if (CachedPetPixels[next].a <= 20 || !component.Add(next)) continue;
                    pending.Enqueue(next);
                }
            }
        }

        int padding = highResolution ? 16 : 7;
        // Equal canvases and a common foot baseline prevent per-frame crop jitter.
        int canvasSize = highResolution ? Mathf.CeilToInt(Mathf.Max(source.width, source.height) / 4f) + padding * 2 : 128;
        int outputWidth = Mathf.Max(canvasSize, maxX - minX + 1 + padding * 2);
        int outputHeight = Mathf.Max(canvasSize, maxY - minY + 1 + padding * 2);
        int leftPadding = (outputWidth - (maxX - minX + 1)) / 2;
        var isolated = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
        var isolatedPixels = new Color32[outputWidth * outputHeight];
        foreach (int sourceIndex in component)
        {
            int sourceX = sourceIndex % textureWidth;
            int sourceY = sourceIndex / textureWidth;
            int targetX = sourceX - minX + leftPadding;
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
        if (FrameCache.TryGetValue(cacheKey, out Sprite[] cached) && cached.All(s => s != null)) return cached;
        Texture2D source = Resources.Load<Texture2D>("Pets/Gacha/blind_box_sheet");
        if (source == null) return Array.Empty<Sprite>();
        Texture2D texture = CreateTransparentBlindBoxTexture(source);

        int cellWidth = texture.width / 4;
        int cellHeight = texture.height / 4;
        // Measured empty gaps in the ORIGINAL 1536x1024 sheet. The last
        // row's stars start above y=768, so an even 256px cut removed them.
        int[] topEdges = { 0, 252, 496, 738, 1024 };
        var frames = new Sprite[16];
        int index = 0;
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                // Give every frame the same safe canvas, including space for the lid,
                // ribbons and sparkles. FullRect avoids a changing tight mesh.
                const int margin = 40;
                int top = Mathf.RoundToInt(topEdges[row] * texture.height / 1024f);
                int bottom = Mathf.RoundToInt(topEdges[row + 1] * texture.height / 1024f);
                int height = bottom - top;
                int nominalBottom = Mathf.RoundToInt((row + 1) * texture.height / 4f);
                var padded = new Texture2D(cellWidth + margin * 2, cellHeight + margin * 2, TextureFormat.RGBA32, false);
                padded.SetPixels32(new Color32[padded.width * padded.height]);
                // Preserve the nominal cell origin on one equal canvas while
                // capturing protruding stars from the empty inter-row gaps.
                padded.SetPixels(margin, margin + nominalBottom - bottom, cellWidth, height,
                    texture.GetPixels(column * cellWidth, texture.height - bottom, cellWidth, height));
                padded.Apply(false, false);
                padded.filterMode = FilterMode.Bilinear;
                padded.wrapMode = TextureWrapMode.Clamp;
                frames[index++] = Sprite.Create(padded, new Rect(0, 0, padded.width, padded.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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
