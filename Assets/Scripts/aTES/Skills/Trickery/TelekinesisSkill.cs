using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace aTES
{
    public class TelekinesisSkill : Skill
    {
        public TelekinesisSkill() : base("Telekinesis", "Click and drag any ingredient on the map with your mouse and drop it onto any reachable tile.", SkillTree.Trickery, 3, 15f) { }

        public override bool TryUse(SkillManager m)
        {
            if (CooldownLeft > 0f || !Activate(m)) return false;
            return true;
        }

        protected override bool Activate(SkillManager m)
        {
            if (m.Items == null || m.Items.ActiveItems.Count == 0) return false;

            if (!m.TryGetComponent(out TelekinesisDragController controller))
                controller = m.gameObject.AddComponent<TelekinesisDragController>();

            return controller.Arm(this);
        }
    }

    public class TelekinesisDragController : MonoBehaviour
    {
        private TelekinesisSkill activeSkill;
        private Item draggedItem;
        private SpriteRenderer draggedRenderer;
        private Color originalColor;
        private Vector3 originalScale;
        private Vector3 originalPosition;
        private int originalSortingOrder;
        private TMP_Text warningText;

        public bool Arm(TelekinesisSkill skill)
        {
            if (activeSkill != null) return false;
            activeSkill = skill;

            if (warningText == null)
                warningText = GameObject.Find("WarningText")?.GetComponent<TMP_Text>();
            if (warningText != null)
                warningText.text = "DRAG & DROP AN INGREDIENT!";

            TryGrabUnderCursor();
            return true;
        }

        private void Update()
        {
            if (activeSkill == null) return;

            if (Time.timeScale == 0f || ItemManager.Instance == null || PathCreator.Active == null || ItemManager.Instance.ActiveItems.Count == 0)
            {
                CancelDrag();
                Disarm();
                return;
            }

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector3 mouseWorld = GetMouseWorldPosition(mouse, cam);

            if (draggedItem == null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    TryGrabAt(mouseWorld);
                return;
            }

            // Keep following mouse while held
            draggedItem.transform.position = mouseWorld;

            Vector3Int targetCell = PathCreator.Active.WorldToCell(mouseWorld);
            bool validDrop = ItemManager.Instance.CanPlaceItemAt(draggedItem, targetCell);

            if (draggedRenderer != null)
                draggedRenderer.color = validDrop ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);

            if (mouse.leftButton.wasReleasedThisFrame)
                FinishDrop(targetCell);
        }

        private void TryGrabUnderCursor()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
                TryGrabAt(GetMouseWorldPosition(mouse, cam));
        }

        private void TryGrabAt(Vector3 mouseWorld)
        {
            PathCreator map = PathCreator.Active;
            Vector3Int mouseCell = map.WorldToCell(mouseWorld);

            Item best = null;
            float bestDist = 0.75f;

            foreach (KeyValuePair<Item, Vector3> kv in ItemManager.Instance.ActiveItems)
            {
                if (kv.Key == null || kv.Key.IsCollected) continue;

                float dist = Vector2.Distance(mouseWorld, kv.Key.transform.position);
                if (map.WorldToCell(kv.Value) == mouseCell || dist < bestDist)
                {
                    bestDist = dist;
                    best = kv.Key;
                }
            }

            if (best == null) return;

            draggedItem = best;
            draggedItem.IsBeingDragged = true;
            originalPosition = draggedItem.transform.position;
            originalScale = draggedItem.transform.localScale;
            draggedItem.transform.localScale = originalScale * 1.35f;

            if (draggedItem.TryGetComponent(out draggedRenderer))
            {
                originalColor = draggedRenderer.color;
                originalSortingOrder = draggedRenderer.sortingOrder;
                draggedRenderer.sortingOrder = 15;
            }

            if (SkillManager.Instance?.Player != null)
                SkillManager.Instance.Player.Replan();
        }

        private void FinishDrop(Vector3Int targetCell)
        {
            Item item = draggedItem;
            RestoreItemVisuals();
            draggedItem = null;

            if (item != null && ItemManager.Instance.TryMoveItem(item, targetCell))
            {
                if (SkillManager.Instance?.Player != null)
                    SkillManager.Instance.Player.Replan();

                activeSkill.StartCooldown();
                Disarm();
            }
            else if (item != null)
            {
                item.transform.position = originalPosition;
                if (SkillManager.Instance?.Player != null)
                    SkillManager.Instance.Player.Replan();
            }
        }

        private void CancelDrag()
        {
            if (draggedItem == null) return;
            draggedItem.transform.position = originalPosition;
            RestoreItemVisuals();
            draggedItem = null;
        }

        private void RestoreItemVisuals()
        {
            if (draggedItem == null) return;
            draggedItem.IsBeingDragged = false;
            draggedItem.transform.localScale = originalScale;

            if (draggedRenderer != null)
            {
                draggedRenderer.color = originalColor;
                draggedRenderer.sortingOrder = originalSortingOrder;
            }
            draggedRenderer = null;
        }

        private void Disarm()
        {
            activeSkill = null;
            if (warningText != null && warningText.text == "DRAG & DROP AN INGREDIENT!")
                warningText.text = "";
        }

        private static Vector3 GetMouseWorldPosition(Mouse mouse, Camera cam)
        {
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            world.z = 0f;
            return world;
        }
    }
}
