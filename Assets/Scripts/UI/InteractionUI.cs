using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionUI : MonoBehaviour
{
    [Header("Build Options")]
    [SerializeField] private int[][] buildCosts; // buildCosts[i][0] = wood, buildCosts[i][1] = stone

    [Header("UI References")]
    [SerializeField] private Button[] buildButtons;
    [SerializeField] private Color normalButtonColor;
    [SerializeField] private Color selectedButtonColor;
    [SerializeField] private TMP_Text woodCostText;
    [SerializeField] private TMP_Text stoneCostText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TileBuildContextPanel tileBuildContextPanel;

    [SerializeField] private Turns turns;

    private int _selectedButtonIndex = -1;

    private void Start()
    {
        if (turns == null)
        {
            turns = FindAnyObjectByType<Turns>();
        }

        if (tileBuildContextPanel == null)
        {
            tileBuildContextPanel = FindAnyObjectByType<TileBuildContextPanel>();
        }

        SetupButtons();
        buildCosts = new int[3][]
        {
            new int[] { 0, 1 },
            new int[] { 2, 2 },
            new int[] { 5, 4 }
        };

    }

    private void SetupButtons()
    {
        if (buildButtons == null || buildButtons.Length == 0)
        {
            Debug.LogWarning("InteractionUI: No build buttons assigned.", this);
            return;
        }

        for (int i = 0; i < buildButtons.Length; i++)
        {
            if (buildButtons[i] == null)
            {
                continue;
            }

            ApplyButtonState(i, isSelected: false);
            int buttonIndex = i;
            buildButtons[i].onClick.AddListener(() => OnButtonPressed(buttonIndex));
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmed);
        }
    }

    public void OnButtonPressed(int pressedButton)
    {
        if (pressedButton < 0 || pressedButton >= buildButtons.Length)
        {
            Debug.LogError($"InteractionUI: Invalid button index {pressedButton}.", this);
            return;
        }

        if (buildCosts == null || pressedButton >= buildCosts.Length)
        {
            Debug.LogError($"InteractionUI: Build costs not configured for button {pressedButton}.", this);
            return;
        }

        // Reset previous selection
        if (_selectedButtonIndex >= 0 && _selectedButtonIndex < buildButtons.Length && buildButtons[_selectedButtonIndex] != null)
        {
            ApplyButtonState(_selectedButtonIndex, isSelected: false);
        }

        // Select new button
        _selectedButtonIndex = pressedButton;
        if (buildButtons[pressedButton] == null)
        {
            return;
        }

        ApplyButtonState(pressedButton, isSelected: true);

        // Update cost texts
        int woodCost = buildCosts[pressedButton][0];
        int stoneCost = buildCosts[pressedButton][1];

        if (woodCostText != null)
        {
            woodCostText.text = woodCost.ToString();
        }

        if (stoneCostText != null)
        {
            stoneCostText.text = stoneCost.ToString();
        }

        Debug.Log($"InteractionUI: Selected build option {pressedButton} - Wood: {woodCost}, Stone: {stoneCost}");

        if (tileBuildContextPanel != null)
        {
            tileBuildContextPanel.SelectBuildOptionByIndex(pressedButton);
        }
    }

    public void OnConfirmed()
    {
        if (_selectedButtonIndex < 0)
        {
            Debug.LogWarning("InteractionUI: No build option selected.", this);
            return;
        }

        if (buildCosts == null || _selectedButtonIndex >= buildCosts.Length)
        {
            Debug.LogError("InteractionUI: Build costs not configured.", this);
            return;
        }

        if (tileBuildContextPanel != null)
        {
            tileBuildContextPanel.SelectBuildOptionByIndex(_selectedButtonIndex);
            tileBuildContextPanel.ConfirmSelectedBuild();
            return;
        }

        Debug.LogWarning("InteractionUI: TileBuildContextPanel is not assigned, so confirm cannot execute build logic.", this);
    }

    private void ApplyButtonState(int index, bool isSelected)
    {
        if (index < 0 || index >= buildButtons.Length || buildButtons[index] == null)
        {
            return;
        }

        ColorBlock colors = buildButtons[index].colors;
        Color fallbackNormal = colors.normalColor;
        Color fallbackSelected = colors.pressedColor;

        Color configuredNormal = GetConfiguredColor(normalButtonColor, fallbackNormal);
        Color configuredSelected = GetConfiguredColor(selectedButtonColor, fallbackSelected);
        Color targetColor = isSelected ? configuredSelected : configuredNormal;

        // Keep the visual state stable after click by applying the same tint for non-pressed states.
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.selectedColor = targetColor;
        buildButtons[index].colors = colors;
    }

    private Color GetConfiguredColor(Color color, Color fallback)
    {
        if (color == null)
        {
            return fallback;
        }

        return color;
    }
}
