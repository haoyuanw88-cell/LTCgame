using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LTC.Tests
{
    public sealed class ChallengeRushPlayModeTests
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        string savedScoreKey;
        int savedBest;
        bool hadBest;
        [SetUp]
        public void PreservePersonalBest()
        {
            var scores=T("LTCChallengeScores");
            string player=(string)scores.GetProperty("CurrentPlayerKey").GetValue(null);
            savedScoreKey=(string)scores.GetMethod("PreferenceKey").Invoke(null,new object[]{player});
            hadBest=PlayerPrefs.HasKey(savedScoreKey);savedBest=PlayerPrefs.GetInt(savedScoreKey);
        }
        [TearDown]
        public void RestorePersonalBest()
        {
            if(hadBest)PlayerPrefs.SetInt(savedScoreKey,savedBest);else PlayerPrefs.DeleteKey(savedScoreKey);
            PlayerPrefs.Save();
        }
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static object F(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
        static object P(object o,string name)=>o.GetType().GetProperty(name,Flags).GetValue(o);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static void Set(object o,string name,object value)=>o.GetType().GetProperty(name,Flags).SetValue(o,value);
        static MonoBehaviour Controller()=>UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="LTCChallengeController");
        static object Kind(int index)=>Enum.ToObject(T("LTCChallengeKind"),index);
        static void Click(object round,int index)=>((Button)((IList)F(round,"buttons"))[index]).onClick.Invoke();
        static int ActiveRecords()=>((IDictionary)T("LTCCognitiveAssessment.CognitiveAssessmentService")
            .GetField("ActiveSessions",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Count;

        [Test]
        public void ScoringUsesCurrentMultiplierAndSingleKillBonus()
        {
            var run=Activator.CreateInstance(T("LTCChallengeRun"),42);
            Set(run,"EnemyHealth",2);
            Call(run,"Resolve",true);
            Assert.AreEqual(50,P(run,"Score"));Assert.AreEqual(1.5f,P(run,"Multiplier"));
            Call(run,"Resolve",true);
            Assert.AreEqual(275,P(run,"Score"),"Final hit: (50 + 100) x 1.5 = 225.");
            Assert.AreEqual(1,P(run,"Defeated"));Assert.AreEqual(2f,P(run,"Multiplier"));
            Call(run,"SpawnEnemy");Call(run,"Resolve",false);
            Assert.AreEqual(2,P(run,"Health"));Assert.AreEqual(1f,P(run,"Multiplier"));
            for(int i=0;i<30;i++){if((bool)P(run,"NeedsEnemy"))Call(run,"SpawnEnemy");Call(run,"Resolve",true);}
            Assert.AreEqual(5f,P(run,"Multiplier"));
            if((bool)P(run,"NeedsEnemy"))Call(run,"SpawnEnemy");
            Call(run,"Resolve",false);Call(run,"Resolve",false);
            Assert.IsTrue((bool)P(run,"IsOver"));
            int score=(int)P(run,"Score");Assert.IsFalse((bool)Call(run,"Resolve",true));
            Assert.AreEqual(score,P(run,"Score"));
        }

        [Test]
        public void EnemyHealthRandomizesAcrossInclusiveTwoToEight()
        {
            var values=new HashSet<int>();
            for(int seed=0;seed<200;seed++)
            {
                var run=Activator.CreateInstance(T("LTCChallengeRun"),seed);
                int hp=(int)P(run,"EnemyHealth");Assert.That(hp,Is.InRange(2,8));values.Add(hp);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(2,7),values);
        }

        [UnityTest]
        public IEnumerator CatalogEntryIsLastAndLoadsChallengeWithRequestedMusic()
        {
            SceneManager.LoadScene("GameScene");yield return null;yield return null;
            var catalog=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="CognitiveGameCatalogController");
            var content=catalog.transform.Find("Cognitive Catalog Canvas/遊戲首頁/能力分類滑動區/Viewport/Content");
            Assert.AreEqual("挑戰模式",content.GetChild(content.childCount-1).name);
            content.Find("挑戰模式/開始挑戰").GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
            var c=Controller();
            Assert.AreEqual("ChallengeRush",SceneManager.GetActiveScene().name);
            var audio=c.GetComponent<AudioSource>();Assert.IsTrue(audio.isPlaying);Assert.IsTrue(audio.loop);
            Assert.AreEqual("Sketchbook 2025-11-13",audio.clip.name);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(b=>b!=null&&(b.GetType().Name=="GameController"||b.GetType().Name=="CardSkillPresentation")));
        }

        [UnityTest]
        public IEnumerator EveryMiniGameCanBeSolvedThroughItsButtonsWithoutAssessmentRecords()
        {
            SceneManager.LoadScene("ChallengeRush");yield return null;yield return null;
            var c=Controller();int sessions=ActiveRecords();
            var assets=F(c,"assets");
            var toolkit=Activator.CreateInstance(T("LTCChallengeUI"),assets);
            for(int kind=0;kind<8;kind++)
            {
                var go=new GameObject("TestRound",typeof(RectTransform));
                var view=go.AddComponent(T("LTCChallengeRoundView"));
                bool? result=null;Action<bool> callback=b=>result=b;
                Call(view,"Initialize",toolkit,Kind(kind),callback);
                float wait=0;
                while(!(bool)P(view,"IsReady")&&wait<4){yield return null;wait+=Time.deltaTime;}
                Assert.IsTrue((bool)P(view,"IsReady"),"Preview must finish for kind "+kind);
                var values=F(view,"values") as IList;
                if(kind==0)
                {
                    for(int pair=0;pair<3;pair++)
                    {
                        for(int i=0;i<values.Count;i++)if((int)values[i]==pair)Click(view,i);
                        yield return new WaitForSeconds(.26f);
                    }
                }
                if(kind==1)
                {
                    // Buttons carry the generated truth comparison. A correct result is the only accepted outcome.
                    // Read the word/swatch via their colors and label text using reflection to avoid TMP assembly coupling.
                    var word=go.transform.Find("操作區/顏色文字").GetComponent(T("TMPro.TextMeshProUGUI"));
                    string text=(string)word.GetType().GetProperty("text").GetValue(word);
                    var swatch=go.transform.Find("操作區/目標色塊").GetComponent<Image>().color;
                    string expected=swatch.r>.7f&&swatch.g<.3f?"紅色":swatch.b>.7f?"藍色":swatch.g>.5f&&swatch.r<.3f?"綠色":"黃色";
                    Click(view,text==expected?0:1);
                }
                if(kind==2)for(int number=1;number<=5;number++)for(int i=0;i<values.Count;i++)if((int)values[i]==number)Click(view,i);
                if(kind==3)
                {
                    int target=(int)F(view,"target");bool solved=false;
                    for(int a=0;a<values.Count&&!solved;a++)for(int b=a+1;b<values.Count&&!solved;b++)
                        if((int)values[a]+(int)values[b]==target){Click(view,a);Click(view,b);solved=true;}
                }
                if(kind==4)
                {
                    var rotations=(int[])F(view,"rotations");
                    for(int i=0;i<9;i++)while(rotations[i]!=0&&result==null)Click(view,i);
                }
                if(kind==5)foreach(int i in (IList)F(view,"shoppingTargets"))Click(view,i);
                if(kind==6)
                {
                    var textComp=go.transform.Find("操作區/生活敘述").GetComponent(T("TMPro.TextMeshProUGUI"));
                    string question=(string)textComp.GetType().GetProperty("text").GetValue(textComp);
                    string[] falseQuestions={"一年有十三個月。","太陽通常從西方升起。","一小時有一百分鐘。","三角形有四條邊。"};
                    Click(view,falseQuestions.Contains(question)?1:0);
                }
                if(kind==7)for(int i=0;i<3;i++)Click(view,(int)F(view,"moleIndex"));
                yield return null;
                Assert.AreEqual(true,result,"Kind "+kind+" should be solvable.");
                Assert.AreEqual(sessions,ActiveRecords(),"Mini-games must not begin assessment sessions.");
                UnityEngine.Object.Destroy(go);yield return null;
            }
        }

        static void ForceRound(MonoBehaviour c,int kind)
        {
            var old=(MonoBehaviour)P(c,"CurrentRound");old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);
            Call(c,"CreateRound",Kind(kind));
        }
        static IEnumerator UntilReady(object round)
        {
            float t=0;while(!(bool)P(round,"IsReady")&&t<6)
            {
                // Editor focus changes can pause the game during automated runs.
                var c=Controller();if((bool)P(c,"IsPaused"))Call(c,"SetPaused",false);
                yield return null;t+=Time.deltaTime;
            }
            Assert.IsTrue((bool)P(round,"IsReady"));
        }
        [UnityTest]
        public IEnumerator CardPreviewPauseTimeoutTransitionsAndDeathKeepScoresSessionOnly()
        {
            SceneManager.LoadScene("ChallengeRush");yield return null;yield return null;
            var c=Controller();int active=ActiveRecords();
            Call(c,"StartRun");ForceRound(c,0);
            var round=P(c,"CurrentRound");yield return new WaitForSeconds(.4f);
            Assert.IsFalse((bool)P(round,"IsReady"));
            Assert.AreEqual(10f,P(c,"Remaining"),"Card preview must not consume the ten-second countdown.");
            Call(c,"SetPaused",true);yield return new WaitForSeconds(.3f);
            Assert.AreEqual(10f,P(c,"Remaining"));
            Call(c,"SetPaused",false);yield return UntilReady(round);
            yield return new WaitForSeconds(.1f);Assert.Less((float)P(c,"Remaining"),10f);
            for(int failure=0;failure<3;failure++)
            {
                round=P(c,"CurrentRound");yield return UntilReady(round);
                // Expire twice to verify the round resolution latch prevents double damage.
                Call(round,"Expire");Call(round,"Expire");
                yield return new WaitForSeconds(1f);
                Assert.AreEqual(2-failure,P(P(c,"Run"),"Health"));
                if(failure<2)Assert.AreNotSame(round,P(c,"CurrentRound"));
            }
            Assert.IsTrue((bool)P(P(c,"Run"),"IsOver"));
            var resultPanel=c.transform.Find("Challenge Rush Canvas/挑戰畫面/挑戰結算");
            Assert.IsNotNull(resultPanel);
            var scoreText=resultPanel.Find("內容/本次分數").GetComponent(T("TMPro.TextMeshProUGUI"));
            Assert.GreaterOrEqual((float)scoreText.GetType().GetProperty("fontSize").GetValue(scoreText),42f);
            Assert.AreEqual(active,ActiveRecords());
            Call(c,"StartRun");
            Assert.AreEqual(0,P(P(c,"Run"),"Score"));Assert.AreEqual(3,P(P(c,"Run"),"Health"));
        }

        [UnityTest]
        public IEnumerator SuccessfulRoundsAttackKillAndSlideIntoFreshEnemy()
        {
            SceneManager.LoadScene("ChallengeRush");yield return null;yield return null;
            var c=Controller();Call(c,"StartRun");
            var run=P(c,"Run");Set(run,"EnemyHealth",2);
            var enemyImage=c.transform.Find("Challenge Rush Canvas/挑戰畫面/戰鬥區/敵人").GetComponent<Image>();
            var firstEnemy=enemyImage.sprite;
            for(int clear=0;clear<2;clear++)
            {
                ForceRound(c,2);var round=P(c,"CurrentRound");
                var values=(IList)F(round,"values");
                for(int number=1;number<=5;number++)for(int i=0;i<values.Count;i++)if((int)values[i]==number)Click(round,i);
                Assert.IsTrue((bool)P(c,"IsTransitioning"));
                yield return new WaitForSeconds(.9f);
                Assert.IsFalse((bool)P(c,"IsTransitioning"));
                Assert.AreNotSame(round,P(c,"CurrentRound"));
            }
            Assert.AreEqual(275,P(run,"Score"));Assert.AreEqual(1,P(run,"Defeated"));
            Assert.GreaterOrEqual(PlayerPrefs.GetInt(savedScoreKey),275);
            Assert.That((int)P(run,"EnemyHealth"),Is.InRange(2,8));
            Assert.AreNotSame(firstEnemy,enemyImage.sprite);
            Assert.AreEqual(2f,P(run,"Multiplier"));
            ForceRound(c,2);
            var wrong=P(c,"CurrentRound");var nums=(IList)F(wrong,"values");
            int bad=0;while((int)nums[bad]==1)bad++;
            Click(wrong,bad);yield return new WaitForSeconds(.9f);
            Assert.AreEqual(2,P(run,"Health"));Assert.AreEqual(1f,P(run,"Multiplier"));
        }
    }
}
