using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LTC.Audio
{
    /// <summary>One persistent audio service; clips are imported assets, never generated at runtime.</summary>
    public sealed class LTCGameAudio : MonoBehaviour
    {
        public static LTCGameAudio Instance { get; private set; }
        public float musicVolume = .18f;
        public float effectsVolume = .45f;
        AudioSource music;
        AudioSource effects;
        AudioClip tap;
        float nextScan;
        int lastTapFrame = -1;
        bool supported;
        bool otherMusicPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Instance) new GameObject("LTC Game Audio").AddComponent<LTCGameAudio>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            musicVolume = PlayerPrefs.GetFloat("LTC.MusicVolume", .18f);
            effectsVolume = PlayerPrefs.GetFloat("LTC.EffectsVolume", .45f);
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0;
            music.volume = 0;
            music.clip = Resources.Load<AudioClip>("LTCAudio/GardenLoop");
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0;
            effects.ignoreListenerPause = true;
            tap = Resources.Load<AudioClip>("LTCAudio/ButtonTap");
            SceneManager.sceneLoaded += SceneLoaded;
            ConfigureScene();
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) { ConfigureScene(); }
        void ConfigureScene()
        {
            string path = SceneManager.GetActiveScene().path;
            supported = path.StartsWith("Assets/new LTC/");
            nextScan = 0;
            if (supported && music.clip && !music.isPlaying) music.Play();
        }

        void Update()
        {
            float target = supported && !otherMusicPlaying ? musicVolume : 0;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * .25f);
            if (!supported && music.volume <= 0 && music.isPlaying) music.Stop();
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .5f;
            otherMusicPlaying = false;
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source != music && source != effects && source.loop && source.isPlaying && source.clip && !source.mute && source.volume > .001f)
                    otherMusicPlaying = true;
            if (!supported) return;
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!button.GetComponent<LTCButtonSound>()) button.gameObject.AddComponent<LTCButtonSound>();
        }

        public void PlayTap()
        {
            if (!supported || !tap || lastTapFrame == Time.frameCount) return;
            lastTapFrame = Time.frameCount;
            effects.PlayOneShot(tap, Mathf.Clamp01(effectsVolume));
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("LTC.MusicVolume", musicVolume);
            PlayerPrefs.Save();
        }
        public void SetEffectsVolume(float value)
        {
            effectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("LTC.EffectsVolume", effectsVolume);
            PlayerPrefs.Save();
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) Instance = null;
        }
    }
}
