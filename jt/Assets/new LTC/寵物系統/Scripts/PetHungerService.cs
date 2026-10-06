using System;
using System.Globalization;
using LTC.Identity;
using UnityEngine;

/// <summary>Three meat icons represent a 72-hour countdown, saved per local player and pet.</summary>
public static class PetHungerService
{
    public const double SecondsPerMeat = 24d * 60d * 60d;
    public const double FullSeconds = 3d * SecondsPerMeat;
    const string Prefix = "LTC_PetHunger_v1_";

    static string Key(string petId)
    {
        string player = PlayerIdentityService.Current.StableLocalPlayerKey;
        return Prefix + player + "_" + petId;
    }

    public static double RemainingSeconds(string petId) => RemainingSeconds(petId, DateTime.UtcNow);

    public static double RemainingSeconds(string petId, DateTime utcNow)
    {
        string key = Key(petId);
        if (!PlayerPrefs.HasKey(key + "_remaining"))
        {
            Save(petId, FullSeconds, utcNow);
            return FullSeconds;
        }

        if (!double.TryParse(PlayerPrefs.GetString(key + "_remaining"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double saved)) saved = FullSeconds;
        if (!long.TryParse(PlayerPrefs.GetString(key + "_utcTicks"), out long ticks) || ticks <= 0)
            return Math.Max(0d, Math.Min(FullSeconds, saved));
        return DecayedSeconds(saved, new DateTime(ticks, DateTimeKind.Utc), utcNow);
    }

    public static double DecayedSeconds(double savedSeconds, DateTime savedUtc, DateTime nowUtc)
    {
        double elapsed = Math.Max(0d, (nowUtc.Ticks - savedUtc.Ticks) / (double)TimeSpan.TicksPerSecond);
        return Math.Max(0d, Math.Min(FullSeconds, savedSeconds - elapsed));
    }

    public static double AfterFeeding(double remainingSeconds)
    {
        return Math.Min(FullSeconds, Math.Max(0d, remainingSeconds) + SecondsPerMeat);
    }

    public static int MeatCount(string petId) => CountMeat(RemainingSeconds(petId));

    public static int CountMeat(double remainingSeconds)
    {
        return Math.Max(0, Math.Min(3, (int)Math.Ceiling(Math.Max(0d, remainingSeconds) / SecondsPerMeat)));
    }

    public static bool TryFeed(string petId, out string message)
    {
        DateTime now = DateTime.UtcNow;
        double remaining = RemainingSeconds(petId, now);
        if (remaining >= FullSeconds - 1d)
        {
            message = "已經吃飽了，稍後再餵食";
            return false;
        }

        string[] ids = { "F_APPLE", "F_BANANA", "F_PINE" };
        string[] names = { "元氣蘋果", "開心香蕉", "陽光鳳梨" };
        for (int i = 0; i < ids.Length; i++)
        {
            if (!InventoryData.TryConsumeItemCount(ids[i], 1)) continue;
            Save(petId, AfterFeeding(remaining), now);
            message = "已餵食「" + names[i] + "」，補回一塊肉";
            return true;
        }

        message = "背包沒有點心，請先到商店購買";
        return false;
    }

    static void Save(string petId, double seconds, DateTime utcNow)
    {
        string key = Key(petId);
        PlayerPrefs.SetString(key + "_remaining", seconds.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(key + "_utcTicks", utcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
