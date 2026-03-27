using GridGeneration;
using UnityEngine;
using UnityEngine.InputSystem;

public class RecourseCollectorBuilding : MonoBehaviour
{
    public enum BuildingType
    {
        Sawmill,
        Mine1,
        Mine2
    }

    [SerializeField] private GridMap gridMap;
    [SerializeField] private BuildingType selectedBuilding = BuildingType.Sawmill;

    private Keyboard keyboard;
    private bool mineModeSelected;

    private void OnEnable()
    {
        keyboard = Keyboard.current;
    }

    private void Update()
    {
        HandleBuildingKeybinds();
    }

    private void HandleBuildingKeybinds()
    {
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.oKey.wasPressedThisFrame)
        {
            ToggleMineMode();
        }

        if (mineModeSelected)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                SelectBuilding(BuildingType.Mine1);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                SelectBuilding(BuildingType.Mine2);
            }
        }
        else
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                SelectBuilding(BuildingType.Sawmill);
            }
        }
    }

    private void ToggleMineMode()
    {
        mineModeSelected = !mineModeSelected;

        if (mineModeSelected)
        {
            SelectBuilding(BuildingType.Mine1);
        }
        else
        {
            SelectBuilding(BuildingType.Sawmill);
        }
    }

    public bool CanPlaceSelectedBuilding(Vector2Int coordinate)
    {
        return CanPlaceBuilding(selectedBuilding, coordinate);
    }

    public bool CanPlaceBuilding(BuildingType buildingType, Vector2Int coordinate)
    {
        if (gridMap == null || !gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tile == null)
        {
            return false;
        }

        switch (buildingType)
        {
            case BuildingType.Sawmill:
                return HasExactTileName(tile, "Forest");

            case BuildingType.Mine1:
                return HasExactTileName(tile, "Valley");

            case BuildingType.Mine2:
                return HasExactTileName(tile, "Obstacle1");

            default:
                return false;
        }
    }

    private static bool HasExactTileName(GridTile tile, string expectedName)
    {
        return tile != null
            && !string.IsNullOrWhiteSpace(expectedName)
            && string.Equals(tile.tileName, expectedName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void SelectBuilding(BuildingType buildingType)
    {
        selectedBuilding = buildingType;
    }
}
