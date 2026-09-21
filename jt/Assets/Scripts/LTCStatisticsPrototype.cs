using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LTCCognitiveAssessment;

/// <summary>Baked, editable uGUI prototype. No sample scores or cohort data are invented.</summary>
public class LTCStatisticsPrototype : MonoBehaviour
{
    public Sprite background;
    public Sprite panelSkin;
    public Sprite tabSkin;
    public Sprite[] gameIcons;
    public Sprite[] navigationIcons;
    [SerializeField] Button[] navigationButtons;
    [SerializeField] TMP_Text[] gameCounts;
    [SerializeField] TMP_Text[] summaryValues;
    CanvasGroup oldNavigation;
    float savedAlpha;
    bool savedInteractable,savedRaycasts;
    int selectedGame=-1;
    Coroutine cloudRoutine;
    LTCStatisticsCloud.Response cloud;
    string cloudError;
    LTC.Identity.IPlayerIdentityProvider identity;
    static readonly string[] GameIds={"stroop_color_match","number_order","number_sum","card_memory_battle","pipe_connection","supermarket_shopping","true_false_life_quiz","gopher_reaction"};
    [SerializeField] TMP_FontAsset font;
    [SerializeField] GameObject[] pages;
    [SerializeField] Button[] tabs;
    [SerializeField] TMP_Text trendStatus;
    [SerializeField] TMP_Text rangeCaption;
    [SerializeField] TMP_Text summary;
    [SerializeField] CognitiveTrendChartGraphic chart;
    [SerializeField] TMP_Text detail;
    [SerializeField] int selectedTab;
    // Existing players may not have played during the latest 30 days.  Start
    // with the widest supported window so their valid history is visible as
    // soon as the statistics page opens; they can still switch to 7/30 days.
    int days = 90;
    RectTransform navigation;
    Vector2 navMin,navMax,navOffsetMin,navOffsetMax;
    Image[] navImages;
    Color[] navColors;
    TMP_Text[] navTexts;
    Color[] navTextColors;
    GameObject idCanvas;
    bool idWasActive;
    readonly Color ink = new Color(.25f,.19f,.15f);
    readonly Color cream = new Color(1f,.975f,.91f);
    readonly Color teal = new Color(.44f,.70f,.65f);
    readonly Color[] colors = {new Color(.97f,.77f,.64f),new Color(.69f,.83f,.91f),new Color(.92f,.73f,.77f)};
    static readonly string[] Titles = {"顏色文字判斷","數字由小到大","數字加總","翻牌記憶","旋轉接水管","超市購物","生活常識判斷","地鼠反應"};
    static readonly string[] Descriptions = {"注意力與抑制控制","處理速度與視覺搜尋","執行功能與數字操作","視覺記憶","視覺空間規劃","購物清單與記憶","生活知識與判斷","反應與動作協調"};

    public void Build(TMP_FontAsset suppliedFont)
    {
        font = suppliedFont;
        var old = transform.Find("StatisticsPrototype");
        if (old) DestroyImmediate(old.gameObject);
        // Keep previous UI as a reversible layout backup; the controller can still bind its references.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var baseImage=GetComponent<Image>();if(baseImage){baseImage.sprite=null;baseImage.color=new Color(.84f,.76f,.63f);}
        var root = Panel(transform,"StatisticsPrototype",Color.white,0,0,1,1);
        var fit=root.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9f;
        root.GetComponent<Image>().sprite=background;root.GetComponent<Image>().type=Image.Type.Simple;
        Label(root,"TopNote","與遊戲相伴\n天天都有好心情",20,.03f,.915f,.17f,.98f);
        Label(root,"TopRight","遊戲讓生活\n更豐富",20,.78f,.91f,.90f,.985f);
        tabs=new Button[3]; pages=new GameObject[3];
        string[] names={"個人趨勢","同齡參考","各遊戲表現"};
        for(int i=0;i<3;i++)
        {
            float x=.215f+i*.169f;
            tabs[i]=ButtonAt(root,"Tab"+i,names[i],x,.883f,x+.166f,.984f,Color.white);
            tabs[i].image.sprite=tabSkin;
            pages[i]=Panel(root,"Page"+i,Color.clear,.05f,.17f,.95f,.88f).gameObject;
        }
        BuildTrend(pages[0].transform);
        BuildCohort(pages[1].transform);
        BuildGames(pages[2].transform);
        Label(root,"FooterNote","遊戲表現僅供參考，不代表醫療診斷",17,.22f,.123f,.81f,.162f);
        ButtonAt(root,"RefreshCloud","重新整理",.81f,.13f,.93f,.17f,Color.white);
        var nav=Panel(root,"Navigation",Color.white,.035f,.006f,.965f,.101f);
        navigationButtons=new Button[5];var navNames=new[]{"遊戲","統計","商店","寵物","我的"};
        for(int i=0;i<5;i++){
            var b=ButtonAt(nav,"Nav"+i,"",i*.2f+.003f,.04f,(i+1)*.2f-.003f,.96f,i==1?colors[2]:Color.white);navigationButtons[i]=b;
            AddIcon(b.transform,"Icon",navigationIcons[i],.10f,.10f,.36f,.90f);
            Label(b.transform,"Name",navNames[i],28,.4f,.08f,.94f,.92f,true);
        }
        Bind(); SelectTab(2);
    }

