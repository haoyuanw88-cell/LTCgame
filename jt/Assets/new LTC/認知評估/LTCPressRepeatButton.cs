using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a date-wheel arrow repeat while it is held. A short press changes once;
/// holding for a moment continues at an elderly-friendly, predictable pace.
/// </summary>
public sealed class LTCPressRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float InitialDelaySeconds = 0.45f;
    private const float RepeatIntervalSeconds = 0.12f;

    private LTCDateWheelField owner;
    private Button button;
    private int direction;
    private Coroutine repeatRoutine;

    public void Configure(LTCDateWheelField target, int stepDirection)
    {
        owner = target;
        direction = stepDirection > 0 ? 1 : -1;
        if (button == null) button = GetComponent<Button>();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner == null || (button != null && !button.interactable)) return;
        StopRepeating();
        owner.Step(direction);
        repeatRoutine = StartCoroutine(RepeatAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopRepeating();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopRepeating();
    }

    private void OnDisable()
    {
        StopRepeating();
    }

    private IEnumerator RepeatAfterDelay()
    {
        yield return new WaitForSecondsRealtime(InitialDelaySeconds);
        while (isActiveAndEnabled && owner != null && (button == null || button.interactable))
        {
            owner.Step(direction);
            yield return new WaitForSecondsRealtime(RepeatIntervalSeconds);
        }
    }

    private void StopRepeating()
    {
        if (repeatRoutine == null) return;
        StopCoroutine(repeatRoutine);
        repeatRoutine = null;
    }
}
