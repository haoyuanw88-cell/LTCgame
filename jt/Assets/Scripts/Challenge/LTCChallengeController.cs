using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Dedicated RUSH scene. Scores live only in this controller's current run.</summary>
public sealed class LTCChallengeController : MonoBehaviour
{
    public LTCChallengeAssets assets;
    public LTCChallengeRun Run { get; private set; }
    public LTCChallengeRoundView CurrentRound { get; private set; }
    public float Remaining { get; private set; } = 10f;
    public bool IsPaused { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool HasStarted { get; private set; }
    LTCChallengeUI ui;
    RectTransform root, roundViewport;
    TMP_Text score, multiplier, hearts, enemyHealth, timer, progress, feedback, enemyLabel;
    Image enemy, player, timerFill;
    GameObject intro, pause, results;
    AudioSource music;
    readonly Queue<LTCChallengeKind> bag = new Queue<LTCChallengeKind>();
    int lastKind = -1, lastEnemy = -1;
    string runPlayerKey;

    void Start()
    {
        if (assets == null) assets = Resources.Load<LTCChallengeAssets>("ChallengeRushAssets");
        if (assets == null) { Debug.LogError("ChallengeRushAssets is missing."); return; }
        ui = new LTCChallengeUI(assets);
        BuildUI();
        music = gameObject.AddComponent<AudioSource>();
        music.clip = assets.music; music.loop = true; music.playOnAwake = false;
        music.volume = PlayerPrefs.GetFloat("LTC.MusicVolume", .18f); music.spatialBlend = 0;
        if (music.clip != null) music.Play();
    }

    void BuildUI()
    {
        var canvasObject = new GameObject("Challenge Rush Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
        canvasObject.transform.SetParent(transform,false);
        root = ui.Panel(canvasObject.transform,"挑戰畫面",ui.Cream,0,0,1,1,false);
        var fitter=root.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=16f/9f;
        if(assets.background!=null){root.GetComponent<Image>().sprite=assets.background;root.GetComponent<Image>().color=Color.white;}
        ui.Text(root,"模式標題","認知 RUSH 挑戰",40,.23f,.915f,.77f,.995f);
        ui.Button(root,"回首頁","返回首頁",ui.Cream,.035f,.925f,.18f,.985f,ReturnHome);
        ui.Button(root,"暫停","暫停",ui.Blue,.72f,.925f,.85f,.985f,()=>SetPaused(true));
        var battle=ui.Panel(root,"戰鬥區",new Color(.86f,.92f,.84f),.045f,.64f,.955f,.91f);
        ui.Panel(battle,"地面",new Color(.68f,.59f,.43f),.03f,.13f,.97f,.18f,false).GetComponent<Image>().raycastTarget=false;
        player=ui.Sprite(battle,"玩家",assets.player,.49f,.12f,.71f,.97f);
        hearts=ui.Text(battle,"玩家生命","生命 3 / 3",27,.48f,0,.72f,.19f);
        var info=ui.Panel(battle,"玩家右側分數",ui.Cream,.73f,.21f,.97f,.93f);
        ui.Text(info,"分數標題","本次分數",20,.04f,.76f,.96f,.96f);
        score=ui.Text(info,"分數","0",43,.04f,.32f,.96f,.78f);
        score.enableAutoSizing=true;score.fontSizeMin=25;score.fontSizeMax=43;
        multiplier=ui.Text(info,"倍率","倍率 ×1.0",31,.04f,.04f,.96f,.33f);
        enemy=ui.Sprite(battle,"敵人",null,.03f,.20f,.28f,.98f);
        enemyHealth=ui.Text(battle,"敵人生命","",27,.015f,0,.30f,.20f);
        enemyLabel=ui.Text(battle,"敵人名稱","",20,.29f,.79f,.39f,.98f);
        feedback=ui.Text(battle,"戰鬥訊息","完成關卡，攻擊敵人！",24,.29f,.30f,.48f,.78f);
        var timeBar=ui.Panel(root,"倒數底板",ui.Cream,.05f,.575f,.95f,.635f);
        timerFill=ui.Panel(timeBar,"剩餘時間條",ui.Sage,.01f,.12f,.99f,.88f).GetComponent<Image>();
        timerFill.raycastTarget=false;
        timer=ui.Text(timeBar,"倒數文字","每關 10 秒",29,.25f,.02f,.75f,.98f);
        progress=ui.Text(timeBar,"完成關數","完成 0 關",22,.01f,.02f,.25f,.98f);
        roundViewport=ui.Panel(root,"關卡滑動區",Color.clear,.05f,.055f,.95f,.565f,false);
        roundViewport.gameObject.AddComponent<RectMask2D>();
        ui.Text(root,"模式說明","挑戰最高分僅儲存在本機，不列入認知紀錄",20,.12f,.005f,.88f,.05f);
        intro=Overlay("開始挑戰", "認知遊戲 RUSH", "每關 10 秒，完成即攻擊敵人 1 點。\n失敗扣 1 點生命，3 點生命用完即結束。\n連續成功：倍率每關 +0.5，最高 ×5.0。\n攻擊 50 分，擊倒再加 100 分，皆乘目前倍率。\n卡牌先記憶、翻面，再開始倒數。", "開始挑戰", StartRun);
        pause=Overlay("暫停畫面","挑戰暫停","倒數與記憶展示已暫停。\n繼續後保留目前關卡與分數。","繼續挑戰",()=>SetPaused(false));
        pause.SetActive(false);
    }

    GameObject Overlay(string name,string title,string body,string action,System.Action callback)
    {
        var overlay=ui.Panel(root,name,new Color(.18f,.16f,.12f,.75f),0,0,1,1,false);
        var card=ui.Panel(overlay,"內容",ui.Cream,.18f,.14f,.82f,.86f);
        ui.Text(card,"標題",title,42,.06f,.78f,.94f,.95f);
        ui.Text(card,"說明",body,28,.07f,.27f,.93f,.77f);
        ui.Button(card,"主要按鈕",action,ui.Sage,.12f,.075f,.56f,.22f,callback);
        ui.Button(card,"返回首頁","返回首頁",ui.Blue,.61f,.075f,.88f,.22f,ReturnHome);
        return overlay.gameObject;
    }

    public void StartRun()
    {
        StopAllCoroutines();
        if(CurrentRound!=null){CurrentRound.gameObject.SetActive(false);Destroy(CurrentRound.gameObject);}
        if(results!=null){results.SetActive(false);Destroy(results);}
        intro.SetActive(false);pause.SetActive(false);IsPaused=false;IsTransitioning=false;
        runPlayerKey=LTCChallengeScores.CurrentPlayerKey;
        Run=new LTCChallengeRun(UnityEngine.Random.Range(1,int.MaxValue)); bag.Clear();lastKind=-1;
        HasStarted=true; SelectEnemy();feedback.text="完成關卡，攻擊敵人！";
        CreateRound(NextKind()); RefreshHUD();
    }
    LTCChallengeKind NextKind()
    {
        if(bag.Count==0)
        {
            var kinds=System.Enum.GetValues(typeof(LTCChallengeKind)).Cast<LTCChallengeKind>().OrderBy(_=>UnityEngine.Random.value).ToList();
            if((int)kinds[0]==lastKind){var tmp=kinds[0];kinds[0]=kinds[1];kinds[1]=tmp;}
            foreach(var kind in kinds)bag.Enqueue(kind);
        }
        var next=bag.Dequeue();lastKind=(int)next;return next;
    }
    void CreateRound(LTCChallengeKind kind)
    {
        var panel=ui.Panel(roundViewport,"隨機關卡_"+kind,ui.Cream,0,0,1,1);
        CurrentRound=panel.gameObject.AddComponent<LTCChallengeRoundView>();
        CurrentRound.Initialize(ui,kind,RoundFinished);
        CurrentRound.IsPaused=IsPaused||IsTransitioning;Remaining=10f;RefreshTimer();
    }

    void Update()
    {
        if(!HasStarted||Run.IsOver||IsPaused||IsTransitioning||CurrentRound==null)return;
        if(!CurrentRound.IsReady){timer.text="記憶展示中 · 完成後開始";return;}
        Remaining=Mathf.Max(0,Remaining-Time.deltaTime);RefreshTimer();
        if(Remaining<=0)CurrentRound.Expire();
    }
    void RefreshTimer()
    {
        if(timer==null)return;
        timer.text=Remaining.ToString("0.0")+" 秒";
        LTCChallengeUI.Place(timerFill.rectTransform,.01f,.12f,.01f+.98f*Remaining/10f,.88f);
        timerFill.color=Remaining<=3?ui.Rose:ui.Sage;
    }
    void RefreshHUD()
    {
        score.text=Run.Score.ToString("N0");multiplier.text="倍率 ×"+Run.Multiplier.ToString("0.0");
        hearts.text="生命 "+Run.Health+" / 3";hearts.color=Run.Health==1?new Color(.75f,.15f,.13f):ui.Ink;
        enemyHealth.text="敵人生命 "+Run.EnemyHealth+" / "+Run.EnemyMaxHealth;
        progress.text="完成 "+Run.Cleared+" 關";
    }
    void SelectEnemy()
    {
        var candidates=assets.enemies.levels.Where(l=>l!=null&&l.enemySprite!=null).GroupBy(l=>l.enemySprite).Select(g=>g.First()).ToList();
        int index=UnityEngine.Random.Range(0,candidates.Count);
        if(candidates.Count>1&&index==lastEnemy)index=(index+1)%candidates.Count;
        lastEnemy=index;var level=candidates[index];
        enemy.sprite=level.enemySprite;enemy.color=Color.white;
        enemy.rectTransform.localScale=new Vector3(level.enemyFacesLeft?-1:1,1,1);
        enemyLabel.text="敵人 "+(Run.Defeated+1);
    }

    void RoundFinished(bool success)
    {
        if(IsTransitioning||IsPaused||Run==null||Run.IsOver)return;
        if(!Run.Resolve(success))return;
        LTCChallengeScores.Record(Run.Score,runPlayerKey);
        IsTransitioning=true;CurrentRound.IsPaused=true;
        feedback.text=success?"攻擊！\n+"+Run.LastAward+" 分": "受到攻擊\n倍率重設";
        if(success&&Run.NeedsEnemy)feedback.text="擊倒！\n+"+Run.LastAward+" 分";
        RefreshHUD();StartCoroutine(Advance(success));
    }
    IEnumerator Wait(float seconds)
    {
        float t=0;while(t<seconds){if(!IsPaused)t+=Time.deltaTime;yield return null;}
    }
    IEnumerator AnimateAttack(bool success)
    {
        var actor=success?player:enemy;var victim=success?enemy:player;
        var actorRect=actor.rectTransform;var original=actorRect.anchoredPosition;
        float direction=Mathf.Sign(victim.rectTransform.position.x-actorRect.position.x);
        Sprite initial=player.sprite;var frames=success?assets.playerAttack:assets.playerHit;
        for(float t=0;t<.38f;)
        {
            if(!IsPaused)
            {
                t+=Time.deltaTime;
                actorRect.anchoredPosition=original+Vector2.right*direction*Mathf.Sin(t/.38f*Mathf.PI)*70;
                victim.color=Color.Lerp(Color.white,ui.Rose,Mathf.Sin(t/.38f*Mathf.PI));
                if(frames!=null&&frames.Length>0)player.sprite=frames[Mathf.Min(frames.Length-1,(int)(t/.38f*frames.Length))];
            }
            yield return null;
        }
        actorRect.anchoredPosition=original;victim.color=Color.white;player.sprite=initial;
        if(success&&Run.NeedsEnemy) { enemy.color=new Color(1,1,1,.25f);yield return Wait(.18f); }
    }
    IEnumerator Advance(bool success)
    {
        yield return AnimateAttack(success);
        if(Run.IsOver)
        {
            IsTransitioning=false;CurrentRound.gameObject.SetActive(false);
            results=BuildResults();
            yield break;
        }
        if(Run.NeedsEnemy){Run.SpawnEnemy();SelectEnemy();}
        var outgoing=CurrentRound;var oldRect=(RectTransform)outgoing.transform;
        CreateRound(NextKind());var incoming=(RectTransform)CurrentRound.transform;
        float width=roundViewport.rect.width;incoming.anchoredPosition=new Vector2(width,0);
        for(float t=0;t<.32f;)
        {
            if(!IsPaused)
            {
                t+=Time.deltaTime;float f=Mathf.SmoothStep(0,1,t/.32f);
                oldRect.anchoredPosition=new Vector2(-width*f,0);incoming.anchoredPosition=new Vector2(width*(1-f),0);
            }
            yield return null;
        }
        outgoing.gameObject.SetActive(false);Destroy(outgoing.gameObject);
        incoming.anchoredPosition=Vector2.zero;IsTransitioning=false;CurrentRound.IsPaused=IsPaused;
        feedback.text="完成關卡，攻擊敵人！";RefreshHUD();RefreshTimer();
    }

    GameObject BuildResults()
    {
        var overlay=ui.Panel(root,"挑戰結算",new Color(.18f,.16f,.12f,.75f),0,0,1,1,false);
        var card=ui.Panel(overlay,"內容",ui.Cream,.13f,.09f,.87f,.91f);
        var title=ui.Text(card,"標題","挑戰結束",52,.06f,.83f,.94f,.97f);
        title.fontStyle=FontStyles.Bold;
        var mainScore=ui.Text(card,"本次分數","本次分數　"+Run.Score.ToString("N0"),58,.05f,.65f,.95f,.84f);
        mainScore.fontStyle=FontStyles.Bold;mainScore.enableAutoSizing=true;mainScore.fontSizeMin=42;mainScore.fontSizeMax=58;
        ui.Text(card,"歷史最高分","歷史最高分　"+LTCChallengeScores.Best(runPlayerKey).ToString("N0"),36,.06f,.54f,.94f,.66f);
        ui.Text(card,"完成關卡","完成關卡　"+Run.Cleared,38,.08f,.42f,.92f,.54f);
        ui.Text(card,"擊倒敵人","擊倒敵人　"+Run.Defeated,38,.08f,.30f,.52f,.43f);
        ui.Text(card,"最高倍率","最高倍率　×"+Run.BestMultiplier.ToString("0.0"),38,.50f,.30f,.94f,.43f);
        ui.Text(card,"本機紀錄說明","最高分儲存於本機，不會寫入資料庫。",26,.06f,.20f,.94f,.29f);
        var retry=ui.Button(card,"主要按鈕","再挑戰一次",ui.Sage,.10f,.055f,.55f,.19f,StartRun);
        var home=ui.Button(card,"返回首頁","返回首頁",ui.Blue,.60f,.055f,.90f,.19f,ReturnHome);
        foreach(var button in new[]{retry,home})button.GetComponentInChildren<TMP_Text>().fontSize=36;
        return overlay.gameObject;
    }

    public void SetPaused(bool value)
    {
        if(!HasStarted||Run==null||Run.IsOver)return;
        IsPaused=value;if(CurrentRound!=null)CurrentRound.IsPaused=value||IsTransitioning;
        pause.SetActive(value);if(value)pause.transform.SetAsLastSibling();
    }
    void OnApplicationFocus(bool focus) { if(!focus&&HasStarted)SetPaused(true); }
    void OnApplicationPause(bool paused) { if(paused&&HasStarted)SetPaused(true); }
    public void ReturnHome() { SceneManager.LoadScene("GameScene"); }
}
