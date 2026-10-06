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

        int currentCount = PlayerPrefs.GetInt("ItemCount_" + itemId, 0);
        int newCount = currentCount + amount;

        PlayerPrefs.SetInt("ItemCount_" + itemId, newCount);
        PlayerPrefs.Save();
    }

    public static int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;

        // Shop purchases return the server's cumulative quantity. Keep local
        // feeding consumption separate so the next purchase does not restore it.
        return Mathf.Max(0, PlayerPrefs.GetInt("ItemCount_" + itemId, 0) -
            PlayerPrefs.GetInt("ItemConsumed_" + itemId, 0));
    }

    public static bool TryConsumeItemCount(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0 || GetItemCount(itemId) < amount) return false;
        string key = "ItemConsumed_" + itemId;
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
        PlayerPrefs.Save();
        return true;
    }

    public static void SetItemCount(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        PlayerPrefs.SetInt("ItemCount_" + itemId, Mathf.Max(0, amount));
        PlayerPrefs.Save();
    }
}
