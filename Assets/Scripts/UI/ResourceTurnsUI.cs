using TMPro;
using UnityEngine;

public class ResourceTurnsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text woodText, stoneText, turnsText;

    public void UpdateTexts(int wood, int stone, int turns)
    {
        woodText.text = wood.ToString();
        stoneText.text = stone.ToString();
        turnsText.text = turns.ToString();
    }
}
