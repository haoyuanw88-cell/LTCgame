using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Approved corner-fold animation, real grid UI and modal garden isolation.</summary>
public sealed class PetCollectionBook : MonoBehaviour
{
    [SerializeField] TMP_FontAsset roundedFont;
    public bool IsOpen { get; private set; }
    public static bool ModalOpen { get; private set; }
    public int SpreadIndex { get; private set; }
    public bool IsAnimating { get; private set; }
    readonly Dictionary<GameObject,bool> hidden = new Dictionary<GameObject,bool>();
    readonly List<Sprite> generatedSprites = new List<Sprite>();
    GameObject overlay, pagesRoot, frameRoot;
    RawImage animation;
    Button previous, next, close, launcher;
    readonly Dictionary<Animator,float> animatorSpeeds=new Dictionary<Animator,float>();
    Coroutine routine;
    float savedTimeScale;
    Transform garden;
    static readonly Color Ink = new Color(.345f,.231f,.161f);
    readonly List<LTCPetDefinition> bookPets = new List<LTCPetDefinition>();
    readonly RenderTexture[] livePages = new RenderTexture[4];
    GameObject liveTurnRoot;
    RawImage liveLeft, liveRight;
    PetBookFoldGraphic frontFace, reversePaper, reverseFace, creaseOutline;
    Sprite roundedCard;

    void Start()
    {
        garden = GameObject.Find("Pet Garden UI")?.transform;
        if (!garden) return;
        var management = garden.Find("Pet Management Button")?.GetComponent<Button>();
        if (!management) return;
        BuildLauncher(management);
        BuildModal();
        EnsureCatalog();
        LTCPetCollectionService.Changed += RefreshOwnership;
    }

    void EnsureCatalog()
    {
        if (bookPets.Count > 0) return;
        bookPets.Add(LTCPetCollectionService.AllPets[0]); // Rabbit and cat always occupy the first two slots.
        bookPets.Add(LTCPetCollectionService.AllPets[2]);
        foreach (var pet in LTCPetCollectionService.AllPets)
            if (pet.id != "rabbit" && pet.id != "cat") bookPets.Add(pet);
    }

    void RefreshOwnership() { if (IsOpen && !IsAnimating) routine=StartCoroutine(RefreshLivePages()); }
    IEnumerator RefreshLivePages(){IsAnimating=true;pagesRoot.SetActive(false);yield return CapturePages();BuildPages();pagesRoot.SetActive(true);IsAnimating=false;routine=null;}

