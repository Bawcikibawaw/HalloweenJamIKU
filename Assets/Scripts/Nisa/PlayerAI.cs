using System.Collections.Generic;
using UnityEngine;
using aTES;

[RequireComponent(typeof(PlayerStats))]
public class PlayerAI : MonoBehaviour
{
    private enum AIState { FindingItem, MovingToItem, MovingToDeliveryPoint, Finished }

    [SerializeField] private float arrivalDistance = 0.05f;

    private AIState currentState = AIState.FindingItem;
    private PlayerStats stats;
    private LevelManager levelManager;
    private Transform handPoint;
    private Transform deliveryPoint;
    private Transform startPoint;
    private Item currentTarget;

    private List<Vector3> currentPath = new();
    private int currentWaypoint;

    /// <summary>The floor item the chef is walking to (null if none or already carrying).</summary>
    public Item CurrentTarget => currentState == AIState.MovingToItem ? currentTarget : null;
    public bool IsCarrying => currentState == AIState.MovingToDeliveryPoint && currentTarget != null;
    public Vector3 StartPosition => startPoint != null ? startPoint.position : transform.position;
    public Vector3 DeliveryPosition => deliveryPoint != null ? deliveryPoint.position : transform.position;
    public Vector3? NextStep => currentWaypoint < currentPath.Count ? currentPath[currentWaypoint] : null;

    /// <summary>Returns the next cell on the path that the chef has not entered yet (i.e. not the cell he currently stands in).</summary>
    public Vector3Int? GetStepAheadCell(PathCreator map)
    {
        if (map == null || currentPath == null) return null;
        Vector3Int playerCell = map.WorldToCell(transform.position);

        for (int i = currentWaypoint; i < currentPath.Count; i++)
        {
            Vector3Int cell = map.WorldToCell(currentPath[i]);
            if (cell != playerCell && map.IsWalkable(cell))
                return cell;
        }

        return null;
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        handPoint = transform.Find("HandPoint") ?? transform;
    }

    private void Start()
    {
        deliveryPoint = GameObject.Find("DeliveryPoint")?.transform;
        startPoint = GameObject.Find("PlayerStartPoint")?.transform;
        levelManager = FindFirstObjectByType<LevelManager>();

        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.OnItemPositionChanged += HandleItemPositionChanged;
            ItemManager.Instance.OnItemsChanged += HandleItemsChanged;
        }

        if (levelManager != null)
            levelManager.OnLevelChanged += HandleLevelChanged;

