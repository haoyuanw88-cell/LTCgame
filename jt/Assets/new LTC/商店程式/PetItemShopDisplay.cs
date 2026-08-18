using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PetItemShopDisplay : MonoBehaviour
{
    [System.Serializable]
    public class PetItem
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
    public PetItemCard petItemCardPrefab;
    public List<PetItem> petItems = new List<PetItem>();

    [Header("商品詳細面板")]
    public GameObject itemPanel;
    public Image itemImage;
    public TMP_Text itemNameText;
    public TMP_Text itemDescriptionText;
    public Button closeButton;
    public Button buyButton;

    [Header("金幣顯示")]
    public CoinDisplay coinDisplay;

    private PetItem currentItem;

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

        ShowPetItems();
    }

    public void ShowPetItems()
    {
        ClearContent();

        for (int i = 0; i < petItems.Count; i++)
        {
            PetItemCard card = Instantiate(petItemCardPrefab, contentParent);
            card.Setup(petItems[i], this);
        }
    }

    public void OpenItemPanel(PetItem item)
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

        if (InventoryData.HasItem(currentItem.itemId))
        {
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = "已經購買過此商品。";
            }

            return;
        }

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

        PetItem purchasingItem = currentItem;
        if (buyButton != null) buyButton.interactable = false;
        CoinCloudService.Purchase(purchasingItem.itemId, 1, result =>
        {
            if (buyButton != null) buyButton.interactable = true;
            if (!result.success)
            {
                if (itemDescriptionText != null) itemDescriptionText.text = result.message;
                return;
            }
            InventoryData.AddItem(purchasingItem.itemId);
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
}
