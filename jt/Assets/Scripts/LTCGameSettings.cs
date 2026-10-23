using UnityEngine;

/// <summary>Persistent tutorial preference shared by all games.</summary>
public static class LTCGameSettings
{
    public const string TutorialsEnabledKey = "LTC_GameTutorialsEnabled";
    public static bool TutorialsEnabled
    {
        get => PlayerPrefs.GetInt(TutorialsEnabledKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(TutorialsEnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
