using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 控制狀態按鍵的浮動選單。平常只顯示「功能」按鍵，使用後自動收合。
    /// </summary>
    public sealed class AssistantStateMenu : MonoBehaviour
    {
        [SerializeField] GameObject menuPanel;

        void Awake()
        {
            Close();
        }

        public void Configure(GameObject panel)
        {
            menuPanel = panel;
        }

        public void Toggle()
        {
            if (menuPanel != null) menuPanel.SetActive(!menuPanel.activeSelf);
        }

        public void Close()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
        }
    }
}
