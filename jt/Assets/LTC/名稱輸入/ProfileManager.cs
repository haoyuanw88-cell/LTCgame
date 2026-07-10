using UnityEngine;
using TMPro; // 處理文字與輸入框必備
using System.Text.RegularExpressions; // 處理中英文驗證必備

public class ProfileManager : MonoBehaviour
{
    [Header("UI 元件連結")]
    [Tooltip("對應：名稱顯示")]
    public TMP_Text displayNameText;

    [Tooltip("對應：輸入名稱 (InputField)")]
    public TMP_InputField nameInputField;

    [Tooltip("對應：個資面板")]
    public GameObject infoPanel;

    [Header("輸入規則設定")]
    public int minLength = 2;
    public int maxLength = 8;

    private void Start()
    {
        // 初始化：讀取先前存檔的名字，若無存檔則顯示預設文字
        string savedName = PlayerPrefs.GetString("SavedPlayerName", displayNameText.text);
        displayNameText.text = savedName;

        // 確保遊戲開始時面板是隱藏的
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    /// <summary>
    /// 開啟面板：掛載於「頭像」按鈕的 OnClick
    /// </summary>
    public void OpenProfilePanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
            // 開啟時自動填入目前顯示的名字
            nameInputField.text = displayNameText.text;
        }
    }

    /// <summary>
    /// 儲存並關閉：掛載於「關閉鈕」按鈕的 OnClick
    /// </summary>
    public void SaveAndClose()
    {
        // 1. 取得輸入文字並去除前後空白
        string inputName = nameInputField.text.Trim();

        // 2. 正規表示式驗證：僅限中文 (\u4e00-\u9fa5) 與 英文 (a-zA-Z)
        // 並根據設定的字數限制進行檢查
        string pattern = @"^[a-zA-Z\u4e00-\u9fa5]{" + minLength + "," + maxLength + "}$";

        if (Regex.IsMatch(inputName, pattern))
        {
            // 驗證通過：更新 UI
            displayNameText.text = inputName;

            // 資料持久化：儲存到硬碟
            PlayerPrefs.SetString("SavedPlayerName", inputName);
            PlayerPrefs.Save();

            // 關閉面板
            infoPanel.SetActive(false);
            Debug.Log("<color=green>成功修改名字！</color>");
        }
        else
        {
            // 驗證失敗：控制台報錯，面板不關閉
            Debug.LogError($"修改失敗！請輸入 {minLength}-{maxLength} 個中英文字元。");
        }
    }

    /// <summary>
    /// 直接關閉不儲存：可用於「取消」或點擊背景
    /// </summary>
    public void CancelEdit()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}