    void BuildTrend(Transform p)
    {
        Label(p,"Title","我的遊玩趨勢",38,.035f,.86f,.80f,.98f,true);
        summaryValues=new TMP_Text[3];
        var summaryNames=new[]{"有效紀錄","完成遊戲","近七天參與"};
        for(int i=0;i<3;i++){var c=Panel(p,"SummaryCard"+i,colors[i],.01f+i*.335f,.61f,.33f+i*.335f,.845f);AddIcon(c,"Icon",navigationIcons[i==1?0:1],.045f,.15f,.24f,.85f);Label(c,"Name",summaryNames[i],24,.30f,.57f,.94f,.91f,true);summaryValues[i]=Label(c,"Value","—",34,.30f,.08f,.94f,.58f,true);}
        summary=Label(p,"Summary","全部遊戲",20,.035f,.495f,.26f,.60f);
        ButtonAt(p,"NextGame","切換遊戲",.26f,.51f,.43f,.60f,colors[1]);
        for(int i=0;i<3;i++) ButtonAt(p,"Range"+i,new[]{"7 天","30 天","90 天"}[i],.51f+i*.16f,.51f,.66f+i*.16f,.60f,Color.white);
        var graph=Panel(p,"Graph",Color.white,.095f,.15f,.96f,.475f);
        DestroyImmediate(graph.GetComponent<Image>());
        chart=graph.gameObject.AddComponent<CognitiveTrendChartGraphic>();
        chart.raycastTarget=false;
        for(int i=0;i<=4;i++) Label(p,"Y"+i,(i*25).ToString(),17,.02f,.13f+i*.08125f,.083f,.18f+i*.08125f);
        rangeCaption=Label(p,"Dates","29 天前",17,.10f,.085f,.50f,.14f);
        var today=Label(p,"Today","今天",17,.80f,.085f,.96f,.14f);today.alignment=TextAlignmentOptions.MidlineRight;
        trendStatus=Label(p,"Status","尚無有效紀錄",17,.1f,.015f,.95f,.075f);
    }
    void BuildCohort(Transform p)
    {
        Label(p,"Title","和同齡玩家比一比",38,.035f,.86f,.8f,.98f,true);
        Label(p,"Explanation","選擇遊戲以查看同齡參考",21,.035f,.75f,.68f,.85f);
        ButtonAt(p,"NextGame","切換遊戲",.76f,.76f,.97f,.85f,colors[1]);
        Panel(p,"DistributionPlaceholder",new Color(.85f,.93f,1f),.015f,.1f,.64f,.73f);
        Label(p,"EmptyTitle","同齡參考資料尚未提供",25,.05f,.44f,.60f,.56f,true);
        Label(p,"EmptyBody","資料充足後，這裡將顯示分布圖與你的位置。",20,.05f,.28f,.60f,.43f);
        string[] labels={"你的分數\n—","同齡中位數\n—","參考玩家\n資料待接入"};
        for(int i=0;i<3;i++)
        {
            var card=Panel(p,"Metric"+i,colors[i],.66f,.53f-i*.215f,.99f,.73f-i*.215f);
            AddIcon(card,"Icon",navigationIcons[i==0?1:4],.04f,.15f,.24f,.85f);
            Label(card,"Value",labels[i],25,.29f,.1f,.94f,.9f,true);
        }
        Label(p,"Notice","平台遊戲表現參考；尚未校正難度，不代表醫療診斷。",18,.035f,.015f,.965f,.09f);
    }
    void BuildGames(Transform p)
    {
        Label(p,"Title","每款遊戲的足跡",38,.03f,.86f,.9f,.98f,true);
        gameCounts=new TMP_Text[8];
        for(int i=0;i<8;i++)
        {
            int col=i%4,row=i/4;
            float x=.025f+col*.242f,y=.48f-row*.365f;
            var palette=new[]{new Color(1f,.88f,.89f),new Color(.91f,.96f,.80f),new Color(1f,.95f,.79f),new Color(1f,.88f,.89f),new Color(.83f,.91f,1f),new Color(1f,.95f,.79f),new Color(1f,.88f,.89f),new Color(.91f,.96f,.80f)};
            var card=Panel(p,"Game"+i,palette[i],x,y,x+.225f,y+.345f);
            var title=Label(card,"Title",Titles[i],24,.055f,.75f,.945f,.96f,true);title.alignment=TextAlignmentOptions.Center;
            AddIcon(card,"Icon",gameIcons[i],.045f,.28f,.43f,.75f);
            gameCounts[i]=Label(card,"Count","—",24,.47f,.48f,.96f,.73f,true);
            Label(card,"Description",Descriptions[i],15,.47f,.29f,.96f,.48f);
            ButtonAt(card,"Inspect"+i,"查看紀錄  ›",.14f,.055f,.86f,.27f,palette[i]);
        }
        detail=Label(p,"Detail","點選遊戲，查看自己的歷次表現。",18,.15f,.015f,.92f,.10f);
    }
    void OnEnable()
    {
        if(pages==null || pages.Length!=3 || !pages[2]) return;
        Bind(); SelectTab(selectedTab);
        if(Application.isPlaying){identity=LTC.Identity.PlayerIdentityService.Current;identity.IdentityChanged+=IdentityChanged;RequestCloud();}
        var oldNav=transform.parent.Find("底部導覽");
        if(oldNav){oldNavigation=oldNav.GetComponent<CanvasGroup>();if(!oldNavigation)oldNavigation=oldNav.gameObject.AddComponent<CanvasGroup>();savedAlpha=oldNavigation.alpha;savedInteractable=oldNavigation.interactable;savedRaycasts=oldNavigation.blocksRaycasts;oldNavigation.alpha=0;oldNavigation.interactable=false;oldNavigation.blocksRaycasts=false;}
        navigation=transform.parent.Find("底部導覽") as RectTransform;
        if(navigation)
        {
            navMin=navigation.anchorMin;navMax=navigation.anchorMax;navOffsetMin=navigation.offsetMin;navOffsetMax=navigation.offsetMax;
            navImages=navigation.GetComponentsInChildren<Image>(true);navColors=navImages.Select(i=>i.color).ToArray();
            navTexts=navigation.GetComponentsInChildren<TMP_Text>(true);navTextColors=navTexts.Select(t=>t.color).ToArray();
            Rect(navigation,.025f,.012f,.975f,.105f);
            foreach(var image in navImages) image.color=image.name=="統計"?teal:cream;
            foreach(var text in navTexts) text.color=ink;
        }
        var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(c=>c.name=="LTC Player ID Canvas");
        if(canvas){idCanvas=canvas.gameObject;idWasActive=idCanvas.activeSelf;idCanvas.SetActive(false);}
    }
    void OnDisable()
    {
        if(identity!=null){identity.IdentityChanged-=IdentityChanged;identity=null;}
        if(cloudRoutine!=null){StopCoroutine(cloudRoutine);cloudRoutine=null;}
        cloud=null;
        if(oldNavigation){oldNavigation.alpha=savedAlpha;oldNavigation.interactable=savedInteractable;oldNavigation.blocksRaycasts=savedRaycasts;oldNavigation=null;}
        if(!navigation || navColors==null) return;
        navigation.anchorMin=navMin;navigation.anchorMax=navMax;navigation.offsetMin=navOffsetMin;navigation.offsetMax=navOffsetMax;
        for(int i=0;i<navImages.Length;i++) if(navImages[i])navImages[i].color=navColors[i];
        for(int i=0;i<navTexts.Length;i++) if(navTexts[i])navTexts[i].color=navTextColors[i];
        if(idCanvas)idCanvas.SetActive(idWasActive);
        navColors=null;
    }
    void Bind()
    {
        if(tabs==null || pages==null) return;
        for(int i=0;i<tabs.Length;i++)
        {
            int n=i;
            tabs[i].onClick.RemoveAllListeners(); tabs[i].onClick.AddListener(()=>SelectTab(n));
        }
        for(int i=0;i<3;i++)
        {
            int n=new[]{7,30,90}[i];
            var b=pages[0].transform.Find("Range"+i).GetComponent<Button>();
            b.onClick.RemoveAllListeners(); b.onClick.AddListener(()=>{days=n;RequestCloud();});
        }
        for(int i=0;i<8;i++)
        {
            int n=i; var b=pages[2].transform.Find("Game"+i+"/Inspect"+i).GetComponent<Button>();
            b.onClick.RemoveAllListeners(); b.onClick.AddListener(()=>{selectedGame=n;SelectTab(0);RequestCloud();});
        }
        for(int page=0;page<2;page++){int pg=page;var next=pages[page].transform.Find("NextGame")?.GetComponent<Button>();if(next){next.onClick.RemoveAllListeners();next.onClick.AddListener(()=>{selectedGame++;if(selectedGame>=8)selectedGame=pg==1?0:-1;RequestCloud();});}}
        var refresh=transform.Find("StatisticsPrototype/RefreshCloud")?.GetComponent<Button>();if(refresh){refresh.onClick.RemoveAllListeners();refresh.onClick.AddListener(()=>{var service=identity as LTC.Identity.PlayerIdentityService;if(service!=null&&service.RequiresGoogleSignIn)UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");else RequestCloud();});}
        if(navigationButtons!=null){var names=new[]{"遊戲","統計","商店","寵物","我的"};for(int i=0;i<navigationButtons.Length;i++){string n=names[i];navigationButtons[i].onClick.RemoveAllListeners();navigationButtons[i].onClick.AddListener(()=>{var original=transform.parent.Find("底部導覽/"+n)?.GetComponent<Button>();if(original)original.onClick.Invoke();});}}
    }
    public void SelectTab(int index)
    {
        selectedTab=Mathf.Clamp(index,0,2);
        for(int i=0;i<3;i++) {pages[i].SetActive(i==selectedTab); tabs[i].image.color=i==selectedTab?colors[2]:Color.white;}
        if(Application.isPlaying){RenderCloud();return;}
        // Editor previews must never initialize a login service or read another
        // account's local history. Live values are supplied only by the API.
        if(selectedTab==0){chart.SetValues(Enumerable.Repeat(float.NaN,days).ToArray(),colors[2]);trendStatus.text="執行遊戲並登入後載入雲端統計";}
        if(selectedTab==2&&gameCounts!=null)foreach(var label in gameCounts)label.text="—";
    }
    void IdentityChanged(){cloud=null;RequestCloud();}
    void RequestCloud()
    {
        if(!Application.isPlaying)return;
        if(cloudRoutine!=null)StopCoroutine(cloudRoutine);
        cloud=null;cloudError="雲端資料讀取中…";RenderCloud();
        cloudRoutine=StartCoroutine(LTCStatisticsCloud.Fetch(days,selectedGame,(data,error)=>{cloud=data;cloudError=error;RenderCloud();}));
    }
    void RenderCloud()
    {
        if(pages==null||pages.Length<3)return;
        var refreshLabel=transform.Find("StatisticsPrototype/RefreshCloud/Label")?.GetComponent<TMP_Text>();if(refreshLabel){var service=identity as LTC.Identity.PlayerIdentityService;refreshLabel.text=service!=null&&service.RequiresGoogleSignIn?"Google 登入":"重新整理";}
        var values=Enumerable.Repeat(float.NaN,days).ToArray();
        if(cloud!=null){DateTime start;if(DateTime.TryParseExact(cloud.startDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out start)){foreach(var p in cloud.trend){DateTime date;if(DateTime.TryParseExact(p.date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date)){int i=(date-start).Days;if(i>=0&&i<days&&p.score>=0&&p.score<=100)values[i]=p.score;}}}}
        chart.SetValues(values,new Color(.81f,.37f,.43f));
        summary.text=selectedGame<0?"全部遊戲":Titles[selectedGame];
        string leftDate=(days-1)+" 天前",rightDate="今天";
        DateTime axisDate;
        if(cloud!=null&&DateTime.TryParseExact(cloud.startDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out axisDate))leftDate=axisDate.ToString("yyyy/MM/dd");
        if(cloud!=null&&DateTime.TryParseExact(cloud.endDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out axisDate))rightDate=axisDate.ToString("yyyy/MM/dd");
        rangeCaption.text=leftDate;
        var endCaption=pages[0].transform.Find("Today")?.GetComponent<TMP_Text>();if(endCaption)endCaption.text=rightDate;
        int count=values.Count(x=>!float.IsNaN(x));
        summaryValues[0].text=cloud==null?"—":count+" 天";summaryValues[1].text=cloud==null?"—":cloud.completed+" 場";summaryValues[2].text=cloud==null?"—":cloud.activeDays7+" 天";
        trendStatus.text=cloud==null?(cloudError??"等待雲端資料"):count==0?(days<90?"這段期間沒有有效評分，可切換到 90 天查看較早紀錄。":"最近 90 天尚無有效評分；完成場次仍會計入。") :count==1?"雲端紀錄：目前只有一天有效評分，尚不足以判斷趨勢。":"雲端每日有效評分平均；不同遊戲及難度請審慎比較。";
        for(int i=0;i<3;i++)pages[0].transform.Find("Range"+i).GetComponent<Image>().color=new[]{7,30,90}[i]==days?teal:colors[0];
        for(int i=0;i<8;i++){var g=cloud?.games.FirstOrDefault(x=>x.gameCode==LTCStatisticsCloud.Codes[i]);gameCounts[i].text=cloud==null?"—":g==null||g.completed==0?"尚無紀錄":"完成 "+g.completed+" 場";}
        detail.text=cloud==null?(cloudError??"等待雲端資料"):"雲端累計完成場次；點選遊戲查看歷次表現。";
        RenderCohort();
    }
    void RenderCohort()
    {
        var p=pages[1].transform;var c=cloud?.cohort;
        p.Find("Explanation").GetComponent<TMP_Text>().text=(selectedGame<0?"請先切換至一款遊戲":Titles[selectedGame])+" · 最近 "+days+" 天";
        string title=cloudError??"等待雲端資料",body="";
        bool show=c!=null&&(c.status=="ready"||c.status=="small_sample");
        if(c!=null){switch(c.status){case "select_game":title="請選擇一款遊戲";body="同齡參考不混合不同遊戲的分數。";break;case "missing_age":title="請先在「我的」填寫出生日期";body="年齡資料完整後，才能選取同齡段。";break;case "no_peers":title="目前沒有符合條件的其他玩家";body="使用同遊戲、同齡段、所選期間內的有效評分。";break;default:title=c.ageMin+"–"+c.ageMax+" 歲 · "+c.players+" 位參考玩家";body=c.status=="small_sample"?"樣本較少，僅供參考；不顯示百分位排名。":c.hasScore?"你的表現位於第 "+c.percentile.ToString("0")+" 百分位（非臨床常模）":"你尚無有效評分，先顯示同齡分布。";break;}}
        var t=p.Find("EmptyTitle").GetComponent<TMP_Text>();t.text=title;Rect(t.rectTransform,.045f,show?.62f:.44f,.615f,show?.72f:.56f);t.fontSizeMax=22;
        var b=p.Find("EmptyBody").GetComponent<TMP_Text>();b.text=body;Rect(b.rectTransform,.045f,show?.49f:.28f,.615f,show?.62f:.43f);b.fontSizeMax=18;
        string[] texts={"你的分數\n"+(c!=null&&c.hasScore?c.score.ToString("0.0"):"—"),"同齡中位數\n"+(show?c.median.ToString("0.0"):"—"),"參考玩家\n"+(c==null?"—":c.players+" 人")};
        for(int i=0;i<3;i++)p.Find("Metric"+i+"/Value").GetComponent<TMP_Text>().text=texts[i];
        var existing=p.Find("DistributionBars");if(existing)existing.gameObject.SetActive(show);
        if(!show)return;
        if(!existing){var holder=Panel(p,"DistributionBars",Color.clear,.045f,.17f,.615f,.48f);existing=holder;for(int i=0;i<5;i++){Panel(holder,"Bar"+i,teal,.04f+i*.19f,.16f,.17f+i*.19f,.8f);Label(holder,"N"+i,"",17,.02f+i*.19f,.81f,.19f+i*.19f,1);Label(holder,"X"+i,new[]{"0–19","20–39","40–59","60–79","80–100"}[i],15,.02f+i*.19f,0,.19f+i*.19f,.15f);}}
        int max=c.bins==null||c.bins.Length!=5?1:Math.Max(1,c.bins.Max());
        for(int i=0;i<5;i++){int n=c.bins!=null&&c.bins.Length==5?c.bins[i]:0;Rect((RectTransform)existing.Find("Bar"+i),.04f+i*.19f,.16f,.17f+i*.19f,.16f+.63f*n/max);existing.Find("N"+i).GetComponent<TMP_Text>().text=n+" 人";}
    }
    void RefreshTrend()
    {
        if(!chart) return;
        var values=CognitiveAssessmentService.BuildDailyTrend(null,days,selectedGame<0?null:GameIds[selectedGame]);
        chart.SetValues(values,new Color(.81f,.37f,.43f));
        int count=values.Count(v=>!float.IsNaN(v));
        summary.text=selectedGame<0?"全部遊戲":Titles[selectedGame];
        var history=CognitiveAssessmentService.GetStatisticsHistory().Where(s=>(selectedGame<0||s.gameId==GameIds[selectedGame])&&DateTimeOffset.FromUnixTimeMilliseconds(s.endedAtUnixMs).UtcDateTime.Date>=DateTime.UtcNow.Date.AddDays(1-days)).ToList();
        if(summaryValues!=null){summaryValues[0].text=count+" 天";summaryValues[1].text=history.Count+" 場";summaryValues[2].text=history.Where(s=>DateTimeOffset.FromUnixTimeMilliseconds(s.endedAtUnixMs).UtcDateTime.Date>=DateTime.UtcNow.Date.AddDays(-6)).Select(s=>DateTimeOffset.FromUnixTimeMilliseconds(s.endedAtUnixMs).UtcDateTime.Date).Distinct().Count()+" 天";}
        rangeCaption.text=(days-1)+" 天前";
        trendStatus.text=count==0?"尚無紀錄，完成遊戲後再來看看。":count==1?"目前只有一天紀錄，尚不足以觀察趨勢。":"個人遊戲表現紀錄；不同難度的結果請審慎比較。";
        for(int i=0;i<3;i++) pages[0].transform.Find("Range"+i).GetComponent<Image>().color=new[]{7,30,90}[i]==days?teal:colors[0];
    }
    RectTransform Panel(Transform parent,string name,Color color,float x,float y,float xx,float yy)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform; Rect(r,x,y,xx,yy); var image=go.GetComponent<Image>();image.color=color;image.sprite=panelSkin;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;
        return r;
    }
    TMP_Text Label(Transform parent,string name,string value,float size,float x,float y,float xx,float yy,bool bold=false)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>(); t.font=font; t.text=value; t.fontSize=size; t.color=ink;
        t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal; t.alignment=TextAlignmentOptions.MidlineLeft;
        t.enableAutoSizing=true;t.fontSizeMin=size*.8f;t.fontSizeMax=size;t.raycastTarget=false;
        Rect(t.rectTransform,x,y,xx,yy); return t;
    }
    Button ButtonAt(Transform parent,string name,string title,float x,float y,float xx,float yy,Color color)
    {
        var r=Panel(parent,name,color,x,y,xx,yy); var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
        var t=Label(r,"Label",title,24,.04f,.04f,.96f,.96f,true);t.alignment=TextAlignmentOptions.Center;return b;
    }
    void AddIcon(Transform p,string n,Sprite s,float x,float y,float xx,float yy){var go=new GameObject(n,typeof(RectTransform),typeof(Image));go.transform.SetParent(p,false);Rect((RectTransform)go.transform,x,y,xx,yy);var image=go.GetComponent<Image>();image.sprite=s;image.preserveAspect=true;image.raycastTarget=false;}
    void LateUpdate(){if(!idCanvas){var c=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(x=>x.name=="LTC Player ID Canvas");if(c){idCanvas=c.gameObject;idWasActive=idCanvas.activeSelf;}}if(idCanvas)idCanvas.SetActive(false);}
    static void Rect(RectTransform r,float x,float y,float xx,float yy)
    { r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero; }
}
