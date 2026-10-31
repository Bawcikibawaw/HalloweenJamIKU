using System.Collections.Generic;
using UnityEngine;
using aTES;

public class PlayerAI : MonoBehaviour
{
    private enum AIState
    {
        FindingItem,
        MovingToItem,
        MovingToDeliveryPoint,
        Finished
    }

    [Header("References")]
    [SerializeField] private Transform handPoint;
    [SerializeField] private Transform deliveryPoint;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float arrivalDistance = 0.05f;

    private AIState currentState = AIState.FindingItem;

    private PathCreator pathCreator;
    private LevelManager levelManager;
    private Transform startPoint;
    private Item currentTarget;

    private List<Vector3> currentPath = new();
    private int currentWaypoint;

    private void Start()
    {
        pathCreator = PathCreator.Active;

        if (handPoint == null)
            handPoint = transform.Find("HandPoint");

        if (deliveryPoint == null)
            deliveryPoint = GameObject.Find("DeliveryPoint")?.transform;

        startPoint = GameObject.Find("PlayerStartPoint")?.transform;
        levelManager = FindFirstObjectByType<LevelManager>();

        if (handPoint == null || deliveryPoint == null)
        {
            Debug.LogError("PlayerAI: Hand Point or Delivery Point is missing.", this);
            enabled = false;
            return;
        }

        if (ItemManager.Instance == null)
        {
            Debug.LogError("PlayerAI: ItemManager instance not found.", this);
            enabled = false;
            return;
        }

        ItemManager.Instance.OnItemPositionChanged += HandleItemPositionChanged;
        ItemManager.Instance.OnItemsChanged += HandleItemsChanged;

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
        {
            currentState = AIState.FindingItem;
        }
    }

    private void HandleLevelChanged(int levelIndex)
    {
        if (currentTarget != null)
        {
            Destroy(currentTarget.gameObject);
            currentTarget = null;
        }

        if (startPoint != null)
            transform.position = startPoint.position;

        currentPath.Clear();
        currentWaypoint = 0;
        currentState = AIState.FindingItem;
    }

    private void Update()
    {
        switch (currentState)
        {
            case AIState.FindingItem:
                FindNextItem();
                break;

            case AIState.MovingToItem:
                MoveToItem();
                break;

            case AIState.MovingToDeliveryPoint:
                MoveToDeliveryPoint();
                break;

            case AIState.Finished:
                break;
        }
    }

    // Finds the best reachable item.
    private void FindNextItem()
    {
        pathCreator = PathCreator.Active;

        if (pathCreator == null || ItemManager.Instance == null)
            return;

        Vector3Int startCell = pathCreator.WorldToCell(transform.position);
        Vector3Int deliveryCell = pathCreator.WorldToCell(deliveryPoint.position);

        Item bestItem = null;
        List<Vector3> bestPath = null;
        int shortestTotalPath = int.MaxValue;

        foreach (var entry in ItemManager.Instance.ActiveItems)
        {
            Item item = entry.Key;
            Vector3 itemPosition = entry.Value;

            if (item == null || item.IsCollected)
                continue;

            Vector3Int itemCell = pathCreator.WorldToCell(itemPosition);

            List<Vector3> pathToItem = FindPath(startCell, itemCell);

            if (pathToItem == null)
                continue;

            List<Vector3> pathToDelivery = FindPath(itemCell, deliveryCell);

            if (pathToDelivery == null)
                continue;

            int totalPathLength = pathToItem.Count + pathToDelivery.Count;

            if (totalPathLength < shortestTotalPath)
            {
                shortestTotalPath = totalPathLength;
                bestItem = item;
                bestPath = pathToItem;
            }
        }

        if (bestItem == null)
        {
            currentState = AIState.Finished;
            currentTarget = null;
            currentPath.Clear();
            return;
        }

        currentTarget = bestItem;
        currentPath = bestPath;
        currentWaypoint = 0;

        currentState = AIState.MovingToItem;
    }

