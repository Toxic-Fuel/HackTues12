using TMPro;
using UnityEngine;

public class ResourceTurnsUI : MonoBehaviour
{
    [Header("Resource Icons")]
    [SerializeField] private Sprite[] sprites;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject CurrentResourcePrefab;
    [SerializeField] private GameObject ResourcesPerTurnPrefab;

    [Header("UI References")]
    [SerializeField] private TMP_Text turnsText;
    [SerializeField] private GameObject CurrentResourcesParent;
    [SerializeField] private GameObject ResourcesPerTurnParent;

    private TMP_Text[] currentResourcesTexts;
    private TMP_Text[] resourcesPerTurnTexts;
    public void UpdateTexts(int[] currentResources, int[] resourcesPerTurn)
    {
        for (int resourceIndex = 0; resourceIndex < currentResources.Length-1; resourceIndex++)
        {
            currentResourcesTexts[resourceIndex].text = currentResources[resourceIndex+1].ToString();
        }
        turnsText.text = currentResources[(int)ResourceType.Turn].ToString();

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
