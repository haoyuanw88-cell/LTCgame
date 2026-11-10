using UnityEngine;

/// <summary>Persistent preferences shared by all games.</summary>
public static class LTCGameSettings
{
    public const string TutorialsEnabledKey = "LTC_GameTutorialsEnabled";
    public const string UnlockAllLevelsKey = "LTC_UnlockAllLevels";
    public const float DefaultMusicVolume = .18f;
    public const float DefaultEffectsVolume = .45f;

    public static void RestoreDefaults()
    {
        var audio = LTC.Audio.LTCGameAudio.EnsureInstance();
        audio.SetMusicVolume(DefaultMusicVolume);
        audio.SetEffectsVolume(DefaultEffectsVolume);
        TutorialsEnabled = true;
        UnlockAllLevels = false;
    }
    public static bool TutorialsEnabled
    {
        get => PlayerPrefs.GetInt(TutorialsEnabledKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(TutorialsEnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool UnlockAllLevels
    {
        get => PlayerPrefs.GetInt(UnlockAllLevelsKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(UnlockAllLevelsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
