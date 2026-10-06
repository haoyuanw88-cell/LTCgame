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
    Texture2D[] opening, forward, backward, activeClip;
    readonly Dictionary<Animator,float> animatorSpeeds=new Dictionary<Animator,float>();
    Texture2D[] pages;
    Coroutine routine;
    float savedTimeScale;
    Transform garden;
    static readonly Color Ink = new Color(.345f,.231f,.161f);

    void Start()
    {
        garden = GameObject.Find("Pet Garden UI")?.transform;
        if (!garden) return;
        var management = garden.Find("Pet Management Button")?.GetComponent<Button>();
        if (!management) return;
        BuildLauncher(management);
        BuildModal();
    }

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
        pages=new Texture2D[4];for(int i=0;i<4;i++)pages[i]=Resources.Load<Texture2D>("PetBook/page_"+i);
        opening=LoadClip("open");forward=LoadClip("next");backward=LoadClip("previous");
        overlay.SetActive(true);BuildPages();routine=StartCoroutine(Animate(opening,.65f));
    }

    public void Turn(int direction)
    {
        if(!IsOpen||IsAnimating||direction==0)return;
        int target=Mathf.Clamp(SpreadIndex+(direction>0?1:-1),0,1);
        if(target==SpreadIndex)return;
        SpreadIndex=target;routine=StartCoroutine(Animate(direction>0?forward:backward,.6f));
    }

    static Texture2D[] LoadClip(string name){return new[]{Resources.Load<Texture2D>("PetBook/"+name+"_0"),Resources.Load<Texture2D>("PetBook/"+name+"_1")};}
    IEnumerator Animate(Texture2D[] atlas,float duration)
    {
        IsAnimating=true;previous.interactable=next.interactable=false;
        pagesRoot.SetActive(false);frameRoot.SetActive(false);animation.gameObject.SetActive(true);activeClip=atlas;
        if(atlas!=null&&atlas[0]){double start=Time.realtimeSinceStartupAsDouble;while(Time.realtimeSinceStartupAsDouble-start<duration){float elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);SetFrame(Mathf.Min(23,Mathf.FloorToInt(elapsed/duration*24)));yield return null;}SetFrame(23);}
        BuildPages();animation.gameObject.SetActive(false);frameRoot.SetActive(true);pagesRoot.SetActive(true);
        IsAnimating=false;routine=null;previous.interactable=SpreadIndex>0;next.interactable=SpreadIndex<1;
    }

    void SetFrame(int index){int local=index%12;animation.texture=activeClip[index/12];animation.uvRect=new Rect((local%3)/3f,1f-(local/3+1)/4f,1f/3f,.25f);}

    void BuildPages()
    {
        foreach(Transform child in pagesRoot.transform){child.gameObject.SetActive(false);foreach(var image in child.GetComponentsInChildren<Image>(true)){if(image.sprite){generatedSprites.Remove(image.sprite);Destroy(image.sprite);}}Destroy(child.gameObject);}
        for(int side=0;side<2;side++)
        {
            int page=SpreadIndex*2+side;
            var go=new GameObject("Page "+(page+1),typeof(RectTransform));go.transform.SetParent(pagesRoot.transform,false);
            Fill((RectTransform)go.transform,(160+480*side)/1280f,121/720f,(640+480*side)/1280f,631/720f);
            var title=Text(go.transform,page==0?"我們的寵物":"等待新的相遇",30);Fill(title.rectTransform,.05f,.884f,.95f,.985f);
            var line=new GameObject("標題底線",typeof(RectTransform),typeof(Image));line.transform.SetParent(go.transform,false);line.GetComponent<Image>().color=new Color(201/255f,170/255f,130/255f);line.GetComponent<Image>().raycastTarget=false;Fill((RectTransform)line.transform,.05f,448/510f,.95f,450/510f);
            var grid=new GameObject("Pet Grid",typeof(RectTransform),typeof(GridLayoutGroup));grid.transform.SetParent(go.transform,false);
            Fill((RectTransform)grid.transform,22/480f,65/510f,456/480f,432/510f);
            var layout=grid.GetComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.spacing=new Vector2(14,13);
            // Cell size follows the fitted page rather than a device-specific pixel size.
            var responsive=grid.AddComponent<PetBookGridSize>();responsive.Page=(RectTransform)go.transform;
            for(int slot=0;slot<4;slot++)
            {
                int index=page*4+slot;
                var cell=new GameObject(index==0?"兔子 已解鎖":index==1?"貓咪 已解鎖":"未解鎖 ???",typeof(RectTransform),typeof(Image));cell.transform.SetParent(grid.transform,false);
                var image=cell.GetComponent<Image>();image.raycastTarget=false;
                if(pages[page]){var sprite=Sprite.Create(pages[page],new Rect(22+slot%2*224,510-(78+slot/2*190+177),210,177),new Vector2(.5f,.5f));generatedSprites.Add(sprite);image.sprite=sprite;}
            }
            var number=Text(go.transform,(page+1).ToString(),23);Fill(number.rectTransform,0,0,1,.085f);
        }
    }

    public void Close()
    {
        if(!IsOpen)return;
        if(routine!=null)StopCoroutine(routine);routine=null;IsAnimating=false;
        overlay.SetActive(false);IsOpen=false;ModalOpen=false;Time.timeScale=savedTimeScale;
        foreach(var pair in animatorSpeeds)if(pair.Key)pair.Key.speed=pair.Value;animatorSpeeds.Clear();
        foreach(var pair in hidden)if(pair.Key)pair.Key.SetActive(pair.Value);hidden.Clear();
    }

    void OnDestroy()
    {
        Close();if(overlay)Destroy(overlay);if(launcher)Destroy(launcher.gameObject);
        foreach(var sprite in generatedSprites)if(sprite)Destroy(sprite);
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
