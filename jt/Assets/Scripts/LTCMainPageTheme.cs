using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LTC.Identity;

// Styles existing, editor-authored controls, preserving their controller paths and events.
public sealed class LTCMainPageTheme : MonoBehaviour
{
    [SerializeField] TMP_Text playerId;
    [SerializeField] Sprite panel;
    [SerializeField] TMP_FontAsset font;
    Transform games, profile, navigation;
    IPlayerIdentityProvider identity;
    readonly Color ink = new Color(.25f,.19f,.15f);
    readonly Color cream = new Color(1f,.975f,.91f);
    readonly Color rose = new Color(.92f,.73f,.77f);
    readonly Color sage = new Color(.85f,.92f,.73f);
    readonly Color blue = new Color(.80f,.89f,.95f);

    public void ApplyStyle(LTCStatisticsPrototype source)
    {
        panel=source.panelSkin;
        font=source.transform.Find("StatisticsPrototype").GetComponentsInChildren<TMP_Text>(true).First(t=>t.font!=null).font;
        games=transform.Find("遊戲首頁"); profile=transform.Find("我的頁"); navigation=transform.Find("底部導覽");
        foreach(var page in new[]{games,profile})
        {
            var image=page.GetComponent<Image>(); image.sprite=source.background;image.color=Color.white;image.type=Image.Type.Simple;
            foreach(var text in page.GetComponentsInChildren<TMP_Text>(true))
            {text.font=font;text.color=ink; text.enableAutoSizing=true;text.fontSizeMin=text.fontSize*.8f;text.fontSizeMax=text.fontSize;}
            foreach(var button in page.GetComponentsInChildren<Button>(true)) Skin(button.GetComponent<Image>(),sage);
            foreach(var shadow in page.GetComponentsInChildren<Shadow>(true))shadow.enabled=false;
            Label(page,"ThemeNote","與遊戲相伴\n天天都有好心情",20,.03f,.915f,.17f,.98f);
        }

        var header=games.Find("玩家資訊列");Place(header,.22f,.89f,.89f,.985f);Skin(header.GetComponent<Image>(),cream);
        Place(header.Find("頭像"),.02f,.1f,.12f,.9f);
        Place(header.Find("玩家名稱"),.14f,.15f,.42f,.85f);
        Place(header.Find("成就任務"),.43f,.16f,.68f,.84f);
        Place(header.Find("金幣區"),.70f,.16f,.98f,.84f);
        Skin(header.Find("金幣區").GetComponent<Image>(),new Color(1f,.91f,.65f));
        Place(header.Find("金幣區/金幣數量"),.08f,.08f,.94f,.92f);
        Place(games.Find("主標題"),.07f,.745f,.92f,.85f);
        games.Find("主標題").GetComponent<TMP_Text>().text="今天，一起玩點什麼？";
        Place(games.Find("副標題"),.07f,.68f,.92f,.745f);
        var scroll=games.Find("能力分類滑動區");Place(scroll,.065f,.205f,.935f,.67f);scroll.GetComponent<Image>().color=Color.clear;
        var content=scroll.Find("Viewport/Content");((RectTransform)content).anchoredPosition=Vector2.zero;int n=0;
        foreach(Transform card in content)
        {
            var tint=new[]{rose,sage,blue,new Color(1f,.91f,.70f)}[n++%4];Skin(card.GetComponent<Image>(),tint);
            var layout=card.GetComponent<LayoutElement>();if(layout){layout.preferredWidth=410;layout.minWidth=410;layout.preferredHeight=310;layout.minHeight=290;}
            foreach(var button in card.GetComponentsInChildren<Button>(true))Skin(button.GetComponent<Image>(),cream);
            Skin(card.Find("能力徽章")?.GetComponent<Image>(),cream);
        }
        Label(games,"ThemeFooter","左右滑動，找到喜歡的遊戲",18,.2f,.135f,.8f,.185f).alignment=TextAlignmentOptions.Center;

        Place(profile.Find("我的標題"),.25f,.895f,.75f,.98f);
        profile.Find("我的標題").GetComponent<TMP_Text>().text="我的小天地";
        var body=profile.Find("個人資料內容");Place(body,.075f,.185f,.925f,.845f);body.GetComponent<Image>().color=Color.clear;
        Place(body.Find("名稱標題"),.025f,.88f,.40f,.98f);
        Place(body.Find("名稱輸入"),.025f,.74f,.42f,.88f);Skin(body.Find("名稱輸入").GetComponent<Image>(),cream);
        Place(body.Find("儲存名稱"),.44f,.74f,.65f,.88f);
        Place(body.Find("名稱狀態"),.025f,.66f,.65f,.735f);
        var cardProfile=body.Find("基本資料卡");Place(cardProfile,.025f,.01f,.65f,.63f);Skin(cardProfile.GetComponent<Image>(),sage);
        Place(cardProfile.Find("標題"),.055f,.82f,.95f,.96f);
        string[] rows={"出生年月日","性別","教育程度","持有金幣","有效紀錄"};
        for(int i=0;i<rows.Length;i++)Place(cardProfile.Find(rows[i]),.055f,.64f-i*.15f,.95f,.79f-i*.15f);
        var idCard=Panel(body,"PlayerIdentity",blue,.69f,.70f,.975f,.975f);
        Label(idCard,"Caption","玩家 ID",23,.08f,.57f,.92f,.88f);
        playerId=Label(idCard,"Value","登入後顯示",29,.08f,.10f,.92f,.54f);
        Place(body.Find("編輯基本資料"),.69f,.50f,.975f,.65f);
        Place(body.Find("設定"),.69f,.31f,.975f,.46f);Skin(body.Find("設定").GetComponent<Image>(),blue);
        var daily=body.Find("每日登入");Place(daily,.69f,.04f,.975f,.26f);
        var oldDaily=daily.GetComponent<DailyLoginCircleGraphic>();if(oldDaily)oldDaily.enabled=false;
        var dailySkin=Panel(daily,"ThemeBackground",new Color(1f,.91f,.70f),0,0,1,1);dailySkin.SetAsFirstSibling();
        daily.GetComponent<Button>().targetGraphic=dailySkin.GetComponent<Image>();
        Label(profile,"ThemeFooter","每天一點小練習，累積自己的進步",18,.20f,.13f,.80f,.175f).alignment=TextAlignmentOptions.Center;

        Place(navigation,.035f,.006f,.965f,.101f);Skin(navigation.GetComponent<Image>(),Color.white);
        string[] names={"遊戲","統計","商店","寵物","我的"};
        for(int i=0;i<names.Length;i++)
        {
            var b=navigation.Find(names[i]);Place(b,i*.2f+.003f,.04f,(i+1)*.2f-.003f,.96f);Skin(b.GetComponent<Image>(),cream);
            var label=b.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            if(label){Place(label.transform,.40f,.08f,.94f,.92f);label.font=font;label.color=ink;label.fontSize=28;label.enableAutoSizing=true;label.fontSizeMin=22;label.fontSizeMax=28;}
            var icon=Panel(b,"ThemeIcon",Color.white,.10f,.10f,.36f,.90f).GetComponent<Image>();icon.sprite=source.navigationIcons[i];icon.type=Image.Type.Simple;icon.preserveAspect=true;icon.raycastTarget=false;
        }
    }

