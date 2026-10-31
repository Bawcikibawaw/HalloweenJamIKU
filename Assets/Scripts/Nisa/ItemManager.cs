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

    [SerializeField] private List<ItemSpawnEntry> spawnEntries = new();

    private readonly Dictionary<Item, Vector3> activeItems = new();
    private LevelManager levelManager;
    private Transform playerStartPoint;
    private Transform deliveryPoint;

    public IReadOnlyDictionary<Item, Vector3> ActiveItems => activeItems;

    public event Action OnItemsChanged;
    public event Action<Item, Vector3> OnItemPositionChanged;

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
        levelManager = FindFirstObjectByType<LevelManager>();
        playerStartPoint = GameObject.Find("PlayerStartPoint")?.transform ?? GameObject.Find("Player")?.transform;
        deliveryPoint = GameObject.Find("DeliveryPoint")?.transform;

        if (levelManager != null)
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

    private void HandleLevelChanged(int _)
    {
        ClearItems();
        SpawnLevelItems();
    }

    public void SpawnLevelItems()
    {
        PathCreator map = PathCreator.Active;
        if (map == null || playerStartPoint == null) return;

        List<Vector3Int> cells = GetReachableCells(map);
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }

        int index = 0;
        foreach (ItemSpawnEntry entry in spawnEntries)
        {
            if (entry.itemData == null || entry.itemData.itemPrefab == null) continue;

            for (int i = 0; i < entry.amount && index < cells.Count; i++, index++)
            {
                Vector3 pos = map.Cells[cells[index]].center;
                GameObject obj = Instantiate(entry.itemData.itemPrefab, pos, Quaternion.identity, map.transform);
                if (!obj.TryGetComponent(out Item item))
                {
                    Destroy(obj);
                    continue;
                }

                item.Initialize(entry.itemData);
                if (entry.itemData.itemSprite != null && obj.TryGetComponent(out SpriteRenderer sr))
                    sr.sprite = entry.itemData.itemSprite;

                activeItems[item] = pos;
            }
        }

        OnItemsChanged?.Invoke();
    }

    private List<Vector3Int> GetReachableCells(PathCreator map)
    {
        Vector3Int startCell = map.WorldToCell(playerStartPoint.position);
        Vector3Int deliveryCell = deliveryPoint != null ? map.WorldToCell(deliveryPoint.position) : startCell;
        List<Vector3Int> result = new();

        if (!map.IsWalkable(startCell)) return result;

        Queue<Vector3Int> queue = new();
        HashSet<Vector3Int> visited = new() { startCell };
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            foreach (Vector3Int dir in Sides.Dir)
            {
                Vector3Int next = current + dir;
                if (map.CanMove(current, next) && visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        foreach (Vector3Int cell in visited)
        {
            if (cell != startCell && cell != deliveryCell)
                result.Add(cell);
        }

        return result;
    }

    public void UnregisterItem(Item item)
    {
        if (activeItems.Remove(item))
            OnItemsChanged?.Invoke();
    }

    public bool TryMoveItem(Item item, Vector3Int targetCell)
    {
        PathCreator map = PathCreator.Active;
        if (item == null || item.IsCollected || map == null || !activeItems.ContainsKey(item) || !GetReachableCells(map).Contains(targetCell))
            return false;

        foreach (KeyValuePair<Item, Vector3> kv in activeItems)
        {
            if (kv.Key != item && map.WorldToCell(kv.Value) == targetCell)
                return false;
        }

        Vector3 newPos = map.CellToWorldCenter(targetCell);
        item.transform.position = newPos;
        activeItems[item] = newPos;
        OnItemPositionChanged?.Invoke(item, newPos);
        return true;
    }

    private void ClearItems()
    {
        foreach (Item item in activeItems.Keys)
        {
            if (item != null) Destroy(item.gameObject);
        }
        activeItems.Clear();
        OnItemsChanged?.Invoke();
    }
}
