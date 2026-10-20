using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 顯示寵物角色、對話氣泡，以及待機、思考、說話、提醒四種狀態。
    /// 動畫只影響 UI，不會送出鍵盤或滑鼠輸入。
    /// </summary>
    public sealed class AssistantCharacterView : MonoBehaviour
    {
        [SerializeField] Image characterImage;
        [SerializeField] GameObject bubbleRoot;
        [SerializeField] TMP_Text bubbleText;
        [SerializeField] TMP_Text stateEffectText;
        [SerializeField] float framesPerSecond = 8f;

        RectTransform characterRect;
        Vector2 basePosition;
        Sprite idleSprite;
        Sprite[] animationFrames = new Sprite[0];
        Coroutine bubbleRoutine;
        AssistantState currentState;
        float stateTime;
        int frameIndex;

        public AssistantState CurrentState => currentState;
        public string CurrentPetId { get; private set; } = string.Empty;

        void Awake()
        {
            characterRect = characterImage == null ? null : characterImage.rectTransform;
            if (characterRect != null) basePosition = characterRect.anchoredPosition;
            if (bubbleRoot != null) bubbleRoot.SetActive(false);
            SetState(AssistantState.Idle);
        }

        void Update()
        {
            if (characterRect == null) return;
            stateTime += Time.unscaledDeltaTime;

            // 說話時依照使用者需求保持角色完全靜止；其他狀態才套用浮動效果。
            float bob = currentState == AssistantState.Talking
                ? 0f
                : Mathf.Sin(stateTime * 2.2f) * 4f;
            characterRect.anchoredPosition = basePosition + Vector2.up * bob;
            characterRect.localRotation = Quaternion.identity;
            characterRect.localScale = Vector3.one;

            switch (currentState)
            {
                case AssistantState.Thinking:
                    characterRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(stateTime * 2f) * 3f);
                    break;
                case AssistantState.Talking:
                    if (characterImage != null && idleSprite != null) characterImage.sprite = idleSprite;
                    break;
                case AssistantState.Reminder:
                    characterRect.anchoredPosition += Vector2.right * Mathf.Sin(stateTime * 18f) * 7f;
                    break;
                default:
                    if (characterImage != null && idleSprite != null) characterImage.sprite = idleSprite;
                    break;
            }
        }

        public void Configure(Image image, GameObject bubble, TMP_Text bubbleLabel, TMP_Text stateLabel)
        {
            characterImage = image;
            bubbleRoot = bubble;
            bubbleText = bubbleLabel;
            stateEffectText = stateLabel;
        }

        public void SetAnimationSpeed(float value)
        {
            framesPerSecond = Mathf.Max(1f, value);
        }

        public void SetPet(LTCPetDefinition pet, Sprite preview, Sprite[] frames)
        {
            CurrentPetId = pet == null ? string.Empty : pet.id;
            idleSprite = preview;
            animationFrames = frames ?? new Sprite[0];
            frameIndex = 0;
            if (characterImage != null)
            {
                characterImage.sprite = idleSprite;
                characterImage.preserveAspect = true;
                characterImage.color = Color.white;
            }
        }

        public void SetState(AssistantState state)
        {
            currentState = state;
            stateTime = 0f;
            frameIndex = 0;
            if (characterImage != null && idleSprite != null) characterImage.sprite = idleSprite;
            if (stateEffectText == null) return;

            stateEffectText.text = state == AssistantState.Thinking ? "…"
                : state == AssistantState.Reminder ? "!"
                : state == AssistantState.Talking ? "♪"
                : string.Empty;
            stateEffectText.gameObject.SetActive(state != AssistantState.Idle);
        }

        public void ShowBubble(string message, float duration)
        {
            if (bubbleRoot == null || bubbleText == null) return;
            if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
            bubbleRoutine = StartCoroutine(ShowBubbleRoutine(message, duration));
        }

        IEnumerator ShowBubbleRoutine(string message, float duration)
        {
            bubbleText.text = message ?? string.Empty;
            bubbleRoot.SetActive(true);
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, duration));
            bubbleRoot.SetActive(false);
            bubbleRoutine = null;
        }

        void AnimateFrames()
        {
            if (characterImage == null || animationFrames.Length == 0) return;
            int nextFrame = Mathf.FloorToInt(stateTime * framesPerSecond) % animationFrames.Length;
            if (nextFrame == frameIndex && characterImage.sprite != null) return;
            frameIndex = nextFrame;
            if (animationFrames[frameIndex] != null) characterImage.sprite = animationFrames[frameIndex];
        }
    }
}
