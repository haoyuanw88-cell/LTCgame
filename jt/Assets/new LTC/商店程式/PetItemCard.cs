using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PetItemCard : MonoBehaviour
{
    public Button itemButton;
    public Image itemImage;
    public TMP_Text priceText;

    private PetItemShopDisplay.PetItem item;
    private PetItemShopDisplay shopDisplay;

    public void Setup(PetItemShopDisplay.PetItem newItem, PetItemShopDisplay display)
    {
        item = newItem;
        shopDisplay = display;

        if (itemImage != null)
        {
            itemImage.sprite = item.icon;
        }

        if (priceText != null)
        {
            priceText.text = item.price.ToString();
        }

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(OpenDetailPanel);
        }
    }

    void OpenDetailPanel()
    {
        if (shopDisplay != null)
        {
            shopDisplay.OpenItemPanel(item);
        }
    }
}