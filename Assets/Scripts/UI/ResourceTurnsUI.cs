using TMPro;
using UnityEngine;

public class ResourceTurnsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text woodText, stoneText, turnsText;
    [SerializeField] private TMP_Text woodPerTurnText, stonePerTurnText;

    public void UpdateTexts(int wood, int stone, int turns, int woodPerTurn, int stonePerTurn)
    {
        woodText.text = wood.ToString();
        stoneText.text = stone.ToString();
        turnsText.text = turns.ToString();

        if (woodPerTurnText != null)
        {
            woodPerTurnText.text = $"+{woodPerTurn}/t";
        }

        if (stonePerTurnText != null)
        {
            stonePerTurnText.text = $"+{stonePerTurn}/t";
        }
    }
}