    void BuildLauncher(Button source)
    {
        var go=new GameObject("圖鑑按鈕",typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(garden,false);
        var r=(RectTransform)go.transform;var original=(RectTransform)source.transform;
        r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=Vector2.zero;r.sizeDelta=original.sizeDelta;
        r.anchoredPosition=original.anchoredPosition+new Vector2(710,0);
        var image=go.GetComponent<Image>();image.sprite=source.image.sprite;image.preserveAspect=true;
        launcher=go.GetComponent<Button>();launcher.targetGraphic=image;
        var label=Text(go.transform,"圖鑑",40);Fill(label.rectTransform,.12f,.24f,.9f,.76f);
        launcher.onClick.AddListener(Open);
    }

    void BuildModal()
    {
        roundedCard=MakeRoundedCard();
        overlay=new GameObject("Pet Collection Book Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var c=overlay.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=32700;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        // Transparent input shield: retain the live garden and its camera position.
        // The book itself is opaque and renders above every pet sprite.
        var shield=new GameObject("透明操作遮罩",typeof(RectTransform),typeof(Image));shield.transform.SetParent(overlay.transform,false);
        var background=shield.GetComponent<Image>();background.color=Color.clear;background.raycastTarget=true;
        Fill(background.rectTransform,0,0,1,1);
        // Fit the approved 16:9 layout as a unit; never stretch the book/pages.
        var root=new GameObject("Book Layout",typeof(RectTransform),typeof(AspectRatioFitter));root.transform.SetParent(overlay.transform,false);
        Fill((RectTransform)root.transform,0,0,1,1);var fit=root.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9f;
        var design=new GameObject("1280x720 Design",typeof(RectTransform));design.transform.SetParent(root.transform,false);
        Fill((RectTransform)design.transform,0,0,1,1);
        var frame=Raw(design.transform,"圓角書本",Resources.Load<Texture2D>("PetBook/frame"));Fill(frame.rectTransform,0,0,1,1);frameRoot=frame.gameObject;
        pagesRoot=new GameObject("Grid Pages",typeof(RectTransform));pagesRoot.transform.SetParent(design.transform,false);Fill((RectTransform)pagesRoot.transform,0,0,1,1);
        animation=Raw(design.transform,"翻頁動畫",null);Fill(animation.rectTransform,0,0,1,1);animation.raycastTarget=false;
        liveTurnRoot = new GameObject("即時內容翻頁", typeof(RectTransform));
        liveTurnRoot.transform.SetParent(design.transform, false);Fill((RectTransform)liveTurnRoot.transform,0,0,1,1);
        liveLeft=Raw(liveTurnRoot.transform,"底部左頁",null);Fill(liveLeft.rectTransform,160/1280f,121/720f,640/1280f,631/720f);
        liveRight=Raw(liveTurnRoot.transform,"底部右頁",null);Fill(liveRight.rectTransform,640/1280f,121/720f,1120/1280f,631/720f);
        frontFace=FoldFace("正在翻動的正面");reversePaper=FoldFace("紙張背面");reverseFace=FoldFace("正在翻動的背面內容");
        creaseOutline=FoldFace("折頁暖色邊線");creaseOutline.OutlineOnly=true;
        liveTurnRoot.SetActive(false);
        previous=Navigation(design.transform,"上一頁",165,()=>Turn(-1));
        close=Navigation(design.transform,"關閉圖鑑",520,Close);
        next=Navigation(design.transform,"下一頁",875,()=>Turn(1));
        overlay.SetActive(false);
    }

    public void Open()
    {
        if (IsOpen || !overlay) return;
        IsOpen=true;ModalOpen=true;SpreadIndex=0;
        savedTimeScale=Time.timeScale;Time.timeScale=0;
        animatorSpeeds.Clear();foreach(var pet in FindObjectsByType<PetWander>(FindObjectsSortMode.None))foreach(var a in pet.GetComponentsInChildren<Animator>(true)){animatorSpeeds[a]=a.speed;a.speed=0;}
        // Remember each original active state, including already-hidden modals.
        hidden.Clear();
        foreach(var button in garden.GetComponentsInChildren<Button>(true)){hidden[button.gameObject]=button.gameObject.activeSelf;button.gameObject.SetActive(false);}
        overlay.SetActive(true);BuildPages();routine=StartCoroutine(OpenLive());
    }

    IEnumerator OpenLive()
    {
        IsAnimating=true;previous.interactable=next.interactable=false;
        pagesRoot.SetActive(false);
        // Capture from a normal game frame, never recursively inside an Editor callback.
        yield return null;
        yield return CapturePages();BuildPages();
        yield return AnimateLive(0,0,true,.65f);
    }

    public void Turn(int direction)
    {
        if(!IsOpen||IsAnimating||direction==0)return;
        int target=Mathf.Clamp(SpreadIndex+(direction>0?1:-1),0,1);
        if(target==SpreadIndex)return;
        int source=SpreadIndex;SpreadIndex=target;routine=StartCoroutine(AnimateLive(source,target,false,.6f));
    }

    PetBookFoldGraphic FoldFace(string name)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(PetBookFoldGraphic));
        go.transform.SetParent(liveTurnRoot.transform,false);Fill((RectTransform)go.transform,0,0,1,1);
        var face=go.GetComponent<PetBookFoldGraphic>();face.raycastTarget=false;return face;
    }

