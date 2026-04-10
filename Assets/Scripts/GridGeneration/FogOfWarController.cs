using GridGeneration;
using System.Collections;
using UnityEngine;

public class FogOfWarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private TileBuilding tileBuilding;

    [Header("Fog")]
    [SerializeField] private GameObject fogTilePrefab;
    [SerializeField, Min(0)] private int revealRadius = 1;
    [SerializeField] private bool hideUnrevealedTileVisuals = true;
    [SerializeField] private bool randomizeFogRotation = true;
    [SerializeField] private Vector2 fogYawRange = new Vector2(0f, 360f);

    [Header("Reveal Animation")]
    [SerializeField] private bool animateReveal = true;
    [SerializeField, Min(0f)] private float revealAnimationDuration = 0.22f;
    [SerializeField] private AnimationCurve revealScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private GameObject[,] fogInstances;
    private bool[,] revealedTiles;

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (gridMap == null)
        {
            gridMap = FindAnyObjectByType<GridMap>();
        }

        if (tileBuilding == null)
        {
            tileBuilding = FindAnyObjectByType<TileBuilding>();
        }
    }

    private void OnEnable()
    {
        if (gridMap != null)
        {
            gridMap.MapGenerated += OnMapGenerated;
        }

        if (tileBuilding != null)
        {
            tileBuilding.RoadBuiltAt += OnRoadBuilt;
            tileBuilding.StructureBuiltAt += OnStructureBuilt;
        }
    }

    private void Start()
    {
        TryInitializeFog(forceRebuild: true);
    }

    private void OnDisable()
    {
        if (gridMap != null)
        {
            gridMap.MapGenerated -= OnMapGenerated;
        }

        if (tileBuilding != null)
        {
            tileBuilding.RoadBuiltAt -= OnRoadBuilt;
            tileBuilding.StructureBuiltAt -= OnStructureBuilt;
        }
    }

    public bool IsRevealed(Vector2Int coordinate)
    {
        if (gridMap == null || !gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        if (!IsInitialized || revealedTiles == null)
        {
            // Avoid blocking interaction during very early startup before fog is built.
            return true;
        }

        return revealedTiles[coordinate.x, coordinate.y];
    }

    public void RevealAround(Vector2Int center, int radius)
    {
        if (!IsInitialized || gridMap == null || revealedTiles == null)
        {
            return;
        }

        int clampedRadius = Mathf.Max(0, radius);
        for (int dx = -clampedRadius; dx <= clampedRadius; dx++)
        {
            for (int dy = -clampedRadius; dy <= clampedRadius; dy++)
            {
                Vector2Int coordinate = new Vector2Int(center.x + dx, center.y + dy);
                if (!gridMap.IsInsideGrid(coordinate))
                {
                    continue;
                }

                RevealTile(coordinate);
            }
        }
    }

    private void OnMapGenerated(GridMap _)
    {
        TryInitializeFog(forceRebuild: true);
    }

    private void OnRoadBuilt(Vector2Int coordinate)
    {
        RevealAround(coordinate, revealRadius);
    }

    private void OnStructureBuilt(Vector2Int coordinate)
    {
        RevealAround(coordinate, revealRadius);
    }

    private void TryInitializeFog(bool forceRebuild)
    {
        if (gridMap == null || fogTilePrefab == null || gridMap.tileMap == null)
        {
            return;
        }

        int width = gridMap.Width;
        int height = gridMap.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        bool sameSize = fogInstances != null
            && revealedTiles != null
            && fogInstances.GetLength(0) == width
            && fogInstances.GetLength(1) == height;

        if (!forceRebuild && sameSize && IsInitialized)
        {
            return;
        }

        ClearFogInstances();

        fogInstances = new GameObject[width, height];
        revealedTiles = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject tileInstance = gridMap.GetTileInstanceAt(x, y);
                if (tileInstance == null)
                {
                    continue;
                }

                GameObject fogInstance = Instantiate(fogTilePrefab, tileInstance.transform.position, Quaternion.identity, gridMap.transform);
                fogInstance.name = $"Fog_{x}_{y}";
                ApplyRandomRotation(fogInstance.transform);
                fogInstances[x, y] = fogInstance;
            }
        }

        if (hideUnrevealedTileVisuals)
        {
            SetAllTileVisualsVisible(false);
        }

        IsInitialized = true;
        RevealSettlements();
    }

    private void RevealSettlements()
    {
        if (!IsInitialized || gridMap == null)
        {
            return;
        }

        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                GridTile tile = gridMap.GetTileAt(x, y);
                if (!IsSettlementTile(tile))
                {
                    continue;
                }

                RevealAround(new Vector2Int(x, y), revealRadius);
            }
        }
    }

    private void RevealTile(Vector2Int coordinate)
    {
        if (!gridMap.IsInsideGrid(coordinate) || revealedTiles == null)
        {
            return;
        }

        if (revealedTiles[coordinate.x, coordinate.y])
        {
            return;
        }

        revealedTiles[coordinate.x, coordinate.y] = true;

        if (hideUnrevealedTileVisuals)
        {
            SetTileVisualsVisible(coordinate, true);
        }

        GameObject fogInstance = fogInstances[coordinate.x, coordinate.y];
        if (fogInstance != null)
        {
            if (animateReveal && Application.isPlaying && revealAnimationDuration > 0f)
            {
                StartCoroutine(AnimateRevealAndHide(fogInstance));
            }
            else
            {
                fogInstance.SetActive(false);
            }
        }
    }

    private void ApplyRandomRotation(Transform fogTransform)
    {
        if (!randomizeFogRotation || fogTransform == null)
        {
            return;
        }

        Vector3 currentEuler = fogTransform.eulerAngles;
        float randomYaw = Random.Range(fogYawRange.x, fogYawRange.y);
        fogTransform.rotation = Quaternion.Euler(currentEuler.x, randomYaw, currentEuler.z);
    }

    private IEnumerator AnimateRevealAndHide(GameObject fogInstance)
    {
        if (fogInstance == null)
        {
            yield break;
        }

        Transform fogTransform = fogInstance.transform;
        Vector3 initialScale = fogTransform.localScale;
        float elapsed = 0f;

        while (elapsed < revealAnimationDuration)
        {
            if (fogInstance == null)
            {
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / revealAnimationDuration);
            float scale = revealScaleCurve.Evaluate(t);
            fogTransform.localScale = initialScale * scale;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (fogInstance != null)
        {
            fogTransform.localScale = initialScale;
            fogInstance.SetActive(false);
        }
    }

    private static bool IsSettlementTile(GridTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        if (tile.tileType == TileType.City || tile.tileType == TileType.Village)
        {
            return true;
        }

        string tileName = (tile.tileName ?? string.Empty).Trim();
        return string.Equals(tileName, "City", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tileName, "Village", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ClearFogInstances()
    {
        if (hideUnrevealedTileVisuals)
        {
            SetAllTileVisualsVisible(true);
        }

        if (fogInstances == null)
        {
            IsInitialized = false;
            return;
        }

        for (int x = 0; x < fogInstances.GetLength(0); x++)
        {
            for (int y = 0; y < fogInstances.GetLength(1); y++)
            {
                GameObject fogInstance = fogInstances[x, y];
                if (fogInstance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(fogInstance);
                }
                else
                {
                    DestroyImmediate(fogInstance);
                }
            }
        }

        IsInitialized = false;
    }

    private void SetAllTileVisualsVisible(bool visible)
    {
        if (gridMap == null || gridMap.tileMap == null)
        {
            return;
        }

        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                SetTileVisualsVisible(new Vector2Int(x, y), visible);
            }
        }
    }

    private void SetTileVisualsVisible(Vector2Int coordinate, bool visible)
    {
        if (gridMap == null || !gridMap.IsInsideGrid(coordinate))
        {
            return;
        }

        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tileInstance == null)
        {
            return;
        }

        Renderer[] renderers = tileInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}
