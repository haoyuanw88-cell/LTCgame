using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 三款認知遊戲提供給共用暫停選單的最小介面。
/// </summary>
public interface ICognitiveGamePauseTarget
{
    bool IsAssessmentRunning { get; }
    void RestartCurrentItemAfterPause();
    void CancelCurrentAssessment();
}

/// <summary>
/// 暫停時凍結遊戲；恢復時捨棄未完成題，回到上一題完成後的計時邊界並換同難度新題。
/// </summary>
public sealed class CognitiveGamePauseMenu : MonoBehaviour
{
    public Button pauseButton;
    public GameObject pausePanel;
    public Button resumeButton;
    public Button homeButton;
    public TMP_Text messageText;
    public string gameHomeScene = "GameScene";

    private ICognitiveGamePauseTarget pauseTarget;
    private bool isPaused;

    private void Awake()
    {
        ResolveTarget();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.onClick.AddListener(OpenPauseMenu);
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (homeButton != null) homeButton.onClick.AddListener(ReturnHomeWithoutSaving);
        if (messageText != null)
        {
            messageText.text =
                "返回遊戲時，會捨棄目前這一題，\n" +
                "恢復到上一題完成時的剩餘時間，\n" +
                "並重新產生相同難度的新題目。";
        }
    }

    private void ResolveTarget()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ICognitiveGamePauseTarget target)
            {
                pauseTarget = target;
                return;
            }
        }
    }

    public void OpenPauseMenu()
    {
        if (isPaused) return;
        if (pauseTarget == null) ResolveTarget();
        if (pauseTarget == null || !pauseTarget.IsAssessmentRunning) return;

        isPaused = true;
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
        }

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        Time.timeScale = 1f;
        pauseTarget?.RestartCurrentItemAfterPause();
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;
    }

    public void ReturnHomeWithoutSaving()
    {
        Time.timeScale = 1f;
        pauseTarget?.CancelCurrentAssessment();
        isPaused = false;
        SceneManager.LoadScene(gameHomeScene);
    }

    private void OnDestroy()
    {
        if (isPaused) Time.timeScale = 1f;
    }
}
