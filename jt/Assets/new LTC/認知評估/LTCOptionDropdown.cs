using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>A simple large-text dropdown that does not depend on a TMP dropdown template.</summary>
public sealed class LTCOptionDropdown : MonoBehaviour
{
    [SerializeField] private Button headerButton;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private List<Button> optionButtons = new List<Button>();
    [SerializeField] private List<string> labels = new List<string>();
    [SerializeField] private List<string> codes = new List<string>();
    [SerializeField] private string fieldLabel = string.Empty;
    [SerializeField] private int selectedIndex;

    private bool listenersBound;
    private readonly List<UnityAction> optionActions = new List<UnityAction>();
    private static LTCOptionDropdown openDropdown;

    public string SelectedCode => selectedIndex >= 0 && selectedIndex < codes.Count ? codes[selectedIndex] : string.Empty;
    public event Action<string> ValueChanged;

    public void Configure(Button header, TMP_Text caption, GameObject menu, IList<Button> buttons,
        IList<string> optionLabels, IList<string> optionCodes, string title)
    {
        headerButton = header;
        captionText = caption;
        optionsPanel = menu;
        optionButtons = new List<Button>(buttons);
        labels = new List<string>(optionLabels);
        codes = new List<string>(optionCodes);
        fieldLabel = title ?? string.Empty;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, labels.Count - 1));
        BindListeners();
        Close();
        RefreshCaption();
    }

    public void SelectCode(string code, bool notify = false)
    {
        int index = codes.IndexOf(code ?? string.Empty);
        SelectIndex(index < 0 ? 0 : index, notify);
    }

    private void Awake()
    {
        BindListeners();
        Close();
        RefreshCaption();
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnDestroy()
    {
        if (!listenersBound) return;
        if (headerButton != null) headerButton.onClick.RemoveListener(Toggle);
        for (int index = 0; index < optionButtons.Count; index++)
        {
            if (index < optionActions.Count && optionButtons[index] != null)
                optionButtons[index].onClick.RemoveListener(optionActions[index]);
        }
        optionActions.Clear();
        listenersBound = false;
    }

    private void BindListeners()
    {
        if (listenersBound) return;
        if (headerButton != null) headerButton.onClick.AddListener(Toggle);
        optionActions.Clear();
        for (int index = 0; index < optionButtons.Count; index++)
        {
            int captured = index;
            UnityAction action = () => SelectIndex(captured, true);
            optionActions.Add(action);
            if (optionButtons[captured] != null) optionButtons[captured].onClick.AddListener(action);
        }
        listenersBound = true;
    }

    private void Toggle()
    {
        if (optionsPanel == null) return;
        bool shouldOpen = !optionsPanel.activeSelf;
        if (openDropdown != null && openDropdown != this) openDropdown.Close();
        optionsPanel.SetActive(shouldOpen);
        if (shouldOpen)
        {
            openDropdown = this;
            optionsPanel.transform.SetAsLastSibling();
        }
        else if (openDropdown == this) openDropdown = null;
    }

    private void Close()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (openDropdown == this) openDropdown = null;
    }

    private void SelectIndex(int index, bool notify)
    {
        selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, labels.Count - 1));
        RefreshCaption();
        Close();
        if (notify) ValueChanged?.Invoke(SelectedCode);
    }

    private void RefreshCaption()
    {
        if (captionText == null || labels.Count == 0) return;
        captionText.text = fieldLabel + "：" + labels[Mathf.Clamp(selectedIndex, 0, labels.Count - 1)] + "　▼";
    }
}
