using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 集中保存 AI 助理可調整的顯示與互動設定。
    /// 未來接入畫面辨識或後端時，不需要把端點與頻率散落在各腳本中。
    /// </summary>
    [CreateAssetMenu(fileName = "DesktopAssistantSettings", menuName = "LTC/桌面 AI 助理/設定")]
    public sealed class AssistantSettings : ScriptableObject
    {
        [Header("角色")]
        [Tooltip("玩家沒有任何寵物時使用的預設外觀；不會因此授予寵物。")]
        public string fallbackPetId = "rabbit";

        [Tooltip("寵物動畫每秒播放的影格數。")]
        [Range(1f, 20f)] public float animationFramesPerSecond = 8f;

        [Header("對話")]
        [Tooltip("AI 回答顯示在角色氣泡中的秒數。")]
        [Range(1f, 20f)] public float bubbleDuration = 6f;

        [Tooltip("說話動畫至少維持的秒數。")]
        [Range(0.2f, 10f)] public float talkingDuration = 2.2f;

        [Tooltip("聊天室最多保留的訊息筆數。")]
        [Range(4, 100)] public int maximumMessages = 30;

        [Header("拖曳")]
        [Tooltip("助理與螢幕邊緣保留的安全距離。")]
        [Range(0f, 100f)] public float screenMargin = 18f;

        [Header("未來後端代理")]
        [Tooltip("第三階段才會使用。API Key 必須保留在後端，不能填在這裡。")]
        public string backendProxyPath = "/api/v1/assistant/chat";
    }
}
