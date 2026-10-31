using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace aTES
{
    /// <summary>
    /// Skill choose page. Put on "SkillChoosePanel" (needs children "Trees", "CountText", "StartButton").
    /// Pauses the game, builds one column per skill tree from <see cref="SkillManager.AllSkills"/>,
    /// and starts the game once 5 skills are chosen. Hovering a skill shows it in the skill popup.
    /// </summary>
    public class SkillChooseScreen : MonoBehaviour
    {
        private static readonly Color Available = new(0.2f, 0.2f, 0.25f);
        private static readonly Color Locked = new(0.07f, 0.07f, 0.09f);

        private readonly Dictionary<Skill, Image> buttons = new();
        private SkillManager manager;
        private Button startButton;
        private TMP_Text countText;

        private void Start()
        {
            manager = SkillManager.Instance;
            if (manager == null)
            {
                gameObject.SetActive(false);
                return;
            }

            startButton = transform.Find("StartButton")?.GetComponent<Button>();
            countText = transform.Find("CountText")?.GetComponent<TMP_Text>();
            Transform trees = transform.Find("Trees") ?? transform;

            foreach (IGrouping<SkillTree, Skill> tree in manager.AllSkills.GroupBy(s => s.Tree))
            {
                Transform column = NewColumn(trees, tree.Key);
                foreach (Skill skill in tree) buttons[skill] = NewSkillButton(column, skill);
            }

            if (startButton != null) startButton.onClick.AddListener(Begin);
            manager.OnEquippedChanged += Refresh;
            Refresh();
            Time.timeScale = 0f;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.OnEquippedChanged -= Refresh;
        }

        private void Refresh()
        {
            foreach (KeyValuePair<Skill, Image> b in buttons)
                b.Value.color = manager.Equipped.Contains(b.Key) ? b.Key.Color : manager.CanEquip(b.Key) ? Available : Locked;

            int required = Mathf.Min(SkillManager.SlotCount, manager.AllSkills.Count);
            if (countText != null) countText.text = $"CHOSEN {manager.Equipped.Count}/{required}";
            if (startButton != null) startButton.interactable = manager.Equipped.Count == required;
        }

        private void Begin()
        {
            Time.timeScale = 1f;
            gameObject.SetActive(false);
        }

        private static Transform NewColumn(Transform parent, SkillTree tree)
        {
            RectTransform column = NewRect($"{tree}Tree", parent);
            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI header = NewText(column, tree.ToString().ToUpper(), 30, Skill.TreeColor(tree));
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            return column;
        }

        private Image NewSkillButton(Transform column, Skill skill)
        {
            RectTransform rt = NewRect(skill.Name, column);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
            Image bg = rt.gameObject.AddComponent<Image>();

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => manager.Toggle(skill));

            EventTrigger.Entry hover = new() { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => SkillSlotUI.ShowPopup(skill));
            rt.gameObject.AddComponent<EventTrigger>().triggers.Add(hover);

            RectTransform icon = NewRect("Icon", rt);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(12f, 0f);
            icon.sizeDelta = new Vector2(80f, 80f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = skill.Icon;
            iconImage.color = skill.Icon != null ? Color.white : Color.Lerp(skill.Color, Color.white, 0.5f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            TextMeshProUGUI label = NewText(rt, $"{skill.Name}\n<size=70%>Tier {skill.Tier}</size>", 22, Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(104f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return bg;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static TextMeshProUGUI NewText(Transform parent, string text, float size, Color color)
        {
            TextMeshProUGUI tmp = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
