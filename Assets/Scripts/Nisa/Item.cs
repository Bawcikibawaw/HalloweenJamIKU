using System.Linq;
using UnityEngine;
using aTES;

public class Item : MonoBehaviour
{
    public ItemData Data { get; private set; }
    public bool IsCollected { get; private set; }

    public void Initialize(ItemData data)
    {
        Data = data;
        IsCollected = false;
    }

    public bool MoveToCell(Vector3Int targetCell)
    {
        if (IsCollected || ItemManager.Instance == null)
            return false;

        return ItemManager.Instance.TryMoveItem(this, targetCell);
    }

    [ContextMenu("Move To Random Walkable Cell")]
    private void MoveToRandomWalkableCell()
    {
        if (PathCreator.Active == null) return;
        Vector3Int[] walkable = PathCreator.Active.Cells.Where(kv => kv.Value.walkable).Select(kv => kv.Key).ToArray();
        if (walkable.Length > 0) MoveToCell(walkable[Random.Range(0, walkable.Length)]);
    }

    public void PickUp(Transform handPoint)
    {
        if (IsCollected)
            return;

        IsCollected = true;
        ItemManager.Instance.UnregisterItem(this);

        transform.SetParent(handPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (TryGetComponent<Collider2D>(out Collider2D itemCollider))
            itemCollider.enabled = false;
    }

    public void Deliver()
    {
        Destroy(gameObject);
    }
}
