using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Short, self-contained rounds; never instantiates a game's recording manager.</summary>
public sealed class LTCChallengeRoundView : MonoBehaviour
{
    public LTCChallengeKind Kind { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsResolved { get; private set; }
    public bool IsPaused { get; set; }
    public static readonly string[] Titles = { "翻牌記憶", "顏色文字判斷", "數字由小到大", "數字組合加總", "旋轉接水管", "超市採購", "文字判斷", "地鼠反應" };
    LTCChallengeUI ui;
    RectTransform area;
    TMP_Text instruction;
    Action<bool> onFinished;
    readonly List<Button> buttons = new List<Button>();
    readonly List<int> values = new List<int>();
    readonly List<Image> images = new List<Image>();
    bool busy;
    int firstCard = -1, matched, expected, sum, target, hits, moleIndex;
    float moleTime;
    Sprite[] pairFaces;
    int[] pipeBase, rotations;
    int pipeStart, pipeEnd, pipeStartDirection, pipeEndDirection;
    readonly List<int> shoppingTargets = new List<int>();
    readonly HashSet<int> shoppingChosen = new HashSet<int>();

    public void Initialize(LTCChallengeUI toolkit, LTCChallengeKind kind, Action<bool> finished)
    {
        ui = toolkit; Kind = kind; onFinished = finished;
        ui.Text(transform, "關卡名稱", Titles[(int)kind], 32, .04f, .83f, .96f, .98f);
        instruction = ui.Text(transform, "操作提示", "", 25, .04f, .70f, .96f, .84f);
        area = ui.Panel(transform, "操作區", Color.clear, .04f, .04f, .96f, .69f, false);
        switch (kind)
        {
            case LTCChallengeKind.Cards: BuildCards(); break;
            case LTCChallengeKind.Stroop: BuildStroop(); break;
            case LTCChallengeKind.NumberOrder: BuildOrder(); break;
            case LTCChallengeKind.NumberSum: BuildSum(); break;
            case LTCChallengeKind.Pipes: BuildPipes(); break;
            case LTCChallengeKind.Shopping: BuildShopping(); break;
            case LTCChallengeKind.Quiz: BuildQuiz(); break;
            case LTCChallengeKind.Gopher: BuildGopher(); break;
        }
    }

    bool CanInput => IsReady && !IsResolved && !IsPaused && !busy;
    public void Expire() { if (IsReady && !IsResolved && !IsPaused) Finish(false); }
    void Finish(bool success)
    {
        if (IsResolved) return;
        IsResolved = true; IsReady = false;
        foreach (var button in buttons) button.interactable = false;
        onFinished?.Invoke(success);
    }
    void Ready(string text) { instruction.text = text; IsReady = true; }
    void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        { int j = UnityEngine.Random.Range(0, i + 1); T temp = list[i]; list[i] = list[j]; list[j] = temp; }
    }
    Button Choice(string name, string label, float x, float y, float xx, float yy, Action action)
    {
        var b = ui.Button(area, name, label, ui.Sage, x, y, xx, yy, () => { if (CanInput) action(); });
        buttons.Add(b); return b;
    }
    IEnumerator Delay(float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        {
            if (!IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void BuildCards()
    {
        instruction.text = "先記住圖案，翻面後才開始倒數";
        values.AddRange(new[] { 0, 0, 1, 1, 2, 2 }); Shuffle(values);
        pairFaces = ui.Assets.cardFaces.OrderBy(_ => UnityEngine.Random.value).Take(3).ToArray();
        for (int i = 0; i < 6; i++)
        {
            int index = i; float x = .035f + i * .157f;
            var b = Choice("卡牌_" + i, "", x, .02f, x + .145f, .97f, () => SelectCard(index));
            images.Add(ui.Sprite(b.transform, "牌面", pairFaces[values[i]], .08f, .08f, .92f, .92f));
        }
        StartCoroutine(CardPreview());
    }
    IEnumerator CardPreview()
    {
        yield return Delay(2f);
        yield return FlipAll(false);
        Ready("找出全部 3 組配對；配錯會失去 1 點生命");
    }
    IEnumerator FlipAll(bool faceUp)
    {
        for (float t = 0; t < .24f; t += Time.deltaTime)
        {
            if (IsPaused) { yield return null; t -= Time.deltaTime; continue; }
            float scale = Mathf.Abs(1f - 2f * t / .24f);
            for (int i = 0; i < images.Count; i++)
            {
                images[i].transform.localScale = new Vector3(scale, 1, 1);
                if (t >= .12f) images[i].sprite = faceUp ? pairFaces[values[i]] : ui.Assets.cardBack;
            }
            yield return null;
        }
        for (int i = 0; i < images.Count; i++)
        { images[i].transform.localScale = Vector3.one; images[i].sprite = faceUp ? pairFaces[values[i]] : ui.Assets.cardBack; }
    }
    void SelectCard(int index)
    {
        if (!buttons[index].interactable || firstCard == index) return;
        images[index].sprite = pairFaces[values[index]];
        if (firstCard < 0) { firstCard = index; return; }
        int previous = firstCard; firstCard = -1;
        busy = true;
        StartCoroutine(CheckPair(previous, index));
    }
    IEnumerator CheckPair(int a, int b)
    {
        yield return Delay(.22f);
        if (IsResolved) yield break;
        if (values[a] != values[b]) { Finish(false); yield break; }
        buttons[a].interactable = buttons[b].interactable = false;
        buttons[a].image.color = buttons[b].image.color = ui.Blue;
        busy = false; matched++;
        if (matched == 3) Finish(true);
    }

    void BuildStroop()
    {
        string[] names = { "紅色", "藍色", "綠色", "黃色" };
        Color[] colors = { new Color(.8f,.16f,.16f), new Color(.15f,.35f,.8f), new Color(.1f,.55f,.25f), new Color(.85f,.64f,.04f) };
        int word = UnityEngine.Random.Range(0,4);
        int block = UnityEngine.Random.value < .5f ? word : (word + UnityEngine.Random.Range(1,4)) % 4;
        int ink = (word + UnityEngine.Random.Range(1,4)) % 4;
        var label = ui.Text(area, "顏色文字", names[word], 60, .08f, .43f, .45f, 1);
        label.color = colors[ink];
        ui.Panel(area, "目標色塊", colors[block], .57f, .48f, .86f, .91f);
        Choice("相同", "相同", .17f, .02f, .46f, .36f, () => Finish(word == block));
        Choice("不同", "不同", .54f, .02f, .83f, .36f, () => Finish(word != block));
        Ready("忽略字的墨色：文字的意思與右邊色塊相同嗎？");
    }

    void BuildOrder()
    {
        values.AddRange(new[] {1,2,3,4,5}); Shuffle(values); expected = 1;
        for (int i = 0; i < values.Count; i++)
        {
            int index = i; float x = .055f + i * .18f;
            Choice("數字_" + i, values[i].ToString(), x, .14f, x + .165f, .85f, () =>
            {
                if (values[index] != expected) { Finish(false); return; }
                buttons[index].interactable = false; buttons[index].image.color = ui.Blue;
                expected++; if (expected == 6) Finish(true);
            });
        }
        Ready("依照 1 → 5 的順序點選；點錯即進入下一關");
    }

    void BuildSum()
    {
        var pool = Enumerable.Range(1,9).ToList(); Shuffle(pool); values.AddRange(pool.Take(4));
        target = values[0] + values[1]; Shuffle(values);
        var sumText = ui.Text(area, "加總目標", "目標 " + target + "　目前 0", 38, .1f, .68f, .9f, 1f);
        for (int i = 0; i < values.Count; i++)
        {
            int index = i; float x = .14f + i * .185f;
            Choice("加總數字_" + i, values[i].ToString(), x, .05f, x + .16f, .59f, () =>
            {
                sum += values[index]; buttons[index].interactable = false; buttons[index].image.color = ui.Blue;
                sumText.text = "目標 " + target + "　目前 " + sum;
                if (sum == target) Finish(true);
                else if (sum > target || buttons.All(b => !b.interactable)) Finish(false);
            });
        }
        Ready("選擇數字，加起來等於目標；超過目標即失敗");
    }

    void BuildPipes()
    {
        // Twelve boundary ports; corners can open onto either adjoining edge.
        int[] cells={0,1,2,2,5,8,8,7,6,6,3,0};
        int[] directions={0,0,0,1,1,1,2,2,2,3,3,3};
        int start=UnityEngine.Random.Range(0,12);
        var ends=Enumerable.Range(0,12).Where(i=>
            Mathf.Abs(cells[i]%3-cells[start]%3)+Mathf.Abs(cells[i]/3-cells[start]/3)>=2).ToList();
        int end=ends[UnityEngine.Random.Range(0,ends.Count)];
        pipeStart=cells[start];pipeEnd=cells[end];
        pipeStartDirection=directions[start];pipeEndDirection=directions[end];
        pipeBase=new int[9];rotations=new int[9];
        pipeBase[pipeStart]|=1<<pipeStartDirection;
        int current=pipeStart;
        while(current!=pipeEnd)
        {
            int x=current%3,y=current/3,tx=pipeEnd%3,ty=pipeEnd/3;
            bool horizontal=x!=tx&&(y==ty||UnityEngine.Random.value<.5f);
            int direction=horizontal?(tx>x?1:3):(ty>y?2:0);
            int next=current+(direction==1?1:direction==3?-1:direction==2?3:-3);
            pipeBase[current]|=1<<direction;
            pipeBase[next]|=1<<((direction+2)%4);
            current=next;
        }
        pipeBase[pipeEnd]|=1<<pipeEndDirection;
        DrawEndpoint("入口",pipeStart,pipeStartDirection,true);
        DrawEndpoint("出口",pipeEnd,pipeEndDirection,false);
        var board=ui.Panel(area,"水管棋盤",Color.clear,.27f,.12f,.73f,.88f,false);
        for(int i=0;i<9;i++)
        {
            int index=i,col=i%3,row=i/3;
            rotations[i]=pipeBase[i]==0?0:UnityEngine.Random.Range(0,4);
            Color color=i==pipeStart?ui.Sage:i==pipeEnd?ui.Rose:ui.Blue;
            var b=ui.Button(board,"水管_"+i,"",color,col/3f+.008f,(2-row)/3f+.015f,
                (col+1)/3f-.008f,(3-row)/3f-.015f,()=>
                {
                    if(!CanInput)return;
                    rotations[index]=(rotations[index]+1)%4;DrawPipe(index);
                    if(PipeConnected())Finish(true);
                });
            buttons.Add(b);DrawPipe(i);
        }
        while(PipeConnected())
        { rotations[pipeStart]=(rotations[pipeStart]+1)%4;DrawPipe(pipeStart); }
        Ready("旋轉水管，連通綠色入口與粉紅色出口");
    }
    void DrawEndpoint(string name,int cell,int direction,bool entrance)
    {
        float centerX=.27f+(.46f/3)*(cell%3+.5f);
        float centerY=.12f+(.76f/3)*(2-cell/3+.5f);
        string[] arrows=entrance?new[]{"↓","←","↑","→"}:new[]{"↑","→","↓","←"};
        string text=(direction==1||direction==3)?(direction==3?"← "+name:name+" →"):name+" "+arrows[direction];
        if(entrance&&direction==1)text="← 入口";
        if(entrance&&direction==3)text="入口 →";
        TMP_Text label;
        if(direction==0)label=ui.Text(area,name,text,24,centerX-.09f,.88f,centerX+.09f,1f);
        else if(direction==2)label=ui.Text(area,name,text,24,centerX-.09f,0,centerX+.09f,.12f);
        else label=ui.Text(area,name,text,26,direction==3?.02f:.74f,centerY-.11f,direction==3?.26f:.98f,centerY+.11f);
        label.color=entrance?new Color(.22f,.43f,.14f):new Color(.65f,.24f,.27f);
        label.fontStyle=FontStyles.Bold;
    }
    int Ports(int i)
    {
        int p = pipeBase[i];
        for (int j = 0; j < rotations[i]; j++) p = ((p << 1) & 15) | (p >> 3);
        return p;
    }
    void DrawPipe(int i)
    {
        var parent = buttons[i].transform;
        for (int j = parent.childCount - 1; j >= 0; j--) { var child = parent.GetChild(j); child.gameObject.SetActive(false); Destroy(child.gameObject); }
        int p = Ports(i);
        if (p == 0) return;
        ui.Panel(parent, "接點", ui.Cream, .40f,.37f,.60f,.63f,false).GetComponent<Image>().raycastTarget=false;
        float[,] ends = {{.40f,.5f,.60f,1f},{.5f,.37f,1f,.63f},{.40f,0,.60f,.5f},{0,.37f,.5f,.63f}};
        for (int d=0;d<4;d++) if ((p & (1<<d)) != 0)
            ui.Panel(parent,"管線_"+d,ui.Cream,ends[d,0],ends[d,1],ends[d,2],ends[d,3],false).GetComponent<Image>().raycastTarget=false;
    }
    bool PipeConnected()
    {
        if ((Ports(pipeStart)&(1<<pipeStartDirection))==0 || (Ports(pipeEnd)&(1<<pipeEndDirection))==0) return false;
        var visited = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(pipeStart);
        int[] dx={0,1,0,-1}, dy={-1,0,1,0};
        while(queue.Count>0)
        {
            int cell=queue.Dequeue(); if(!visited.Add(cell))continue; if(cell==pipeEnd)return true;
            for(int d=0;d<4;d++)
            {
                int x=cell%3+dx[d], y=cell/3+dy[d];
                if(x<0||x>2||y<0||y>2||(Ports(cell)&(1<<d))==0)continue;
                int next=y*3+x;
                if((Ports(next)&(1<<((d+2)%4)))!=0)queue.Enqueue(next);
            }
        }
        return false;
    }

    void BuildShopping()
    {
        instruction.text = "先記住這 3 件商品，清單收起後開始倒數";
        var products = ui.Assets.products.OrderBy(_=>UnityEngine.Random.value).Take(6).ToArray();
        values.AddRange(Enumerable.Range(0,6)); Shuffle(values); shoppingTargets.AddRange(values.Take(3));
        var memory = ui.Panel(area,"購物清單",ui.Cream,.22f,.02f,.78f,.98f);
        for(int i=0;i<3;i++)ui.Sprite(memory,"目標_"+i,products[shoppingTargets[i]],i/3f+.03f,.08f,(i+1)/3f-.03f,.92f);
        for(int i=0;i<6;i++)
        {
            int index=i; float x=.035f+i*.157f;
            var b=Choice("商品_"+i,"",x,.03f,x+.145f,.95f,()=>
            {
                if(!shoppingTargets.Contains(index)){Finish(false);return;}
                shoppingChosen.Add(index);buttons[index].interactable=false;buttons[index].image.color=ui.Blue;
                if(shoppingChosen.Count==3)Finish(true);
            });
            ui.Sprite(b.transform,"商品圖片",products[i],.09f,.12f,.91f,.88f);
            b.gameObject.SetActive(false);
        }
        StartCoroutine(ShoppingPreview(memory.gameObject));
    }
    IEnumerator ShoppingPreview(GameObject memory)
    {
        yield return Delay(2f); memory.SetActive(false);
        foreach(var b in buttons)b.gameObject.SetActive(true);
        Ready("選出剛才清單中的 3 件商品；選錯即失敗");
    }

    void BuildQuiz()
    {
        string[] questions = { "一星期有七天。", "一年有十三個月。", "紅綠燈的紅燈代表停止。", "太陽通常從西方升起。", "十五比十二大。", "一小時有一百分鐘。", "下雨時可以用雨傘遮雨。", "三角形有四條邊。", "冬天通常比夏天冷。", "七加八等於十五。" };
        bool[] answers = { true,false,true,false,true,false,true,false,true,true };
        int q=UnityEngine.Random.Range(0,questions.Length);
        ui.Text(area,"生活敘述",questions[q],42,.06f,.45f,.94f,.98f);
        Choice("正確","正確",.17f,.02f,.46f,.36f,()=>Finish(answers[q]));
        Choice("錯誤","錯誤",.54f,.02f,.83f,.36f,()=>Finish(!answers[q]));
        Ready("閱讀敘述，判斷正確或錯誤");
    }

    void BuildGopher()
    {
        for(int i=0;i<6;i++)
        {
            int index=i,col=i%3,row=i/3;
            var b=Choice("地洞_"+i,"",.18f+col*.22f,.04f+(1-row)*.48f,.38f+col*.22f,.48f+(1-row)*.48f,()=>
            {
                if(index!=moleIndex){Finish(false);return;}
                hits++; if(hits==3){Finish(true);return;}
                MoveMole(); instruction.text="點中地鼠 "+hits+" / 3";
            });
            images.Add(ui.Sprite(b.transform,"地鼠",ui.Assets.mole,.14f,.02f,.86f,.98f));
        }
        moleIndex=-1;MoveMole();Ready("點中地鼠 0 / 3；點到空洞即失敗");
    }
    void MoveMole()
    {
        int next=UnityEngine.Random.Range(0,5);if(next>=moleIndex&&moleIndex>=0)next++;
        moleIndex=next;moleTime=0;
        for(int i=0;i<images.Count;i++)images[i].enabled=i==moleIndex;
    }
    void Update()
    {
        if(!CanInput||Kind!=LTCChallengeKind.Gopher)return;
        moleTime+=Time.deltaTime;if(moleTime>=1.1f)MoveMole();
    }
}
