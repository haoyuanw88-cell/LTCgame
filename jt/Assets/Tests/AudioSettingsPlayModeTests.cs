using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LTC.Tests
{
    public sealed class AudioSettingsPlayModeTests
    {
        readonly string[] keys={"LTC.MusicVolume","LTC.EffectsVolume"};
        readonly float[] saved=new float[2];
        readonly bool[] existed=new bool[2];
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static object Audio()=>T("LTC.Audio.LTCGameAudio").GetMethod("EnsureInstance").Invoke(null,null);
        static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method).Invoke(obj,args);
        static Transform Root()=>UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .First(b=>b.GetType().Name=="CognitiveGameCatalogController").transform.Find("Cognitive Catalog Canvas");
        static string Label(Transform target)
        {
            var label=target.GetComponent(T("TMPro.TextMeshProUGUI"));
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }
        [SetUp]
        public void PreserveVolumes()
        {
            for(int i=0;i<2;i++){existed[i]=PlayerPrefs.HasKey(keys[i]);saved[i]=PlayerPrefs.GetFloat(keys[i],i==0?.18f:.45f);}
        }
        [TearDown]
        public void RestoreVolumes()
        {
            var service=Audio();
            Call(service,"SetMusicVolume",saved[0]);Call(service,"SetEffectsVolume",saved[1]);
            for(int i=0;i<2;i++)if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
        }
        [UnityTest]
        public IEnumerator SlidersRespondToPointerSaveIndependentlyAndSurviveReload()
        {
            SceneManager.LoadScene("GameScene");yield return null;yield return null;
            var root=Root();
            root.Find("首次引導").gameObject.SetActive(false);root.Find("每日登入彈窗").gameObject.SetActive(false);
            root.Find("底部導覽/我的").GetComponent<Button>().onClick.Invoke();
            root.Find("我的頁/個人資料內容/設定").GetComponent<Button>().onClick.Invoke();
            var page=root.Find("設定頁");var card=page.Find("設定內容");
            Assert.IsNull(card.Find("教學說明"));
            var music=card.Find("背景音樂/滑桿").GetComponent<Slider>();
            var effects=card.Find("音效/滑桿").GetComponent<Slider>();
            music.value=65;effects.value=25;
            Assert.That(PlayerPrefs.GetFloat(keys[0]),Is.EqualTo(.65f).Within(.001f));
            Assert.That(PlayerPrefs.GetFloat(keys[1]),Is.EqualTo(.25f).Within(.001f));
            Assert.AreEqual("65%",Label(card.Find("背景音樂/百分比")));
            Assert.AreEqual("25%",Label(card.Find("音效/百分比")));
            Canvas.ForceUpdateCanvases();
            yield return null; // Newly enabled graphics join the raycast registry on the next frame.
            var rect=music.GetComponent<RectTransform>();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.8f,rect.rect.center.y)));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.IsNotEmpty(hits);Assert.AreEqual(music,hits[0].gameObject.GetComponentInParent<Slider>());
            ExecuteEvents.Execute(music.gameObject,data,ExecuteEvents.pointerDownHandler);
            Assert.That(music.value,Is.InRange(75f,85f),"Clicking the track must adjust the slider.");
            float selected=music.value;
            SceneManager.LoadScene("GameScene");yield return null;yield return null;
            card=Root().Find("設定頁/設定內容");
            Assert.AreEqual(selected,card.Find("背景音樂/滑桿").GetComponent<Slider>().value);
            Assert.AreEqual(25,card.Find("音效/滑桿").GetComponent<Slider>().value);
        }
        [UnityTest]
        public IEnumerator MusicAndEffectsChannelsControlSceneAudioAndMuteIndependently()
        {
            SceneManager.LoadScene("GameScene");yield return null;yield return null;
            var service=Audio();
            var host=new GameObject("AudioSettingsTest");
            try
            {
                var music=host.AddComponent<AudioSource>();music.loop=true;
                var effect=host.AddComponent<AudioSource>();effect.loop=false;
                Call(service,"SetMusicVolume",.3f);Call(service,"SetEffectsVolume",.8f);
                Assert.That(music.volume,Is.EqualTo(.3f).Within(.001));
                Assert.That(effect.volume,Is.EqualTo(.8f).Within(.001));
                Call(service,"SetMusicVolume",0f);
                Assert.AreEqual(0,music.volume);Assert.That(effect.volume,Is.EqualTo(.8f).Within(.001));
                Call(service,"SetEffectsVolume",0f);Assert.AreEqual(0,effect.volume);
            }
            finally{UnityEngine.Object.Destroy(host);}
            Call(service,"SetMusicVolume",.35f);Call(service,"SetEffectsVolume",.2f);
            SceneManager.LoadScene("ChallengeRush");yield return null;yield return new WaitForSecondsRealtime(.65f);
            var game=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="LTCChallengeController");
            Assert.That(game.GetComponent<AudioSource>().volume,Is.EqualTo(.35f).Within(.001));
            Call(service,"SetMusicVolume",0f);Assert.AreEqual(0,game.GetComponent<AudioSource>().volume);
        }
    }
}