    // Moves the AI towards the selected item.
    private void MoveToItem()
    {
        if (currentTarget == null)
        {
            currentState = AIState.FindingItem;
            return;
        }

        if (MoveAlongPath())
        {
            currentTarget.PickUp(handPoint);

            currentPath = FindPath(
                pathCreator.WorldToCell(transform.position),
                pathCreator.WorldToCell(deliveryPoint.position)
            );

            if (currentPath == null)
            {
                Debug.LogError("PlayerAI: No path to delivery point.", this);
                currentState = AIState.Finished;
                return;
            }

            currentWaypoint = 0;
            currentState = AIState.MovingToDeliveryPoint;
        }
    }

    // Moves the AI towards the delivery point.
    private void MoveToDeliveryPoint()
    {
        if (MoveAlongPath())
        {
            currentTarget.Deliver();
            currentTarget = null;

            currentPath.Clear();
            currentWaypoint = 0;

            if (ItemManager.Instance != null && ItemManager.Instance.ActiveItems.Count == 0 && levelManager != null)
            {
                levelManager.Next();
                return;
            }

            currentState = AIState.FindingItem;
        }
    }

    // Moves towards the next waypoint.
    private bool MoveAlongPath()
    {
        if (currentWaypoint >= currentPath.Count)
            return true;

        Vector3 targetPosition = currentPath[currentWaypoint];

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) <= arrivalDistance)
        {
            transform.position = targetPosition;
            currentWaypoint++;
        }

        return currentWaypoint >= currentPath.Count;
    }

    // Recalculates the route if the current target is moved.
    private void HandleItemPositionChanged(Item item, Vector3 newPosition)
    {
        if (currentState != AIState.MovingToItem || item != currentTarget)
            return;

        pathCreator = PathCreator.Active;

        if (pathCreator == null)
            return;

        Vector3Int startCell = pathCreator.WorldToCell(transform.position);
        Vector3Int targetCell = pathCreator.WorldToCell(newPosition);

        List<Vector3> newPath = FindPath(startCell, targetCell);

        if (newPath == null)
        {
            currentTarget = null;
            currentPath.Clear();
            currentState = AIState.FindingItem;
            return;
        }

        currentPath = newPath;
        currentWaypoint = 0;
    }

    // A* pathfinding algorithm.
    private List<Vector3> FindPath(Vector3Int start, Vector3Int target)
    {
        if (pathCreator == null)
            return null;

        if (!pathCreator.IsWalkable(start) || !pathCreator.IsWalkable(target))
            return null;

        if (start == target)
            return new List<Vector3>();

        List<Vector3Int> openSet = new();
        HashSet<Vector3Int> closedSet = new();

        Dictionary<Vector3Int, Vector3Int> cameFrom = new();
        Dictionary<Vector3Int, int> gCost = new();

        openSet.Add(start);
        gCost[start] = 0;

        Vector3Int[] directions =
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        while (openSet.Count > 0)
        {
            Vector3Int current = openSet[0];

            foreach (Vector3Int cell in openSet)
            {
                int currentFCost = gCost[current] + GetHeuristic(current, target);
                int cellFCost = gCost[cell] + GetHeuristic(cell, target);

                if (cellFCost < currentFCost)
                    current = cell;
            }

            if (current == target)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector3Int direction in directions)
            {
                Vector3Int neighbor = current + direction;

                if (closedSet.Contains(neighbor))
                    continue;

                if (!pathCreator.CanMove(current, neighbor))
                    continue;

                int tentativeGCost = gCost[current] + 1;

                if (!gCost.ContainsKey(neighbor) ||
                    tentativeGCost < gCost[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gCost[neighbor] = tentativeGCost;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null;
    }

    // Reconstructs the path from the destination back to the start.
    private List<Vector3> ReconstructPath(
        Dictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int current)
    {
        List<Vector3> path = new();

        while (cameFrom.ContainsKey(current))
        {
            path.Add(pathCreator.CellToWorldCenter(current));
            current = cameFrom[current];
        }

        path.Reverse();

        return path;
    }

    // Manhattan distance heuristic.
    private int GetHeuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void OnDrawGizmos()
    {
        if (currentPath == null || currentPath.Count == 0)
            return;

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
