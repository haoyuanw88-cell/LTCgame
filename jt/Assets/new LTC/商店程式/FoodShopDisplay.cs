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
        AssignServerItemCodes();
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

        if (string.IsNullOrWhiteSpace(currentItem.itemId))
        {
            if (itemDescriptionText != null) itemDescriptionText.text = "商品尚未設定雲端代碼。";
            return;
        }

        FoodItem purchasingItem = currentItem;
        if (buyButton != null) buyButton.interactable = false;
        CoinCloudService.Purchase(purchasingItem.itemId, 1, result =>
        {
            if (buyButton != null) buyButton.interactable = true;
            if (!result.success)
            {
                if (itemDescriptionText != null) itemDescriptionText.text = result.message;
                return;
            }
            InventoryData.SetItemCount(purchasingItem.itemId, result.itemQuantity);
            if (coinDisplay != null) coinDisplay.Refresh();
            CloseItemPanel();
        });
    }

    void ClearContent()
    {
        if (contentParent == null) return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }

    void AssignServerItemCodes()
    {
        foreach (FoodItem item in foodItems)
        {
            if (item == null || !string.IsNullOrWhiteSpace(item.itemId)) continue;
            switch ((item.itemName ?? string.Empty).Trim())
            {
                case "蘋果": item.itemId = "F_APPLE"; break;
                case "香蕉": item.itemId = "F_BANANA"; break;
                case "鳳梨": item.itemId = "F_PINE"; break;
            }
        }
    }
}
