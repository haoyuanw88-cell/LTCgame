using TMPro;
using UnityEngine;

public class ShopCategoryManager : MonoBehaviour
{
    public TMP_Text categoryTitleText;

    [Header("分類區塊")]
    public GameObject petItemShopRoot;
    public GameObject foodShopRoot;

    void Start()
    {
        ShowFoodCategory();
    }

    public void ShowPetItemCategory()
    {
        ShowCategory("寵物道具", petItemShopRoot);
    }

    public void ShowFoodCategory()
    {
        ShowCategory("食物", foodShopRoot);
    }

    void ShowCategory(string categoryName, GameObject targetRoot)
    {
        if (categoryTitleText != null)
        {
            categoryTitleText.text = categoryName;
        }

        if (petItemShopRoot != null)
        {
            petItemShopRoot.SetActive(false);
        }

        if (foodShopRoot != null)
        {
            foodShopRoot.SetActive(false);
        }

        if (targetRoot != null)
        {
            targetRoot.SetActive(true);
        }
    }
}