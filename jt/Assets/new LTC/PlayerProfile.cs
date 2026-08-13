using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

// 類別名稱必須與檔案名稱 (PlayerProfile.cs) 完全一致
public class PlayerProfile : MonoBehaviour
{
    [Header("介面物件連結")]
    [Tooltip("用來顯示玩家名字的 TextMeshPro 元件")]
    public TMP_Text text_PlayerName;

    [Tooltip("玩家輸入名字的 Input Field")]
    public TMP_InputField input_NameField;

    [Tooltip("修改名字的彈出視窗面板")]
    public GameObject panel_EditProfile;

    [Header("名字規則設定")]
    public int minLength = 2;
    public int maxLength = 8;

    private void Start()
    {
        // 遊戲開始時，讀取存檔的名字
        // 如果沒有存檔紀錄，就顯示畫面上原本的文字
        string savedName = PlayerPrefs.GetString("AccountName", text_PlayerName.text);
        text_PlayerName.text = savedName;

        // 確保面板一開始是隱藏的
        if (panel_EditProfile != null)
            panel_EditProfile.SetActive(false);
    }

    // 按下「修改資料」按鈕時執行
    public void OpenEditUI()
    {
        if (panel_EditProfile != null)
        {
            panel_EditProfile.SetActive(true);
            // 將目前顯示的名字填入輸入框
            input_NameField.text = text_PlayerName.text;
        }
    }

    // 按下「確認儲存」按鈕時執行
    public void ConfirmUpdate()
    {
        string newName = input_NameField.text.Trim();

        // 規則：僅限中文與英文，並符合長度限制
        string pattern = @"^[a-zA-Z\u4e00-\u9fa5]{" + minLength + "," + maxLength + "}$";

        if (Regex.IsMatch(newName, pattern))
        {
            // 更新畫面
            text_PlayerName.text = newName;

            // 儲存到本地端
            PlayerPrefs.SetString("AccountName", newName);
            PlayerPrefs.Save();

            // 關閉視窗
            panel_EditProfile.SetActive(false);
            Debug.Log("<color=green>玩家名字更新成功！</color>");
        }
        else
        {
            // 驗證失敗報錯
            Debug.LogError($"名字格式不符：請輸入 {minLength}~{maxLength} 個中英文字。");
        }
    }

    // 按下「取消」或關閉時執行
    public void CloseEditUI()
    {
        if (panel_EditProfile != null)
            panel_EditProfile.SetActive(false);
    }
}