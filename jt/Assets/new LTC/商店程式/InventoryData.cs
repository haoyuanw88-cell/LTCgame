using UnityEngine;

public static class InventoryData
{
    public static void AddItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        PlayerPrefs.SetInt("Owned_" + itemId, 1);
        PlayerPrefs.Save();
    }

    public static bool HasItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;

        return PlayerPrefs.GetInt("Owned_" + itemId, 0) == 1;
    }

    public static void AddItemCount(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        int currentCount = GetItemCount(itemId);
        int newCount = currentCount + amount;

        PlayerPrefs.SetInt("ItemCount_" + itemId, newCount);
        PlayerPrefs.Save();
    }

    public static int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;

        return PlayerPrefs.GetInt("ItemCount_" + itemId, 0);
    }

    public static void SetItemCount(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        PlayerPrefs.SetInt("ItemCount_" + itemId, Mathf.Max(0, amount));
        PlayerPrefs.Save();
    }
}