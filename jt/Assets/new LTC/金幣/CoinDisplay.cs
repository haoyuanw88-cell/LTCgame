using TMPro;
using UnityEngine;

public class CoinDisplay : MonoBehaviour
{
    public TMP_Text coinText;

    void Start()
    {
        Refresh();
    }

    void OnEnable()
    {
        CoinData.BalanceChanged += OnBalanceChanged;
        Refresh();
    }

    void OnDisable()
    {
        CoinData.BalanceChanged -= OnBalanceChanged;
    }

    void OnBalanceChanged(int balance)
    {
        if (coinText != null) coinText.text = balance.ToString();
    }

    public void Refresh()
    {
        if (coinText != null)
        {
            coinText.text = CoinData.TotalCoins.ToString();
        }
    }
}

