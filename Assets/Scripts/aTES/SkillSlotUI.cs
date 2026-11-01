using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace aTES
{
    /// <summary>
    /// One of the 5 equipped skill boxes. Its order under its parent is its slot (key 1-5).
    /// Shows the equipped skill's icon + cooldown, and the skill popup on hover.
    /// Click = use the skill (or unchoose it while on the skill choose page).
    /// </summary>
    public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static GameObject popupPanel;
        private static Image popupLogo;
        private static TMP_Text popupTitle;
        private static TMP_Text popupDescription;

        private Image logo;
        private Image cooldown;
        private Skill skill;

        private int Slot => transform.GetSiblingIndex();

        private void Awake()
        {
            logo = transform.Find("Logo")?.GetComponent<Image>();
            cooldown = transform.Find("Cooldown")?.GetComponent<Image>();

            TMP_Text key = transform.Find("Key")?.GetComponent<TMP_Text>();
            if (key != null) key.text = (Slot + 1).ToString();
        }

        private void Start()
        {
            if (SkillManager.Instance != null) SkillManager.Instance.OnEquippedChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (SkillManager.Instance != null) SkillManager.Instance.OnEquippedChanged -= Refresh;
        }

        private void Update()
        {
            if (cooldown != null) cooldown.fillAmount = skill?.CooldownPercent ?? 0f;
        }

        private void Refresh()
        {
            SkillManager m = SkillManager.Instance;
            skill = m != null && Slot < m.Equipped.Count ? m.Equipped[Slot] : null;

            if (logo == null) return;
            logo.enabled = skill != null;
            if (skill == null) return;
            logo.sprite = skill.Icon;
            logo.color = skill.Icon != null ? Color.white : Color.Lerp(skill.Color, Color.white, 0.5f);
        }

        public void OnPointerEnter(PointerEventData eventData) => ShowPopup(skill);

        public void OnPointerClick(PointerEventData eventData)
        {
            SkillManager m = SkillManager.Instance;
            if (m == null || skill == null) return;

            if (Time.timeScale == 0f) m.Toggle(skill);
            else m.Use(Slot);
        }

        /// <summary>Shows a skill's logo, name and explanation in "SkillPopupPanel".</summary>
        public static void ShowPopup(Skill s)
        {
            if (s == null) return;
            if (popupPanel == null)
            {
                popupPanel = GameObject.Find("SkillPopupPanel");
                if (popupPanel == null) return;
                popupLogo = popupPanel.transform.Find("PopupLogo")?.GetComponent<Image>();
                popupTitle = popupPanel.transform.Find("PopupTitle")?.GetComponent<TMP_Text>();
                popupDescription = popupPanel.transform.Find("PopupDescription")?.GetComponent<TMP_Text>();
            }

            if (popupLogo != null)
            {
                popupLogo.sprite = s.Icon;
                popupLogo.color = s.Icon != null ? Color.white : Color.Lerp(s.Color, Color.white, 0.5f);
            }
            if (popupTitle != null) popupTitle.text = s.Name;
            if (popupDescription != null)
                popupDescription.text = $"<color=#{ColorUtility.ToHtmlStringRGB(s.Color)}>{s.Tree} Tree - Tier {s.Tier}</color>\n\n{s.Description}\n\nCooldown: {s.Cooldown:0.#}s";
        }
    }
}
