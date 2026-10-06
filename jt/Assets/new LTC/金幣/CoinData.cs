using System;
using UnityEngine;

public static class CoinData
{
    private const string CoinKey = "TotalCoins";

    // Temporary test switch requested for blind-box testing.
    // Set this to false to restore the saved wallet.
    public const bool FixedTestBalanceEnabled = true;
    public const int FixedTestBalance = 100;

    public static event Action<int> BalanceChanged;

    public static int TotalCoins
    {
        get
        {
            return FixedTestBalanceEnabled
                ? FixedTestBalance
                : PlayerPrefs.GetInt(CoinKey, 0);
        }
    }

    public static void AddCoins(int amount)
    {
        if (FixedTestBalanceEnabled)
        {
            BalanceChanged?.Invoke(FixedTestBalance);
            return;
        }
        SetCoins(Mathf.Max(0, TotalCoins + amount));
    }

    public static void SetCoins(int amount)
    {
        if (FixedTestBalanceEnabled)
        {
            BalanceChanged?.Invoke(FixedTestBalance);
            return;
        }

        int safeAmount = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(CoinKey, safeAmount);
        PlayerPrefs.Save();
        BalanceChanged?.Invoke(safeAmount);
    }
}

