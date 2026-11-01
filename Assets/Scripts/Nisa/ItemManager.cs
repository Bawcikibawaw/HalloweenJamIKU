using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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

    [SerializeField] private int itemsPerLevel = 5;
    [SerializeField] private List<ItemSpawnEntry> spawnEntries = new();

    private readonly Dictionary<Item, Vector3> activeItems = new();
    private readonly List<Image> ingredientSlots = new();
    private readonly List<ItemData> gatheredItems = new();
    private int gatheredCount;
    private LevelManager levelManager;
    private Transform playerStartPoint;
    private Transform deliveryPoint;

    public IReadOnlyDictionary<Item, Vector3> ActiveItems => activeItems;
    public ItemData RandomItemData => spawnEntries.Count > 0 ? spawnEntries[UnityEngine.Random.Range(0, spawnEntries.Count)].itemData : null;

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

        GameObject panel = GameObject.Find("IngredientsPanel");
        if (panel != null)
        {
            for (int i = 0; i < panel.transform.childCount; i++)
            {
                Transform icon = panel.transform.GetChild(i).Find("Icon");
                if (icon != null && icon.TryGetComponent(out Image img))
                    ingredientSlots.Add(img);
            }
        }

        if (levelManager != null)
            levelManager.OnLevelChanged += HandleLevelChanged;

        ResetIngredientSlots();
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
        ResetIngredientSlots();
        SpawnLevelItems();
    }

    public void RecordGatheredIngredient(ItemData data)
    {
        if (data == null) return;
        gatheredItems.Add(data);
        if (gatheredCount >= ingredientSlots.Count) return;

        Image slot = ingredientSlots[gatheredCount++];
        Sprite icon = data.gatheredSprite != null ? data.gatheredSprite : data.itemSprite;
        if (icon != null) slot.sprite = icon;
        slot.color = Color.white;
        slot.enabled = true;
    }

    /// <summary>Steals one already-gathered ingredient from the UI box and spawns it back on the map at <paramref name="cell"/>.</summary>
    public bool RespawnGatheredIngredient(Vector3Int cell)
    {
        if (gatheredItems.Count == 0) return false;

        ItemData data = gatheredItems[^1];
        gatheredItems.RemoveAt(gatheredItems.Count - 1);

        if (gatheredCount > 0)
        {
            gatheredCount--;
            if (gatheredCount < ingredientSlots.Count && ingredientSlots[gatheredCount] != null)
                ingredientSlots[gatheredCount].enabled = false;
        }

        return SpawnItem(data, cell) != null;
    }

    private void ResetIngredientSlots()
    {
        gatheredCount = 0;
        gatheredItems.Clear();
        foreach (Image slot in ingredientSlots)
        {
            if (slot != null) slot.enabled = false;
        }
    }

    public void SpawnLevelItems()
    {
        PathCreator map = PathCreator.Active;
        if (map == null || playerStartPoint == null) return;

        List<Vector3Int> cells = GetFreeCells(map.WorldToCell(playerStartPoint.position));
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }

        List<ItemData> pool = new();
        foreach (ItemSpawnEntry entry in spawnEntries)
        {
            if (entry.itemData == null) continue;
            for (int i = 0; i < entry.amount; i++) pool.Add(entry.itemData);
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Min(itemsPerLevel, Mathf.Min(pool.Count, cells.Count));
        for (int i = 0; i < count; i++)
        {
            SpawnItem(pool[i], cells[i]);
        }
    }

    /// <summary>Spawns one item on a cell and registers it. Returns null if the data/prefab is invalid.</summary>
    public Item SpawnItem(ItemData data, Vector3Int cell)
    {
        PathCreator map = PathCreator.Active;
        if (map == null || data == null || data.itemPrefab == null) return null;

        Vector3 pos = map.CellToWorldCenter(cell);
        GameObject obj = Instantiate(data.itemPrefab, pos, Quaternion.identity, map.transform);
        if (!obj.TryGetComponent(out Item item))
        {
            Destroy(obj);
            return null;
        }

        item.Initialize(data);
        if (data.itemSprite != null && obj.TryGetComponent(out SpriteRenderer sr))
        {
            sr.sprite = data.itemSprite;
            sr.color = Color.white;
        }

        RegisterItem(item, pos);
        return item;
    }

    /// <summary>Maze (walking) distance from <paramref name="start"/> to every cell reachable from it.</summary>
    public Dictionary<Vector3Int, int> FloodFill(Vector3Int start)
    {
        PathCreator map = PathCreator.Active;
        Dictionary<Vector3Int, int> dist = new();
        if (map == null || !map.IsWalkable(start)) return dist;

        Queue<Vector3Int> queue = new();
        dist[start] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            foreach (Vector3Int dir in Sides.Dir)
            {
                Vector3Int next = current + dir;
                if (map.CanMove(current, next) && dist.TryAdd(next, dist[current] + 1))
                    queue.Enqueue(next);
            }
        }

        return dist;
    }

    /// <summary>
    /// Cells the player can reach that are empty (no item, not start/delivery, not <paramref name="from"/>),
    /// sorted farthest-first by walking distance from <paramref name="from"/>.
    /// </summary>
    public List<Vector3Int> GetFreeCells(Vector3Int from)
    {
        PathCreator map = PathCreator.Active;
        if (map == null || playerStartPoint == null) return new List<Vector3Int>();

        Vector3Int startCell = map.WorldToCell(playerStartPoint.position);
        HashSet<Vector3Int> blocked = new() { from, startCell };
        if (deliveryPoint != null) blocked.Add(map.WorldToCell(deliveryPoint.position));
        foreach (Vector3 pos in activeItems.Values) blocked.Add(map.WorldToCell(pos));

        Dictionary<Vector3Int, int> dist = FloodFill(from);
        return FloodFill(startCell).Keys
            .Where(c => !blocked.Contains(c))
            .OrderByDescending(c => dist.TryGetValue(c, out int d) ? d : 0)
            .ToList();
    }

    public void RegisterItem(Item item, Vector3 position)
    {
        activeItems[item] = position;
        OnItemsChanged?.Invoke();
    }

    public void UnregisterItem(Item item)
    {
        if (activeItems.Remove(item))
            OnItemsChanged?.Invoke();
    }

    public bool CanPlaceItemAt(Item item, Vector3Int targetCell)
    {
        PathCreator map = PathCreator.Active;
        if (item == null || item.IsCollected || map == null || playerStartPoint == null || !activeItems.ContainsKey(item))
            return false;

        Vector3Int startCell = map.WorldToCell(playerStartPoint.position);
        Vector3Int deliveryCell = deliveryPoint != null ? map.WorldToCell(deliveryPoint.position) : startCell;
        if (targetCell == startCell || targetCell == deliveryCell || !FloodFill(startCell).ContainsKey(targetCell))
            return false;

        foreach (KeyValuePair<Item, Vector3> kv in activeItems)
        {
            if (kv.Key != item && map.WorldToCell(kv.Value) == targetCell)
                return false;
        }
        return true;
    }

    public bool TryMoveItem(Item item, Vector3Int targetCell)
    {
        if (!CanPlaceItemAt(item, targetCell))
            return false;

        Vector3 newPos = PathCreator.Active.CellToWorldCenter(targetCell);
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