    void Start()
    {
        games=transform.Find("遊戲首頁");profile=transform.Find("我的頁");navigation=transform.Find("底部導覽");
        identity=PlayerIdentityService.Current;identity.IdentityChanged+=RefreshIdentity;RefreshIdentity();
        // The progression controller can create its header button during Start.
        Invoke(nameof(StyleHeaderActions),0f);
    }
    void OnDestroy(){if(identity!=null)identity.IdentityChanged-=RefreshIdentity;}
    void StyleHeaderActions()
    {
        var b=games.Find("玩家資訊列/成就任務");if(!b)return;
        Place(b,.43f,.16f,.68f,.84f);Skin(b.GetComponent<Image>(),sage);
        foreach(var text in b.GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.color=ink;}
    }
    void RefreshIdentity(){if(playerId)playerId.text=identity!=null&&identity.IsReady?identity.PlayerCode:"尚未登入";}
    void LateUpdate()
    {
        if(!navigation||(!games.gameObject.activeSelf&&!profile.gameObject.activeSelf))return;
        foreach(Transform item in navigation){var img=item.GetComponent<Image>();if(img)img.color=(item.name=="遊戲"&&games.gameObject.activeSelf)||(item.name=="我的"&&profile.gameObject.activeSelf)?rose:cream;}
    }
    void Skin(Image image,Color color){if(!image)return;image.sprite=panel;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;image.color=color;}
    Transform Panel(Transform parent,string name,Color color,float x,float y,float xx,float yy)
    {
        var t=parent.Find(name);if(!t){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);t=go.transform;}
        Place(t,x,y,xx,yy);Skin(t.GetComponent<Image>(),color);return t;
    }
    TMP_Text Label(Transform parent,string name,string value,float size,float x,float y,float xx,float yy)
    {
        var t=parent.Find(name);if(!t){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);t=go.transform;}
        Place(t,x,y,xx,yy);var label=t.GetComponent<TMP_Text>();label.font=font;label.text=value;label.fontSize=size;label.color=ink;label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=size*.8f;label.fontSizeMax=size;return label;
    }
    static void Place(Transform t,float x,float y,float xx,float yy){if(!t)return;var r=t as RectTransform;r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=r.offsetMax=Vector2.zero;}
}
