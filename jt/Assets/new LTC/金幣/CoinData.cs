using System;
using UnityEngine;

public static class CoinData
{
    private const string CoinKey = "TotalCoins";
    public static event Action<int> BalanceChanged;

    public static int TotalCoins
    {
        get
        {
            return PlayerPrefs.GetInt(CoinKey, 0);
        }
    }

    public static void AddCoins(int amount)
    {
        SetCoins(Mathf.Max(0, TotalCoins + amount));
    }

    public static void SetCoins(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(CoinKey, safeAmount);
        PlayerPrefs.Save();
        BalanceChanged?.Invoke(safeAmount);
    }
}
