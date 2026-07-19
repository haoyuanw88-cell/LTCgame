using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FoodShopDisplay : MonoBehaviour
{
    [System.Serializable]
    public class FoodItem
    {
        public string itemId;
        public string itemName;

        [TextArea(2, 4)]
        public string description;

        public int price;
        public Sprite icon;
    }

    [Header("商品列表")]
    public Transform contentParent;
    public FoodItemCard foodItemCardPrefab;
    public List<FoodItem> foodItems = new List<FoodItem>();

    [Header("商品詳細面板")]
    public GameObject itemPanel;
    public Image itemImage;
    public TMP_Text itemNameText;
    public TMP_Text itemDescriptionText;
    public Button closeButton;
    public Button buyButton;

    [Header("金幣顯示")]
    public CoinDisplay coinDisplay;

    private FoodItem currentItem;

    void Start()
    {
        if (itemPanel != null)
        {
            itemPanel.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseItemPanel);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyCurrentItem);
        }

        ShowFoodItems();
    }

    public void ShowFoodItems()
    {
        ClearContent();

        for (int i = 0; i < foodItems.Count; i++)
        {
            FoodItemCard card = Instantiate(foodItemCardPrefab, contentParent);
            card.Setup(foodItems[i], this);
        }
    }

    public void OpenItemPanel(FoodItem item)
    {
        currentItem = item;

        if (itemImage != null)
        {
            itemImage.sprite = item.icon;
            itemImage.enabled = item.icon != null;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item.itemName;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = item.description;
        }

        if (itemPanel != null)
        {
            itemPanel.SetActive(true);
        }
    }

    public void CloseItemPanel()
    {
        currentItem = null;

        if (itemPanel != null)
        {
            itemPanel.SetActive(false);
        }
    }

    public void BuyCurrentItem()
    {
        if (currentItem == null) return;

        if (CoinData.TotalCoins < currentItem.price)
        {
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = "金幣不足，無法購買。";
            }

            return;
        }

        CoinData.AddCoins(-currentItem.price);
        InventoryData.AddItemCount(currentItem.itemId, 1);

        if (coinDisplay != null)
        {
            coinDisplay.Refresh();
        }

        CloseItemPanel();
    }

    void ClearContent()
    {
        if (contentParent == null) return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }
}