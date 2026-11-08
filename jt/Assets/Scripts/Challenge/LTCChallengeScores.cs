using UnityEngine;
using LTC.Identity;

/// <summary>Stores the personal best for the local challenge display.</summary>
public static class LTCChallengeScores
{
    public static string CurrentPlayerKey => PlayerIdentityService.Current.StableLocalPlayerKey;
    public static string PreferenceKey(string playerKey) => "LTC_ChallengeBest_v1_" + playerKey;
    public static int Best(string playerKey) => Mathf.Max(0, PlayerPrefs.GetInt(PreferenceKey(playerKey), 0));
    public static bool Record(int score, string playerKey)
    {
        if (score <= Best(playerKey)) return false;
        PlayerPrefs.SetInt(PreferenceKey(playerKey), score);
        PlayerPrefs.Save();
        return true;
    }
}