    // Render the same real UI used at rest; no separate pet lists or baked unlock state.
    IEnumerator CapturePages()
    {
        int savedSpread=SpreadIndex;
        var root=new GameObject("圖鑑頁面離屏擷取",typeof(RectTransform),typeof(Canvas));
        root.layer=31;root.transform.position=new Vector3(10000,10000,0);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;
        ((RectTransform)root.transform).sizeDelta=new Vector2(480,510);
        var cameraObject=new GameObject("圖鑑擷取相機",typeof(Camera));
        var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        camera.orthographicSize=255;camera.aspect=480f/510f;camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(1f,.96f,.86f,1f);
        camera.transform.position=root.transform.position+new Vector3(0,0,-10);camera.nearClipPlane=.1f;camera.farClipPlane=20;
        canvas.worldCamera=camera;
        try
        {
            // Register the offscreen camera/canvas before the first content capture.
            // Otherwise Unity's first world-space UI batch can arrive one frame late.
            if(livePages[0]==null){livePages[0]=new RenderTexture(960,1020,24,RenderTextureFormat.ARGB32);livePages[0].name="Live Pet Book Page 0";livePages[0].Create();}
            camera.targetTexture=livePages[0];camera.enabled=true;
            yield return null;yield return new WaitForEndOfFrame();
            camera.enabled=false;camera.targetTexture=null;
            for(int spread=0;spread<2;spread++)
            {
                SpreadIndex=spread;BuildPages();Canvas.ForceUpdateCanvases();
                yield return null; // Retire old pages before looking up the fresh UI roots.
                for(int side=0;side<2;side++)
                {
                    int page=spread*2+side;
                    Transform source=null;
                    foreach(Transform candidate in pagesRoot.transform)
                        if(candidate.name=="Page "+(page+1) && candidate.gameObject.activeSelf){source=candidate;break;}
                    var clone=Instantiate(source.gameObject,root.transform,false);
                    foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    var rect=(RectTransform)clone.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                    rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(480,510);
                    clone.SetActive(true);
                    foreach(var grid in clone.GetComponentsInChildren<PetBookGridSize>())
                    {
                        grid.Page=rect;
                        var layout=grid.GetComponent<GridLayoutGroup>();layout.cellSize=new Vector2(210,177);layout.spacing=new Vector2(14,13);
                    }
                    Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    foreach(var text in clone.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
                    if(livePages[page]==null){livePages[page]=new RenderTexture(960,1020,24,RenderTextureFormat.ARGB32);livePages[page].name="Live Pet Book Page "+page;livePages[page].Create();}
                    camera.targetTexture=livePages[page];camera.enabled=true;
                    // Let Unity finish its normal UI batching and render pass for each page.
                    // Rendering four UI canvases manually in one Editor callback can skip batches.
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    camera.enabled=false;camera.targetTexture=null;
                    clone.SetActive(false);Destroy(clone);
                }
            }
        }
        finally{SpreadIndex=savedSpread;root.SetActive(false);Destroy(root);Destroy(cameraObject);}
    }

    IEnumerator AnimateLive(int source,int target,bool cover,float duration)
    {
        IsAnimating=true;previous.interactable=next.interactable=false;
        pagesRoot.SetActive(false);frameRoot.SetActive(true);animation.gameObject.SetActive(false);liveTurnRoot.SetActive(true);
        int direction=target>=source?1:-1;
        liveLeft.texture=livePages[cover?0:direction>0?source*2:target*2];
        liveRight.texture=livePages[cover?1:direction>0?target*2+1:source*2+1];
        if(cover)liveLeft.gameObject.SetActive(false);else liveLeft.gameObject.SetActive(true);
        Texture front=cover?Resources.Load<Texture2D>("PetBook/open_0"):livePages[direction>0?source*2+1:source*2];
        Texture back=livePages[cover?0:direction>0?target*2:target*2+1];
        double start=Time.realtimeSinceStartupAsDouble;
        while(Time.realtimeSinceStartupAsDouble-start<duration)
        {
            float t=(float)(Time.realtimeSinceStartupAsDouble-start)/duration;
            DrawFold(front,back,t*t*(3-2*t),direction,cover);yield return null;
        }
        BuildPages();liveTurnRoot.SetActive(false);pagesRoot.SetActive(true);
        IsAnimating=false;routine=null;previous.interactable=SpreadIndex>0;next.interactable=SpreadIndex<1;
    }

    static List<Vector2> Clip(List<Vector2> polygon,Vector2 mid,Vector2 normal,bool folded)
    {
        var result=new List<Vector2>();
        for(int i=0;i<polygon.Count;i++)
        {
            Vector2 a=polygon[i],b=polygon[(i+1)%polygon.Count];float da=Vector2.Dot(a-mid,normal),db=Vector2.Dot(b-mid,normal);
            bool inside=folded?da>=0:da<=0,other=folded?db>=0:db<=0;
            if(inside)result.Add(a);if(inside!=other)result.Add(a+(b-a)*da/(da-db));
        }
        return result;
    }

    void DrawFold(Texture front,Texture back,float k,int direction,bool cover)
    {
        float left=direction>0?640:160;
        var polygon=new List<Vector2>();
        Vector2[] centers={new Vector2(left+18,107),new Vector2(left+462,107),new Vector2(left+462,581),new Vector2(left+18,581)};
        for(int cornerIndex=0;cornerIndex<4;cornerIndex++)for(int step=0;step<=6;step++)
        {
            float angle=(180+cornerIndex*90+step*15)*Mathf.Deg2Rad;
            polygon.Add(centers[cornerIndex]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*18);
        }
        Vector2 corner=new Vector2(direction>0?1120:160,599);
        Vector2 drag=new Vector2(corner.x-direction*960*k,599-100*Mathf.Sin(Mathf.PI*k));
        Vector2 normal=(corner-drag).normalized;if(normal.sqrMagnitude<.01f)normal=new Vector2(direction,0);
        Vector2 mid=(corner+drag)*.5f;
        var visible=Clip(polygon,mid,normal,false);var fold=Clip(polygon,mid,normal,true);
        var frontUV=new List<Vector2>();foreach(var p in visible)
        {
            Vector2 uv=new Vector2((p.x-left)/480f,1-(p.y-89)/510f);
            if(cover)uv=new Vector2((640+uv.x*480)/3840f,(2880-599+uv.y*510)/2880f);
            frontUV.Add(uv);
        }
        frontFace.SetPage(front,visible,frontUV,Color.white);
        var reflected=new List<Vector2>();var backUV=new List<Vector2>();
        foreach(var p in fold){reflected.Add(p-2*normal*Vector2.Dot(p-mid,normal));backUV.Add(new Vector2(1-(p.x-left)/480f,1-(p.y-89)/510f));}
        reversePaper.SetPage(null,reflected,backUV,new Color(.969f,.91f,.8f));
        reverseFace.SetPage(back,reflected,backUV,new Color(1,1,1,Mathf.Clamp01((k-.76f)/.24f)));
        creaseOutline.SetPage(null,reflected,backUV,new Color(.71f,.586f,.44f));
    }


    void BuildPages()
    {
        EnsureCatalog();
        foreach(Transform child in pagesRoot.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        for(int side=0;side<2;side++)
        {
            int page=SpreadIndex*2+side;
            var go=new GameObject("Page "+(page+1),typeof(RectTransform));go.transform.SetParent(pagesRoot.transform,false);
            Fill((RectTransform)go.transform,(160+480*side)/1280f,121/720f,(640+480*side)/1280f,631/720f);
            var title=Text(go.transform,"寵物圖鑑 · " + LTCPetCollectionService.OwnedCount + "/" + bookPets.Count,30);Fill(title.rectTransform,.05f,.884f,.95f,.985f);
            var line=new GameObject("標題底線",typeof(RectTransform),typeof(Image));line.transform.SetParent(go.transform,false);line.GetComponent<Image>().color=new Color(201/255f,170/255f,130/255f);line.GetComponent<Image>().raycastTarget=false;Fill((RectTransform)line.transform,.05f,448/510f,.95f,450/510f);
            var grid=new GameObject("Pet Grid",typeof(RectTransform),typeof(GridLayoutGroup));grid.transform.SetParent(go.transform,false);
            Fill((RectTransform)grid.transform,22/480f,65/510f,456/480f,432/510f);
            var layout=grid.GetComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.spacing=new Vector2(14,13);
            // Cell size follows the fitted page rather than a device-specific pixel size.
            var responsive=grid.AddComponent<PetBookGridSize>();responsive.Page=(RectTransform)go.transform;
            for(int slot=0;slot<4;slot++)
            {
                int index=page*4+slot;
                LTCPetDefinition definition = index < bookPets.Count ? bookPets[index] : null;
                bool unlocked = definition != null && LTCPetCollectionService.IsOwned(definition.id);
                var cell=new GameObject(unlocked ? definition.id + " 已解鎖" : "未解鎖 ???",typeof(RectTransform),typeof(Image));cell.transform.SetParent(grid.transform,false);
                var image=cell.GetComponent<Image>();image.raycastTarget=false;
                image.sprite=roundedCard;image.type=Image.Type.Sliced;image.color=new Color(.70f,.576f,.424f);
                var inside=CardImage(cell.transform,"暖色卡片底框",new Color(.918f,.851f,.722f));Fill(inside.rectTransform,.009f,.011f,.991f,.989f);
                var portraitFrame=CardImage(cell.transform,"圓角頭像底框",unlocked?new Color(1f,.98f,.94f):new Color(.082f,.075f,.063f));
                Fill(portraitFrame.rectTransform,12/210f,40/177f,198/210f,168/177f);
                if (unlocked)
                {
                    Sprite portrait = null;
                    foreach (var pet in FindObjectsByType<PetWander>(FindObjectsSortMode.None))
                        if (pet.PetId == definition.id) { portrait = pet.Portrait; break; }
                    if (portrait == null) portrait = LTCPetCollectionService.GetPreviewSprite(definition);
                    var icon = new GameObject("寵物頭像",typeof(RectTransform),typeof(Image));icon.transform.SetParent(portraitFrame.transform,false);
                    var picture = icon.GetComponent<Image>();picture.sprite=portrait;picture.preserveAspect=true;picture.raycastTarget=false;
                    Fill((RectTransform)icon.transform,.06f,.02f,.94f,.98f);
                }
                var label = Text(cell.transform, unlocked ? definition.displayName : "???", 28);
                label.color = Ink;
                Fill(label.rectTransform,.03f,.015f,.97f,.23f);
            }
            var number=Text(go.transform,(page+1).ToString(),23);Fill(number.rectTransform,0,0,1,.085f);
        }
    }

    Image CardImage(Transform parent,string name,Color tint)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.sprite=roundedCard;image.type=Image.Type.Sliced;image.color=tint;image.raycastTarget=false;return image;
    }

    Sprite MakeRoundedCard()
    {
        // A small nine-slice keeps the original soft corners without stretching artwork.
        const int size=64;const float radius=16;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="Book Rounded Card";
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=Mathf.Max(radius-(x+.5f),x+.5f-(size-radius),0);
            float dy=Mathf.Max(radius-(y+.5f),y+.5f-(size-radius),0);
            float alpha=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));
            pixels[y*size+x]=new Color(1,1,1,alpha);
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);texture.wrapMode=TextureWrapMode.Clamp;
        var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(17,17,17,17));
        generatedSprites.Add(sprite);return sprite;
    }

    public void Close()
    {
        if(!IsOpen)return;
        if(routine!=null)StopCoroutine(routine);routine=null;IsAnimating=false;
        overlay.SetActive(false);IsOpen=false;ModalOpen=false;Time.timeScale=savedTimeScale;
        if(liveTurnRoot)liveTurnRoot.SetActive(false);
        foreach(var pair in animatorSpeeds)if(pair.Key)pair.Key.speed=pair.Value;animatorSpeeds.Clear();
        foreach(var pair in hidden)if(pair.Key)pair.Key.SetActive(pair.Value);hidden.Clear();
    }

    void OnDestroy()
    {
        LTCPetCollectionService.Changed -= RefreshOwnership;
        Close();if(overlay)Destroy(overlay);if(launcher)Destroy(launcher.gameObject);
        if(roundedCard && roundedCard.texture)Destroy(roundedCard.texture);
        foreach(var sprite in generatedSprites)if(sprite)Destroy(sprite);
        foreach(var page in livePages)if(page){page.Release();Destroy(page);}
    }

    Button Navigation(Transform parent,string label,int x,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        Fill((RectTransform)go.transform,x/1280f,16/720f,(x+240)/1280f,80/720f);
        var image=go.GetComponent<Image>();var tex=Resources.Load<Texture2D>("PetBook/button");
        if(tex){var sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));generatedSprites.Add(sprite);image.sprite=sprite;}
        var b=go.GetComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(action);
        var text=Text(go.transform,label=="上一頁"?"‹ 上一頁":label=="下一頁"?"下一頁 ›":label,30);Fill(text.rectTransform,0,0,1,1);return b;
    }
    TMP_Text Text(Transform parent,string value,float size)
    {
        var go=new GameObject(value,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var t=go.GetComponent<TMP_Text>();t.font=roundedFont;t.text=value;t.fontSize=size;t.color=Ink;t.alignment=TextAlignmentOptions.Center;t.enableAutoSizing=false;t.raycastTarget=false;return t;
    }
    static RawImage Raw(Transform parent,string name,Texture texture)
    {var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);var r=go.GetComponent<RawImage>();r.texture=texture;r.raycastTarget=false;return r;}
    static void Fill(RectTransform r,float x,float y,float xx,float yy){r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=r.offsetMax=Vector2.zero;}
}

public sealed class PetBookGridSize : MonoBehaviour
{
    public RectTransform Page;
    void LateUpdate(){if(!Page)return;var grid=GetComponent<GridLayoutGroup>();float scale=Page.rect.width/480f;grid.cellSize=new Vector2(210,177)*scale;grid.spacing=new Vector2(14,13)*scale;}
}
