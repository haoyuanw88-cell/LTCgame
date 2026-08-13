using UnityEngine;

public static class CoinData
{
    private const string CoinKey = "TotalCoins";

    public static int TotalCoins
    {
        get
        {
            return PlayerPrefs.GetInt(CoinKey, 0);
        }
    }

    public static void AddCoins(int amount)
    {
        int newTotal = TotalCoins + amount;
        PlayerPrefs.SetInt(CoinKey, newTotal);
        PlayerPrefs.Save();
    }

    public static void SetCoins(int amount)
    {
        PlayerPrefs.SetInt(CoinKey, amount);
        PlayerPrefs.Save();
    }
}
