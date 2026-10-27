using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LTC.Tests
{
    public class ChallengeRefinementsPlayModeTests
    {
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static object F(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
        [UnityTest]
        public IEnumerator BestScoreSurvivesReloadAndNeverDecreases()
        {
            var scores=T("LTCChallengeScores");
            string player=(string)scores.GetProperty("CurrentPlayerKey").GetValue(null);
            string key=(string)scores.GetMethod("PreferenceKey").Invoke(null,new object[]{player});
            bool existed=PlayerPrefs.HasKey(key);int previous=PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                scores.GetMethod("Record").Invoke(null,new object[]{4321,player});
                scores.GetMethod("Record").Invoke(null,new object[]{100,player});
                Assert.AreEqual(4321,scores.GetMethod("Best").Invoke(null,new object[]{player}));
                Assert.AreEqual(0,scores.GetMethod("Best").Invoke(null,new object[]{Guid.NewGuid().ToString()}));
                SceneManager.LoadScene("GameScene");yield return null;yield return null;
                var entry=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.GetType().Name=="LTCChallengeEntry");
                var label=entry.transform.Find("歷史最高分").GetComponent(T("TMPro.TextMeshProUGUI"));
                Assert.IsTrue(((string)label.GetType().GetProperty("text").GetValue(label)).Contains("4,321"));
            }
            finally{if(existed)PlayerPrefs.SetInt(key,previous);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
        [UnityTest]
        public IEnumerator SixtyFourPipeBoardsVaryAndAreSolvable()
        {
            SceneManager.LoadScene("ChallengeRush");yield return null;yield return null;
            var controller=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="LTCChallengeController");
            var toolkit=Activator.CreateInstance(T("LTCChallengeUI"),F(controller,"assets"));
            var starts=new HashSet<int>();var ends=new HashSet<int>();var pairs=new HashSet<string>();
            var saved=UnityEngine.Random.state;
            try
            {
                for(int sample=0;sample<64;sample++)
                {
                    UnityEngine.Random.InitState(2100+sample);
                    var go=new GameObject("PipeSample",typeof(RectTransform));
                    var view=go.AddComponent(T("LTCChallengeRoundView"));bool solved=false;
                    T("LTCChallengeRoundView").GetMethod("Initialize").Invoke(view,new object[]{toolkit,Enum.ToObject(T("LTCChallengeKind"),4),(Action<bool>)(ok=>solved=ok)});
                    starts.Add((int)F(view,"pipeStartDirection"));ends.Add((int)F(view,"pipeEndDirection"));
                    pairs.Add(F(view,"pipeStart")+":"+F(view,"pipeStartDirection")+"-"+F(view,"pipeEnd")+":"+F(view,"pipeEndDirection"));
                    var check=T("LTCChallengeRoundView").GetMethod("PipeConnected",BindingFlags.Instance|BindingFlags.NonPublic);
                    Assert.IsFalse((bool)check.Invoke(view,null));
                    var rotation=(int[])F(view,"rotations");Array.Clear(rotation,0,rotation.Length);
                    Assert.IsTrue((bool)check.Invoke(view,null),"Sample "+sample);
                    UnityEngine.Object.Destroy(go);yield return null;
                }
                Assert.AreEqual(4,starts.Count);Assert.AreEqual(4,ends.Count);Assert.Greater(pairs.Count,20);
            }
            finally{UnityEngine.Random.state=saved;}
        }
    }
}
