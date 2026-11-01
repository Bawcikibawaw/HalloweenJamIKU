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
        private static ScrollRect popupScrollRect;

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
            }

            EnsureScrollSetup(popupPanel);

            if (popupDescription == null)
            {
                popupLogo = popupPanel.transform.Find("PopupLogo")?.GetComponent<Image>();
                popupTitle = popupPanel.transform.Find("PopupTitle")?.GetComponent<TMP_Text>();
                popupDescription = popupPanel.transform.Find("DescScrollView/PopupDescription")?.GetComponent<TMP_Text>()
                                   ?? popupPanel.transform.Find("PopupDescription")?.GetComponent<TMP_Text>();
                popupScrollRect = popupPanel.GetComponentInChildren<ScrollRect>();
            }

            if (popupLogo != null)
            {
                popupLogo.sprite = s.Icon;
                popupLogo.color = s.Icon != null ? Color.white : Color.Lerp(s.Color, Color.white, 0.5f);
            }
            if (popupTitle != null) popupTitle.text = s.Name;
            if (popupDescription != null)
            {
                popupDescription.text = $"<color=#{ColorUtility.ToHtmlStringRGB(s.Color)}>{s.Tree} Tree - Tier {s.Tier}</color>\n\n{s.Description}\n\n<color=#E5C158>Cooldown: {s.Cooldown:0.#}s</color>";
                if (popupScrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    popupScrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        public static void EnsureScrollSetup(GameObject panel)
        {
            if (panel == null) return;

            Transform logoTr = panel.transform.Find("PopupLogo");
            if (logoTr != null)
            {
                RectTransform logoRt = (RectTransform)logoTr;
                logoRt.anchorMin = new Vector2(0.5f, 1f);
                logoRt.anchorMax = new Vector2(0.5f, 1f);
                logoRt.pivot = new Vector2(0.5f, 1f);
                logoRt.anchoredPosition = new Vector2(0f, -14f);
                logoRt.sizeDelta = new Vector2(140f, 140f);

                Image logoImg = logoTr.GetComponent<Image>();
                if (logoImg != null) logoImg.preserveAspect = true;
            }

            Transform titleTr = panel.transform.Find("PopupTitle");
            if (titleTr != null)
            {
                RectTransform titleRt = (RectTransform)titleTr;
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -160f);
                titleRt.sizeDelta = new Vector2(400f, 36f);
                TMP_Text titleTmp = titleTr.GetComponent<TMP_Text>();
                if (titleTmp != null)
                {
                    titleTmp.fontSize = 30f;
                    titleTmp.alignment = TextAlignmentOptions.Top;
                }
            }

            Transform scrollTr = panel.transform.Find("DescScrollView");
            if (scrollTr == null)
            {
                Transform oldDesc = panel.transform.Find("PopupDescription");

                GameObject scrollObj = new GameObject("DescScrollView");
                scrollObj.transform.SetParent(panel.transform, false);
                RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
                scrollRt.anchorMin = new Vector2(0.5f, 1f);
                scrollRt.anchorMax = new Vector2(0.5f, 1f);
                scrollRt.pivot = new Vector2(0.5f, 1f);
                scrollRt.anchoredPosition = new Vector2(0f, -200f);
                scrollRt.sizeDelta = new Vector2(400f, 226f);

                Image hitImg = scrollObj.AddComponent<Image>();
                hitImg.color = Color.clear;

                scrollObj.AddComponent<RectMask2D>();
                ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 30f;
                scrollRect.viewport = scrollRt;

                GameObject descObj;
                if (oldDesc != null)
                {
                    descObj = oldDesc.gameObject;
                    descObj.transform.SetParent(scrollObj.transform, false);
                }
                else
                {
                    descObj = new GameObject("PopupDescription");
                    descObj.transform.SetParent(scrollObj.transform, false);
                    descObj.AddComponent<TextMeshProUGUI>();
                }

                RectTransform descRt = descObj.GetComponent<RectTransform>();
                descRt.anchorMin = new Vector2(0f, 1f);
                descRt.anchorMax = new Vector2(1f, 1f);
                descRt.pivot = new Vector2(0.5f, 1f);
                descRt.anchoredPosition = Vector2.zero;
                descRt.offsetMin = new Vector2(10f, 0f);
                descRt.offsetMax = new Vector2(-20f, 0f);

                TextMeshProUGUI descTmp = descObj.GetComponent<TextMeshProUGUI>();
                if (descTmp != null)
                {
                    descTmp.fontSize = 24f;
                    descTmp.enableAutoSizing = false;
                    descTmp.textWrappingMode = TextWrappingModes.Normal;
                    descTmp.alignment = TextAlignmentOptions.Top;
                    descTmp.lineSpacing = 0f;
                    descTmp.paragraphSpacing = 10f;
                }

                ContentSizeFitter fitter = descObj.GetComponent<ContentSizeFitter>() ?? descObj.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.content = descRt;

                GameObject sbarObj = new GameObject("Scrollbar");
                sbarObj.transform.SetParent(scrollObj.transform, false);
                RectTransform sbarRt = sbarObj.AddComponent<RectTransform>();
                sbarRt.anchorMin = new Vector2(1f, 0f);
                sbarRt.anchorMax = new Vector2(1f, 1f);
                sbarRt.pivot = new Vector2(1f, 0.5f);
                sbarRt.anchoredPosition = new Vector2(-2f, 0f);
                sbarRt.sizeDelta = new Vector2(6f, -16f);

                Image trackImg = sbarObj.AddComponent<Image>();
                trackImg.color = new Color(0f, 0f, 0f, 0.25f);

                GameObject handleObj = new GameObject("Handle");
                handleObj.transform.SetParent(sbarObj.transform, false);
                RectTransform handleRt = handleObj.AddComponent<RectTransform>();
                handleRt.sizeDelta = Vector2.zero;
                Image handleImg = handleObj.AddComponent<Image>();
                handleImg.color = new Color(0.9f, 0.8f, 0.4f, 0.6f);

                Scrollbar sbar = sbarObj.AddComponent<Scrollbar>();
                sbar.direction = Scrollbar.Direction.BottomToTop;
                sbar.targetGraphic = handleImg;
                sbar.handleRect = handleRt;

                scrollRect.verticalScrollbar = sbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            else
            {
                RectTransform scrollRt = (RectTransform)scrollTr;
                scrollRt.anchorMin = new Vector2(0.5f, 1f);
                scrollRt.anchorMax = new Vector2(0.5f, 1f);
                scrollRt.pivot = new Vector2(0.5f, 1f);
                scrollRt.anchoredPosition = new Vector2(0f, -200f);
                scrollRt.sizeDelta = new Vector2(400f, 226f);

                Transform descTr = scrollTr.Find("PopupDescription");
                if (descTr != null)
                {
                    RectTransform descRt = (RectTransform)descTr;
                    descRt.offsetMin = new Vector2(10f, 0f);
                    descRt.offsetMax = new Vector2(-20f, 0f);

                    TMP_Text descTmp = descTr.GetComponent<TMP_Text>();
                    if (descTmp != null)
                    {
                        descTmp.fontSize = 24f;
                        descTmp.enableAutoSizing = false;
                        descTmp.lineSpacing = 0f;
                        descTmp.paragraphSpacing = 10f;
                    }
                }
            }
        }
    }
}
