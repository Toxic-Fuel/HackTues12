using System.Collections;
using GridGeneration;
using UnityEngine;

public class CamerabjectAlignment : MonoBehaviour
{
    [SerializeField] private GridMap gridMap;
    [SerializeField] private bool keepCurrentY = true;
    [SerializeField] private Vector3 offset = Vector3.zero;

    private IEnumerator Start()
    {
        if (gridMap == null)
        {
            gridMap = FindObjectOfType<GridMap>();
        }

        if (gridMap == null)
        {
            Debug.LogError("CamerabjectAlignment: GridMap reference is missing.", this);
            yield break;
        }

        yield return new WaitUntil(() => gridMap.tileMap != null);

        if (gridMap.Width <= 0 || gridMap.Height <= 0)
        {
            Debug.LogError("CamerabjectAlignment: GridMap dimensions are invalid.", this);
            yield break;
        }

        GameObject firstTile = gridMap.GetTileInstanceAt(0, 0);
        GameObject lastTile = gridMap.GetTileInstanceAt(gridMap.Width - 1, gridMap.Height - 1);

        if (firstTile == null || lastTile == null)
        {
            Debug.LogError("CamerabjectAlignment: Could not resolve tile instances to compute center.", this);
            yield break;
        }

        Vector3 center = (firstTile.transform.position + lastTile.transform.position) * 0.5f;
        Vector3 targetPosition = center + offset;

        if (keepCurrentY)
        {
            targetPosition.y = transform.position.y;
        }

        transform.position = targetPosition;
    }
}
