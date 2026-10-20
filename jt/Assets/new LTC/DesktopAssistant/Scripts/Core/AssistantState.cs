namespace LTC.DesktopAssistant
{
    /// <summary>
    /// AI 助理的可視狀態。狀態只控制顯示效果，不會操作其他遊戲。
    /// </summary>
    public enum AssistantState
    {
        Idle,
        Thinking,
        Talking,
        Reminder
    }
}
