using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>The final card in the catalog's horizontal list.</summary>
public sealed class LTCChallengeEntry : MonoBehaviour
{
    public const string SceneName = "ChallengeRush";
    LTC.Identity.IPlayerIdentityProvider identity;
    public static void Ensure(Transform canvas)
    {
        var content=canvas?.Find("遊戲首頁/能力分類滑動區/Viewport/Content");
        if(content==null||content.childCount==0)return;
        var existing=content.Find("挑戰模式");
        if(existing!=null)
        {
            existing.GetComponent<LTCChallengeEntry>()?.EnsureBestLabel();
            return;
        }
        var card=Instantiate(content.GetChild(content.childCount-1).gameObject,content,false);
        card.name="挑戰模式";card.transform.SetAsLastSibling();
        var title=card.transform.Find("能力名稱")?.GetComponent<TMP_Text>();
        if(title!=null)title.text="挑戰模式";
        var summary=card.transform.Find("能力說明")?.GetComponent<TMP_Text>();
        if(summary!=null)summary.text="10 秒認知 RUSH，連擊挑戰最高分。";
        var badge=card.transform.Find("能力徽章/文字")?.GetComponent<TMP_Text>();
        if(badge!=null)badge.text="戰";
        var buttons=card.GetComponentsInChildren<Button>(true);
        for(int i=0;i<buttons.Length;i++)
        {
            if(i>0){buttons[i].gameObject.SetActive(false);continue;}
            buttons[i].name="開始挑戰";buttons[i].interactable=true;
            buttons[i].onClick=new Button.ButtonClickedEvent();
            var text=buttons[i].GetComponentInChildren<TMP_Text>(true);
            if(text!=null)text.text="開始挑戰　›";
        }
        card.AddComponent<LTCChallengeEntry>().EnsureBestLabel();
    }
    public void EnsureBestLabel()
    {
        var existing=transform.Find("歷史最高分");
        TMP_Text label;
        if(existing==null)
        {
            var go=new GameObject("歷史最高分",typeof(RectTransform),typeof(TextMeshProUGUI));
            go.transform.SetParent(transform,false);
            label=go.GetComponent<TMP_Text>();
            var source=transform.Find("開始挑戰")?.GetComponentInChildren<TMP_Text>(true);
            if(source!=null){label.font=source.font;label.color=source.color;}
            label.fontSize=30;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            LTCChallengeUI.Place(label.rectTransform,.06f,.08f,.94f,.30f);
        }
        else label=existing.GetComponent<TMP_Text>();
        label.text=Application.isPlaying ? "歷史最高分："+LTCChallengeScores.Best(LTCChallengeScores.CurrentPlayerKey).ToString("N0")+" 分" : "歷史最高分：0 分";
    }
    void Start()
    {
        identity=LTC.Identity.PlayerIdentityService.Current;
        identity.IdentityChanged+=EnsureBestLabel;
        EnsureBestLabel();
        var button=transform.Find("開始挑戰")?.GetComponent<Button>();
        if(button!=null)button.onClick.AddListener(()=>SceneManager.LoadScene(SceneName));
    }
    void OnDestroy(){if(identity!=null)identity.IdentityChanged-=EnsureBestLabel;}
}