        FindNextItem();
    }

    private void OnDestroy()
    {
        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.OnItemPositionChanged -= HandleItemPositionChanged;
            ItemManager.Instance.OnItemsChanged -= HandleItemsChanged;
        }

        if (levelManager != null)
            levelManager.OnLevelChanged -= HandleLevelChanged;
    }

    private void HandleItemsChanged()
    {
        if (currentState == AIState.Finished && ItemManager.Instance != null && ItemManager.Instance.ActiveItems.Count > 0)
            currentState = AIState.FindingItem;
    }

    private void HandleLevelChanged(int _)
    {
        if (currentTarget != null) Destroy(currentTarget.gameObject);
        if (startPoint != null) transform.position = startPoint.position;

        currentTarget = null;
        stats.ClearDebuffs();
        currentPath.Clear();
        currentWaypoint = 0;
        currentState = AIState.FindingItem;
    }

    private void Update()
    {
        if (stats.IsStunned) return;

        switch (currentState)
        {
            case AIState.FindingItem: FindNextItem(); break;
            case AIState.MovingToItem: MoveToItem(); break;
            case AIState.MovingToDeliveryPoint: MoveToDeliveryPoint(); break;
            case AIState.Finished:
                if (ItemManager.Instance != null && ItemManager.Instance.ActiveItems.Count > 0)
                    FindNextItem();
                break;
        }
    }

    private void FindNextItem()
    {
        PathCreator map = PathCreator.Active;
        if (map == null || ItemManager.Instance == null || deliveryPoint == null) return;

        Vector3Int startCell = map.WorldToCell(transform.position);
        Vector3Int deliveryCell = map.WorldToCell(deliveryPoint.position);

        Item bestItem = null;
        List<Vector3> bestPath = null;
        int shortest = int.MaxValue;

        foreach (KeyValuePair<Item, Vector3> entry in ItemManager.Instance.ActiveItems)
        {
            if (entry.Key == null || entry.Key.IsCollected || entry.Key.IsBeingDragged) continue;

            Vector3Int itemCell = map.WorldToCell(entry.Value);
            List<Vector3> toItem = FindPath(map, startCell, itemCell);
            List<Vector3> toDelivery = toItem != null ? FindPath(map, itemCell, deliveryCell) : null;

            if (toDelivery != null && toItem.Count + toDelivery.Count < shortest)
            {
                shortest = toItem.Count + toDelivery.Count;
                bestItem = entry.Key;
                bestPath = toItem;
            }
        }

        currentTarget = bestItem;
        currentPath = bestPath ?? new List<Vector3>();
        currentWaypoint = 0;
        currentState = bestItem != null ? AIState.MovingToItem : AIState.Finished;
    }

    private void MoveToItem()
    {
        if (currentTarget == null || currentTarget.IsBeingDragged)
        {
            currentState = AIState.FindingItem;
            return;
        }

        if (!MoveAlongPath()) return;

        PathCreator map = PathCreator.Active;
        currentTarget.PickUp(handPoint);
        currentPath = FindPath(map, map.WorldToCell(transform.position), map.WorldToCell(deliveryPoint.position)) ?? new List<Vector3>();
        currentWaypoint = 0;
        currentState = AIState.MovingToDeliveryPoint;
    }

    private void MoveToDeliveryPoint()
    {
        if (!MoveAlongPath()) return;

        currentTarget.Deliver();
        currentTarget = null;
        currentPath.Clear();
        currentWaypoint = 0;

        if (ItemManager.Instance != null && ItemManager.Instance.ActiveItems.Count == 0)
        {
            currentState = AIState.Finished;
            FindFirstObjectByType<GameTimer>()?.FailLevel();
            return;
        }

        currentState = AIState.FindingItem;
    }

    private bool MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0) return false;
        if (currentWaypoint >= currentPath.Count) return true;

        Vector3 target = currentPath[currentWaypoint];
        float speed = stats.GetMoveSpeed(currentState == AIState.MovingToDeliveryPoint);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= arrivalDistance)
        {
            transform.position = target;
            currentWaypoint++;
        }

        return currentWaypoint >= currentPath.Count;
    }

    /// <summary>Instantly moves the chef to a position (use a cell center) and recalculates his path.</summary>
    public void Teleport(Vector3 position)
    {
        transform.position = position;
        Replan();
    }

    /// <summary>The chef drops the item he carries at a position and goes looking for an item again.</summary>
    public void DropCarried(Vector3 position)
    {
        if (!IsCarrying) return;

        currentTarget.Drop(position);
        currentTarget = null;
        Replan();
    }

    /// <summary>Forces the chef to abandon his current target and walk toward the farthest ingredient on the map.</summary>
    public bool TargetFarthestItem()
    {
        PathCreator map = PathCreator.Active;
        if (IsCarrying || map == null || ItemManager.Instance == null) return false;

        Vector3Int startCell = map.WorldToCell(transform.position);
        Item farthestItem = null;
        List<Vector3> farthestPath = null;
        int longest = -1;

        foreach (KeyValuePair<Item, Vector3> entry in ItemManager.Instance.ActiveItems)
        {
            if (entry.Key == null || entry.Key.IsCollected || entry.Key.IsBeingDragged) continue;

            List<Vector3> toItem = FindPath(map, startCell, map.WorldToCell(entry.Value));
            if (toItem != null && toItem.Count > longest)
            {
                longest = toItem.Count;
                farthestItem = entry.Key;
                farthestPath = toItem;
            }
        }

        if (farthestItem == null) return false;

        currentTarget = farthestItem;
        currentPath = farthestPath;
        currentWaypoint = 0;
        currentState = AIState.MovingToItem;
        return true;
    }

    public void Replan()
    {
        currentWaypoint = 0;
        PathCreator map = PathCreator.Active;
        if (map != null)
        {
            Vector3Int currentCell = map.WorldToCell(transform.position);
            if (!map.IsWalkable(currentCell))
            {
                // If chef is somehow inside an unwalkable cell, push him to the closest walkable neighbor
                Vector3Int safeCell = currentCell;
                float bestDist = float.MaxValue;
                foreach (Vector3Int dir in Sides.Dir)
                {
                    Vector3Int n = currentCell + dir;
                    if (map.IsWalkable(n))
                    {
                        float d = (n - currentCell).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; safeCell = n; }
                    }
                }
                if (bestDist < float.MaxValue)
                    transform.position = map.CellToWorldCenter(safeCell);
            }
        }

        if (!IsCarrying)
        {
            currentTarget = null;
            currentPath.Clear();
            currentState = AIState.FindingItem;
            return;
        }

        if (map != null && deliveryPoint != null)
        {
            currentPath = FindPath(map, map.WorldToCell(transform.position), map.WorldToCell(deliveryPoint.position)) ?? new List<Vector3>();
        }
        else
        {
            currentPath.Clear();
        }
    }

    private void HandleItemPositionChanged(Item item, Vector3 newPos)
    {
        if (currentState != AIState.MovingToItem || item != currentTarget || PathCreator.Active == null) return;

        PathCreator map = PathCreator.Active;
        List<Vector3> newPath = FindPath(map, map.WorldToCell(transform.position), map.WorldToCell(newPos));
        currentPath = newPath ?? new List<Vector3>();
        currentWaypoint = 0;

        if (newPath == null)
        {
            currentTarget = null;
            currentState = AIState.FindingItem;
        }
    }

    private static List<Vector3> FindPath(PathCreator map, Vector3Int start, Vector3Int target)
    {
        if (map == null || !map.IsWalkable(target)) return null;

        if (!map.IsWalkable(start))
        {
            // Find closest walkable neighbor to start
            Vector3Int safeStart = start;
            float bestDist = float.MaxValue;
            foreach (Vector3Int dir in Sides.Dir)
            {
                Vector3Int n = start + dir;
                if (map.IsWalkable(n))
                {
                    float d = (n - start).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; safeStart = n; }
                }
            }
            if (bestDist == float.MaxValue) return null;
            start = safeStart;
        }

        if (start == target) return new List<Vector3>();

        List<Vector3Int> open = new() { start };
        HashSet<Vector3Int> closed = new();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new();
        Dictionary<Vector3Int, int> gCost = new() { [start] = 0 };

        while (open.Count > 0)
        {
            Vector3Int current = open[0];
            for (int i = 1; i < open.Count; i++)
            {
                if (F(open[i]) < F(current)) current = open[i];
            }

            if (current == target)
            {
                List<Vector3> path = new();
                for (Vector3Int c = current; cameFrom.TryGetValue(c, out Vector3Int prev); c = prev)
                    path.Add(map.CellToWorldCenter(c));
                path.Reverse();
                return path;
            }

            open.Remove(current);
            closed.Add(current);

            foreach (Vector3Int dir in Sides.Dir)
            {
                Vector3Int next = current + dir;
                if (closed.Contains(next) || !map.CanMove(current, next)) continue;

                int tentativeG = gCost[current] + 1;
                if (!gCost.TryGetValue(next, out int oldG) || tentativeG < oldG)
                {
                    cameFrom[next] = current;
                    gCost[next] = tentativeG;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
        }

        return null;

        int F(Vector3Int c) => gCost[c] + Mathf.Abs(c.x - target.x) + Mathf.Abs(c.y - target.y);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 prev = transform.position;
        for (int i = currentWaypoint; i < currentPath.Count; i++)
        {
            Gizmos.DrawLine(prev, currentPath[i]);
            Gizmos.DrawSphere(currentPath[i], 0.08f);
            prev = currentPath[i];
        }
    }
}
