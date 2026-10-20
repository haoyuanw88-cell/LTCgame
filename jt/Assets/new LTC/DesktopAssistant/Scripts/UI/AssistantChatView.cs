using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 管理文字輸入、訊息紀錄與等待狀態。實際 AI 呼叫由 Controller 負責。
    /// </summary>
    public sealed class AssistantChatView : MonoBehaviour
    {
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;
        [SerializeField] TMP_Text transcriptText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] ScrollRect scrollRect;

        readonly Queue<string> messages = new Queue<string>();
        int maximumMessages = 30;

        public event Action<string> Submitted;

        void Awake()
        {
            if (sendButton != null) sendButton.onClick.AddListener(SubmitCurrentText);
            if (inputField != null) inputField.onSubmit.AddListener(SubmitText);
        }

        void OnDestroy()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(SubmitCurrentText);
            if (inputField != null) inputField.onSubmit.RemoveListener(SubmitText);
        }

        public void Configure(TMP_InputField input, Button button, TMP_Text transcript,
            TMP_Text status, ScrollRect scroll)
        {
            inputField = input;
            sendButton = button;
            transcriptText = transcript;
            statusText = status;
            scrollRect = scroll;
        }

        public void SetMaximumMessages(int value)
        {
            maximumMessages = Mathf.Max(4, value);
        }

        public void SetBusy(bool busy, string status = "")
        {
            if (sendButton != null) sendButton.interactable = !busy;
            if (inputField != null) inputField.interactable = !busy;
            if (statusText != null) statusText.text = status ?? string.Empty;
        }

        public void AppendMessage(string speaker, string message)
        {
            messages.Enqueue("<b>" + speaker + "</b>：" + (message ?? string.Empty));
            while (messages.Count > maximumMessages) messages.Dequeue();
            if (transcriptText != null) transcriptText.text = string.Join("\n\n", messages);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        }

        void SubmitCurrentText()
        {
            SubmitText(inputField == null ? string.Empty : inputField.text);
        }

        void SubmitText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string trimmed = value.Trim();
            if (inputField != null) inputField.text = string.Empty;
            Submitted?.Invoke(trimmed);
        }
    }
}
