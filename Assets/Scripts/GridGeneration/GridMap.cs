using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridGeneration
{
    public class GridMap : MonoBehaviour
    {
        [SerializeField] private int width, height;
        [SerializeField] private float spacing, tileSize;
        [SerializeField] private GridTile[] tiles;
        [SerializeField] private int seed;
        private GridTile[,] tileMap;
        private GameObject[,] gameObjectMap;
        private bool[,] protectedTileMap;
        private const int maxVillages = 4, minVillages = 1;

        // Cache the inspected values so OnValidate regenerates only on relevant changes.
        private int _lastConfigHash;

#if UNITY_EDITOR
        private bool _isEditorRegenQueued;
#endif

        private void Start()
        {
            GenerateLandMap();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            int configHash = GetConfigHash();
            if (configHash == _lastConfigHash)
            {
                return;
            }

            _lastConfigHash = configHash;

#if UNITY_EDITOR
            QueueEditorRegeneration();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRegeneration()
        {
            if (_isEditorRegenQueued)
            {
                return;
            }

            _isEditorRegenQueued = true;
            EditorApplication.delayCall += RegenerateInEditor;
        }

        private void RegenerateInEditor()
        {
            EditorApplication.delayCall -= RegenerateInEditor;
            _isEditorRegenQueued = false;

            if (this == null || Application.isPlaying)
            {
                return;
            }

            GenerateLandMap();
        }
#endif

        private void GenerateLandMap()
        {
            if (tiles == null || tiles.Length == 0 || tiles[0] == null)
            {
                Debug.LogError("GridMap: Missing base tile in the tiles array.", this);
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                {
                    Debug.LogError($"GridMap: Tile at index {i} is not assigned.", this);
                    return;
                }

                if (tiles[i].tilePrefab == null)
                {
                    Debug.LogError($"GridMap: Tile at index {i} does not have a prefab assigned.", this);
                    return;
                }
            }

            if (width <= 0 || height <= 0)
            {
                Debug.LogError("GridMap: Width and height must be greater than 0.", this);
                return;
            }

            if (tileSize <= 0)
            {
                Debug.LogError("GridMap: Tile size must be greater than 0.", this);
                return;
            }

            ClearGeneratedTiles();

            tileMap = new GridTile[width, height];
            gameObjectMap = new GameObject[width, height];
            protectedTileMap = new bool[width, height];
            

            float step = tileSize + spacing;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var noiseValue = Mathf.PerlinNoise((x + seed) * 0.1f, (y + seed) * 0.1f);
                    if(noiseValue > 0.5f)
                        tileMap[x, y] = tiles[0];
                    else
                    {
                        tileMap[x, y] = tiles[1];
                    }
                    Vector3 localPos = new Vector3(x * step, 0f, y * step);
                    GameObject tileInstance = Instantiate(tileMap[x, y].tilePrefab, transform);
                    tileInstance.transform.localPosition = localPos;
                    tileInstance.name = $"Tile_{x}_{y}";
                    gameObjectMap[x, y] = tileInstance;
                }
            }
            var rng = new System.Random(seed);
            GenerateVillageMap(rng, true);
            var villageCount = rng.Next(minVillages, maxVillages + 1);
            for (int villageIndex = 0; villageIndex < villageCount; ++villageIndex)
            {
                GenerateVillageMap(rng, false);
            }
        }

        private void GenerateVillageMap(System.Random rng, bool placeCity)
        {
            if (rng == null || tileMap == null || gameObjectMap == null || protectedTileMap == null || width <= 0 || height <= 0)
            {
                return;
            }

            GridTile protectedTile = null;
            var tileType = placeCity ? TileType.City : TileType.Village;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].tileType == tileType)
                {
                    protectedTile = tiles[i];
                    break;
                }
            }

            if (protectedTile == null)
            {
                Debug.LogWarning($"GridMap: No tile with type {tileType} found. Skipping placement.", this);
                return;
            }

            if (!TryGetRandomUnprotectedCoordinate(rng, out int randomX, out int randomY))
            {
                Debug.LogWarning("GridMap: No available tile left for city/village placement.", this);
                return;
            }
            
            // Remove the previously spawned object at this coordinate before replacing it.
            GameObject oldTileInstance = gameObjectMap[randomX, randomY];
            if (oldTileInstance != null)
            {
                if (Application.isPlaying) Destroy(oldTileInstance);
                else DestroyImmediate(oldTileInstance);
            }
            
            tileMap[randomX, randomY] = protectedTile;
            


            //Place city tile prefab
            Vector3 localPos = new Vector3(randomX * (tileSize + spacing), 0f, randomY * (tileSize + spacing));
            GameObject cityInstance = Instantiate(protectedTile.tilePrefab, transform);
            cityInstance.transform.localPosition = localPos;
            cityInstance.name = $"Tile_{randomX}_{randomY}";
            gameObjectMap[randomX, randomY] = cityInstance;
            protectedTileMap[randomX, randomY] = true;
            //Debug.Log("GridMap: City/Village placement complete.", this);
        }

        private bool TryGetRandomUnprotectedCoordinate(System.Random rng, out int x, out int y)
        {
            int attempts = width * height * 2;
            for (int i = 0; i < attempts; i++)
            {
                int candidateX = rng.Next(0, width);
                int candidateY = rng.Next(0, height);
                if (!protectedTileMap[candidateX, candidateY])
                {
                    x = candidateX;
                    y = candidateY;
                    return true;
                }
            }

            for (int ix = 0; ix < width; ix++)
            {
                for (int iy = 0; iy < height; iy++)
                {
                    if (!protectedTileMap[ix, iy])
                    {
                        x = ix;
                        y = iy;
                        return true;
                    }
                }
            }

            x = -1;
            y = -1;
            return false;
        }

        private int GetConfigHash()
        {
            int baseTileId = 0;
            int basePrefabId = 0;

            if (tiles != null && tiles.Length > 0 && tiles[0] != null)
            {
                baseTileId = tiles[0].GetEntityId().GetHashCode();
                if (tiles[0].tilePrefab != null)
                {
                    basePrefabId = tiles[0].tilePrefab.GetEntityId().GetHashCode();
                }
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + width;
                hash = hash * 31 + height;
                hash = hash * 31 + spacing.GetHashCode();
                hash = hash * 31 + tileSize.GetHashCode();
                hash = hash * 31 + seed;
                hash = hash * 31 + baseTileId;
                hash = hash * 31 + basePrefabId;
                return hash;
            }
        }

        private void ClearGeneratedTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
#if UNITY_EDITOR
                else
                {
                    DestroyImmediate(child);
                }
#endif
            }

            gameObjectMap = null;
            tileMap = null;
            protectedTileMap = null;
        }
    }
}
