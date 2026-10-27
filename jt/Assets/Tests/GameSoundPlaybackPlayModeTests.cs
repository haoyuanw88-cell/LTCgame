using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LTC.Tests
{
    public sealed class GameSoundPlaybackPlayModeTests
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        float savedVolume;bool hadVolume;
        MonoBehaviour manager;
        ScriptableObject level;
        static object F(object obj,string name)=>obj.GetType().GetField(name,Flags).GetValue(obj);
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        static MonoBehaviour Find(string type)=>UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b!=null&&b.GetType().Name==type);
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        [SetUp]
        public void Preserve()
        {
            hadVolume=PlayerPrefs.HasKey("LTC.EffectsVolume");
            savedVolume=PlayerPrefs.GetFloat("LTC.EffectsVolume",.45f);
            var audio=T("LTC.Audio.LTCGameAudio").GetMethod("EnsureInstance").Invoke(null,null);
            Call(audio,"SetEffectsVolume",.7f);
        }
        [TearDown]
        public void Restore()
        {
            if(manager!=null)Call(manager,"CancelCurrentAssessment");
            if(level!=null)UnityEngine.Object.Destroy(level);
            var audio=T("LTC.Audio.LTCGameAudio").GetMethod("EnsureInstance").Invoke(null,null);
            Call(audio,"SetEffectsVolume",savedVolume);
            if(!hadVolume)PlayerPrefs.DeleteKey("LTC.EffectsVolume");
            PlayerPrefs.Save();
        }
        static void AssertAudibleClip(AudioClip clip)
        {
            Assert.IsNotNull(clip,"The original sound asset must resolve to an AudioClip.");
            Assert.IsTrue(clip.LoadAudioData());
            var samples=new float[clip.samples*clip.channels];
            Assert.IsTrue(clip.GetData(samples,0));
            Assert.Greater(samples.Max(v=>Mathf.Abs(v)),.001f,"Sound data must not be silent.");
        }

        [UnityTest]
        public IEnumerator MatchingCardsPlayOriginalSuccessSoundAtSavedEffectsVolume()
        {
            SceneManager.LoadScene("CardsGame");yield return null;yield return null;
            manager=Find("GameController");
            var clip=(AudioClip)F(manager,"matchSuccessSound");AssertAudibleClip(clip);
            var selection=F(manager,"levelSelection");
            var levels=(IList)F(F(selection,"catalog"),"levels");
            level=UnityEngine.Object.Instantiate((ScriptableObject)levels[0]);
            Set(level,"cardCount",4);Set(level,"columns",2);Set(level,"previewSeconds",.5f);
            Set(level,"enemyHealth",1000);Set(level,"playerHealth",100);
            ((GameObject)F(selection,"panel")).SetActive(false);
            Call(manager,"LoadLevel",level);Call(manager,"CloseTeachingUI");
            yield return new WaitForSeconds(1.1f);
            var cards=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b.GetType().Name=="Card").ToArray();
            var pair=cards.GroupBy(b=>(int)Call(b,"GetCardID")).First(g=>g.Count()>=2).Take(2).ToArray();
            Call(pair[0],"FlipCard");Call(manager,"CardClicked",pair[0]);
            Call(pair[1],"FlipCard");Call(manager,"CardClicked",pair[1]);
            yield return new WaitForSeconds(.55f);
            var source=(AudioSource)F(manager,"audioSource");
            Assert.IsTrue(source.isPlaying,"A successful pair must start the success one-shot.");
            Assert.IsFalse(source.mute);Assert.AreEqual(0,source.spatialBlend);
            Assert.That(source.volume,Is.EqualTo(.7f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator GeneratedPipeLevelProvidesAndPlaysRotationSound()
        {
            SceneManager.LoadScene("PipeGame");yield return null;yield return null;
            manager=Find("PipeManager");
            AssertAudibleClip((AudioClip)F(manager,"defaultPipeRotateSound"));
            Call(F(manager,"levelSelection"),"PlaySelected");Call(manager,"CloseTutorial");yield return null;
            var pipe=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(b=>b.GetType().Name=="Blockin"&&!(bool)F(b,"isStartingPipe")&&!(bool)F(b,"isEndingPipe")&&!(bool)Call(b,"IsRotationLocked"));
            AssertAudibleClip((AudioClip)F(pipe,"rotateSound"));
            Call(pipe,"OnMouseDown");
            var source=(AudioSource)F(pipe,"myAudioSource");
            Assert.IsTrue(source.isPlaying,"Rotating a generated pipe must play its original sound.");
            Assert.IsFalse(source.mute);Assert.AreEqual(0,source.spatialBlend);
            var audio=T("LTC.Audio.LTCGameAudio").GetMethod("EnsureInstance").Invoke(null,null);
            Call(audio,"ApplySceneVolumes");
            Assert.That(source.volume,Is.EqualTo(.7f).Within(.001f));
        }
    }
}
