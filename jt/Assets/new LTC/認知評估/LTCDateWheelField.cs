using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Large, elderly-friendly date spinner. Users can use the arrow buttons,
/// mouse wheel, or touchpad scroll without typing a date string.
/// </summary>
public sealed class LTCDateWheelField : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float DragStepPixels = 28f;

    [SerializeField] private Button increaseButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private int minimum = 1;
    [SerializeField] private int maximum = 12;
    [SerializeField] private int value = 1;
    [SerializeField] private string suffix = string.Empty;

    private bool listenersBound;
    private float dragAccumulator;

    public int Value => value;
    public event Action<int> ValueChanged;

    public void Configure(Button increase, Button decrease, TMP_Text display, int min, int max, int initialValue,
        string valueSuffix)
    {
        increaseButton = increase;
        decreaseButton = decrease;
        valueText = display;
        suffix = valueSuffix ?? string.Empty;
        BindListeners();
        SetRange(min, max, initialValue, false);
    }

    public void SetRange(int min, int max, int newValue, bool notify = false)
    {
        minimum = Mathf.Min(min, max);
        maximum = Mathf.Max(min, max);
        SetValue(newValue, notify);
    }

    public void SetValue(int newValue, bool notify = false)
    {
        int clamped = Mathf.Clamp(newValue, minimum, maximum);
        bool changed = clamped != value;
        value = clamped;
        RefreshLabel();
        if (changed && notify) ValueChanged?.Invoke(value);
    }

    private void Awake()
    {
        BindListeners();
        RefreshLabel();
    }

    private void OnDestroy()
    {
        listenersBound = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData.scrollDelta.y > 0f) Increase();
        else if (eventData.scrollDelta.y < 0f) Decrease();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragAccumulator = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragAccumulator += eventData.delta.y;

        while (dragAccumulator >= DragStepPixels)
        {
            Increase();
            dragAccumulator -= DragStepPixels;
        }

        while (dragAccumulator <= -DragStepPixels)
        {
            Decrease();
            dragAccumulator += DragStepPixels;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragAccumulator = 0f;
    }

    private void BindListeners()
    {
        if (listenersBound) return;
        ConfigureRepeatButton(increaseButton, 1);
        ConfigureRepeatButton(decreaseButton, -1);
        listenersBound = true;
    }

    private void ConfigureRepeatButton(Button button, int direction)
    {
        if (button == null) return;
        LTCPressRepeatButton repeater = button.GetComponent<LTCPressRepeatButton>();
        if (repeater == null) repeater = button.gameObject.AddComponent<LTCPressRepeatButton>();
        repeater.Configure(this, direction);
    }

    public void Step(int direction)
    {
        if (direction > 0) Increase();
        else if (direction < 0) Decrease();
    }

    private void Increase()
    {
        int next = value >= maximum ? minimum : value + 1;
        SetValue(next, true);
    }

    private void Decrease()
    {
        int next = value <= minimum ? maximum : value - 1;
        SetValue(next, true);
    }

    private void RefreshLabel()
    {
        if (valueText != null) valueText.text = value + suffix;
    }
}
