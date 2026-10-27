using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "LTC/Supermarket Level", fileName = "SupermarketLevel")]
public sealed class SupermarketLevelData : ScriptableObject
{
    public string displayName = "購物練習";
    [Min(.5f)] public float panelHideDelay = 3f;
    public List<ShoppingRequirement> products = new List<ShoppingRequirement>();
    public bool ValidateProducts(ICollection<Sprite> available, out string error)
    {
        if (products == null || products.Count == 0) { error = "請至少加入一項商品。"; return false; }
        var seen = new HashSet<Sprite>();
        foreach (var item in products)
        {
            if (item == null || item.productImage == null || item.quantity < 1)
            { error = "每項商品都需要圖片及大於零的數量。"; return false; }
            if (!seen.Add(item.productImage)) { error = "商品重複，請合併數量。"; return false; }
            if (available != null && !available.Contains(item.productImage))
            { error = "場景中沒有可購買的商品：" + item.productImage.name; return false; }
        }
        error = ""; return true;
    }
    public List<ShoppingRequirement> CreateRequirements()
    {
        var result = new List<ShoppingRequirement>();
        foreach (var item in products)
            result.Add(new ShoppingRequirement { productImage = item.productImage, quantity = item.quantity, remaining = item.quantity });
        return result;
    }
    public int TotalQuantity { get { int total = 0; foreach (var item in products) if (item != null) total += item.quantity; return total; } }
}