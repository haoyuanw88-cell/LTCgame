using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FoodItemCard : MonoBehaviour
{
    public Button itemButton;
    public Image itemImage;
    public TMP_Text priceText;

    private FoodShopDisplay.FoodItem item;
    private FoodShopDisplay shopDisplay;

    public void Setup(FoodShopDisplay.FoodItem newItem, FoodShopDisplay display)
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