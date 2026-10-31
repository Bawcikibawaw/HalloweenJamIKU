using System;
using System.Collections.Generic;
using UnityEngine;
using aTES;

[Serializable]
public class ItemSpawnEntry
{
    public ItemData itemData;
    [Min(0)] public int amount;
}

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private Transform playerStartPoint;
    [SerializeField] private Transform deliveryPoint;

    [Header("Spawn Settings")]
    [SerializeField] private List<ItemSpawnEntry> spawnEntries = new();

    private readonly Dictionary<Item, Vector3> activeItems = new();

    public IReadOnlyDictionary<Item, Vector3> ActiveItems => activeItems;

    public event Action OnItemsChanged;
    public event Action<Item, Vector3> OnItemPositionChanged;

    private PathCreator pathCreator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (levelManager == null)
        {
            Debug.LogError("ItemManager: LevelManager reference is missing.", this);
            return;
        }

        levelManager.OnLevelChanged += HandleLevelChanged;

        SpawnLevelItems();
    }

    private void OnDestroy()
    {
        if (levelManager != null)
            levelManager.OnLevelChanged -= HandleLevelChanged;

        if (Instance == this)
            Instance = null;
    }

    private void HandleLevelChanged(int levelIndex)
    {
        ClearItems();
        SpawnLevelItems();
    }

    public void SpawnLevelItems()
    {
        pathCreator = PathCreator.Active;

        if (pathCreator == null)
        {
            Debug.LogError("ItemManager: Active PathCreator not found.", this);
            return;
        }

        List<Vector3Int> availableCells = GetAvailableCells();
        ShuffleCells(availableCells);

        int cellIndex = 0;

        foreach (ItemSpawnEntry entry in spawnEntries)
        {
            if (entry.itemData == null || entry.itemData.itemPrefab == null)
            {
                Debug.LogWarning("ItemManager: Invalid ItemData or prefab.", this);
                continue;
            }

            for (int i = 0; i < entry.amount; i++)
            {
                if (cellIndex >= availableCells.Count)
                {
                    Debug.LogWarning("ItemManager: Not enough available grid cells.", this);
                    OnItemsChanged?.Invoke();
                    return;
                }

                Vector3Int cell = availableCells[cellIndex];
                GridCell gridCell = pathCreator.Cells[cell];

                SpawnItem(entry.itemData, gridCell.center, pathCreator.transform);
                cellIndex++;
            }
        }

        OnItemsChanged?.Invoke();
    }

    private List<Vector3Int> GetAvailableCells()
    {
        List<Vector3Int> availableCells = new();

        if (playerStartPoint == null)
        {
            Debug.LogWarning("ItemManager: Player start point is missing.");
            return availableCells;
        }

        Vector3Int startCell = pathCreator.WorldToCell(playerStartPoint.position);

        if (!pathCreator.IsWalkable(startCell))
        {
            Debug.LogError("ItemManager: Player start cell is not walkable.");
            return availableCells;
        }

        Vector3Int deliveryCell = deliveryPoint != null
            ? pathCreator.WorldToCell(deliveryPoint.position)
            : default;

        Vector3Int[] directions =
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        Queue<Vector3Int> queue = new();
        HashSet<Vector3Int> visited = new();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();

            foreach (Vector3Int direction in directions)
            {
                Vector3Int neighbor = current + direction;

                if (visited.Contains(neighbor))
                    continue;

                if (!pathCreator.CanMove(current, neighbor))
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        foreach (Vector3Int cell in visited)
        {
            if (cell == startCell || cell == deliveryCell)
                continue;

            availableCells.Add(cell);
        }

        return availableCells;
    }

    private void ShuffleCells(List<Vector3Int> cells)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (cells[i], cells[randomIndex]) = (cells[randomIndex], cells[i]);
        }
    }

    private void SpawnItem(ItemData data, Vector3 position, Transform parent)
    {
        GameObject itemObject = Instantiate(
            data.itemPrefab,
            position,
            Quaternion.identity,
            parent
        );

        Item item = itemObject.GetComponent<Item>();

        if (item == null)
        {
            Debug.LogError("Item prefab is missing Item component.", itemObject);
            Destroy(itemObject);
            return;
        }

        item.Initialize(data);

        if (data.itemSprite != null &&
            itemObject.TryGetComponent<SpriteRenderer>(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.sprite = data.itemSprite;
        }

        activeItems.Add(item, position);
    }

    public void UnregisterItem(Item item)
    {
        if (activeItems.Remove(item))
            OnItemsChanged?.Invoke();
    }

    public bool TryMoveItem(Item item, Vector3Int targetCell)
    {
        if (item == null || item.IsCollected || pathCreator == null)
            return false;

        if (!activeItems.ContainsKey(item))
            return false;

        if (!pathCreator.IsWalkable(targetCell))
            return false;

        foreach (KeyValuePair<Item, Vector3> entry in activeItems)
        {
            if (entry.Key == item)
                continue;

            if (pathCreator.WorldToCell(entry.Value) == targetCell)
                return false;
        }

        Vector3 newPosition = pathCreator.CellToWorldCenter(targetCell);

        item.transform.position = newPosition;
        activeItems[item] = newPosition;

        OnItemPositionChanged?.Invoke(item, newPosition);

        return true;
    }

    private void ClearItems()
    {
        foreach (Item item in activeItems.Keys)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        activeItems.Clear();
        OnItemsChanged?.Invoke();
    }
}