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
        Refresh();
    }

    public void Refresh()
    {
        if (coinText != null)
        {
            coinText.text = CoinData.TotalCoins.ToString();
        }
    }
}

