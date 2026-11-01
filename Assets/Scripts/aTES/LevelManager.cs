using UnityEngine;
using System;
using System.Linq;
using TMPro;
using UnityEngine.UI;

namespace aTES
{
    /// <summary>
    /// Put this on a parent object whose children are the levels (each child = one Grid with a PathCreator).
    /// Level order is the order of the children in the Hierarchy. Only the current level is enabled.
    /// </summary>
    [ExecuteAlways]
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private float cameraZoom = 9.9f;
        [SerializeField] private float rightOffset = 2.6f;
        [SerializeField] private float bottomOffset = 0.5f;

        public int Current { get; private set; }
        
        public event Action<int> OnLevelChanged;
        
        private void Awake()
        {
            if (Application.isPlaying) Show(0);
        }

        private void OnEnable()
        {
            SyncTopHud();
        }

        private static void SyncTopHud()
        {
            GameObject fgTitle = GameObject.Find("FalseGodTitle");
            if (fgTitle != null) DestroyImmediate(fgTitle);

            RectTransform levelRt = GameObject.Find("LevelPanel")?.GetComponent<RectTransform>();
            if (levelRt != null)
            {
                levelRt.anchoredPosition = new Vector2(-1120f, -6f);
                levelRt.sizeDelta = new Vector2(210f, 56f);
                TMP_Text lt = levelRt.Find("LevelText")?.GetComponent<TMP_Text>();
                if (lt != null) lt.fontSize = 22f;
            }

            RectTransform timerRt = GameObject.Find("TimerPanel")?.GetComponent<RectTransform>();
            if (timerRt != null)
            {
                timerRt.anchoredPosition = new Vector2(-142f, -6f);
                timerRt.sizeDelta = new Vector2(230f, 56f);
                TMP_Text tt = timerRt.Find("TimerText")?.GetComponent<TMP_Text>();
                if (tt != null)
                {
                    tt.fontSize = 22f;
                    tt.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                }
                TMP_Text wt = timerRt.Find("WarningText")?.GetComponent<TMP_Text>();
                if (wt != null)
                {
                    wt.fontSize = 13f;
                    wt.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                }
            }

            GameObject popupPanel = GameObject.Find("SkillPopupPanel");
            if (popupPanel != null)
            {
                SkillSlotUI.EnsureScrollSetup(popupPanel);
            }

#if UNITY_EDITOR
            SyncStuff3Sprites();
#endif
        }

#if UNITY_EDITOR
        private static void SyncStuff3Sprites()
        {
            Sprite[] stuff3 = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff3-(stas work).png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            if (stuff3.Length < 10) return;

            // 0: Skill choose panel
            Image chooseImg = GameObject.Find("SkillChoosePanel")?.GetComponent<Image>();
            if (chooseImg != null) chooseImg.sprite = stuff3[0];

            // 1: Pause panel
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform pausePanelTr = canvas.transform.Find("PausePanel");
                if (pausePanelTr == null)
                {
                    GameObject pausePanel = new GameObject("PausePanel");
                    pausePanel.transform.SetParent(canvas.transform, false);
                    RectTransform pRt = pausePanel.AddComponent<RectTransform>();
                    pRt.anchorMin = new Vector2(0.5f, 0.5f);
                    pRt.anchorMax = new Vector2(0.5f, 0.5f);
                    pRt.pivot = new Vector2(0.5f, 0.5f);
                    pRt.anchoredPosition = Vector2.zero;
                    pRt.sizeDelta = new Vector2(480f, 680f);
                    Image pImg = pausePanel.AddComponent<Image>();
                    pImg.sprite = stuff3[1];
                    pImg.color = Color.white;

                    GameObject titleObj = new GameObject("Title");
                    titleObj.transform.SetParent(pausePanel.transform, false);
                    RectTransform tRt = titleObj.AddComponent<RectTransform>();
                    tRt.anchorMin = new Vector2(0.5f, 1f);
                    tRt.anchorMax = new Vector2(0.5f, 1f);
                    tRt.pivot = new Vector2(0.5f, 1f);
                    tRt.anchoredPosition = new Vector2(0f, -80f);
                    tRt.sizeDelta = new Vector2(400f, 60f);
                    TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
                    TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MesseDuesseldorf-O1d3 SDF.asset");
                    if (font != null) titleTmp.font = font;
                    titleTmp.text = "PAUSED";
                    titleTmp.fontSize = 42;
                    titleTmp.color = new Color(1f, 0.9f, 0.55f);
                    titleTmp.alignment = TextAlignmentOptions.Center;

                    Sprite buttonSprite = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff.png")
                        .OfType<Sprite>().OrderBy(s => s.name).ElementAtOrDefault(5);

                    Action<string, string, float> makeBtn = (bName, bText, yPos) =>
                    {
                        GameObject bObj = new GameObject(bName);
                        bObj.transform.SetParent(pausePanel.transform, false);
                        RectTransform bRt = bObj.AddComponent<RectTransform>();
                        bRt.anchorMin = new Vector2(0.5f, 0.5f);
                        bRt.anchorMax = new Vector2(0.5f, 0.5f);
                        bRt.pivot = new Vector2(0.5f, 0.5f);
                        bRt.anchoredPosition = new Vector2(0f, yPos);
                        bRt.sizeDelta = new Vector2(300f, 75f);
                        Image bImg = bObj.AddComponent<Image>();
                        bImg.sprite = buttonSprite;
                        Button btn = bObj.AddComponent<Button>();
                        btn.targetGraphic = bImg;

                        GameObject txtObj = new GameObject("Text");
                        txtObj.transform.SetParent(bObj.transform, false);
                        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
                        txtRt.anchorMin = Vector2.zero;
                        txtRt.anchorMax = Vector2.one;
                        txtRt.sizeDelta = Vector2.zero;
                        TextMeshProUGUI bTmp = txtObj.AddComponent<TextMeshProUGUI>();
                        if (font != null) bTmp.font = font;
                        bTmp.text = bText;
                        bTmp.fontSize = 28;
                        bTmp.color = Color.white;
                        bTmp.alignment = TextAlignmentOptions.Center;
                        bTmp.raycastTarget = false;
                    };

                    makeBtn("ResumeButton", "RESUME", 50f);
                    makeBtn("RestartButton", "RESTART", -50f);
                    makeBtn("QuitButton", "QUIT", -150f);

                    if (canvas.GetComponent<PauseMenu>() == null)
                        canvas.gameObject.AddComponent<PauseMenu>();

                    pausePanel.SetActive(false);
                }
                else
                {
                    Image pImg = pausePanelTr.GetComponent<Image>();
                    if (pImg != null) pImg.sprite = stuff3[1];
                    if (canvas.GetComponent<PauseMenu>() == null)
                        canvas.gameObject.AddComponent<PauseMenu>();
                }
            }

            // 2: Gathered item boxes
            for (int i = 1; i <= 5; i++)
            {
                Image box = GameObject.Find($"IngredientBox_{i:00}")?.GetComponent<Image>();
                if (box != null) box.sprite = stuff3[2];
            }

            // 3: Chosen skill boxes
            for (int i = 1; i <= 5; i++)
            {
                Image box = GameObject.Find($"SkillBox_{i:00}")?.GetComponent<Image>();
                if (box != null) box.sprite = stuff3[3];
            }

            // 4: Gathered items background panel
            Image ingPanel = GameObject.Find("IngredientsPanel")?.GetComponent<Image>();
            if (ingPanel != null) ingPanel.sprite = stuff3[4];

            // 5: Gathering spot
            SpriteRenderer del = GameObject.Find("DeliveryPoint")?.GetComponent<SpriteRenderer>();
            if (del != null) del.sprite = stuff3[5];

            // 7: Chosen skills background panel
            Image skillsGrid = GameObject.Find("SkillsGrid")?.GetComponent<Image>();
            if (skillsGrid != null) skillsGrid.sprite = stuff3[7];

            // 8: Skill description background panel
            Image popupBg = GameObject.Find("SkillPopupPanel")?.GetComponent<Image>();
            if (popupBg != null) popupBg.sprite = stuff3[8];

            // 9: False gods background panel
            Image fgBg = GameObject.Find("FalseGodPanel")?.GetComponent<Image>();
            if (fgBg != null) fgBg.sprite = stuff3[9];
        }
#endif

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.orthographicSize = cameraZoom;
            cam.transform.position = new Vector3(9f + rightOffset - cameraZoom * cam.aspect, -9f - bottomOffset + cameraZoom, -10f);
        }

        [ContextMenu("Next Level")]
        public void Next() => Show((Current + 1) % transform.childCount);

        public void Show(int index)
        {
            if (transform.childCount == 0) return;
            Current = Mathf.Clamp(index, 0, transform.childCount - 1);

            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(i == Current);
            }

            Transform activeLevel = transform.GetChild(Current);
            Transform levelStart = activeLevel.Find("StartPoint");
            Transform levelDelivery = activeLevel.Find("DeliveryPoint");

            GameObject startObj = GameObject.Find("PlayerStartPoint");
            if (startObj != null && levelStart != null) startObj.transform.position = levelStart.position;

            GameObject deliveryObj = GameObject.Find("DeliveryPoint");
            if (deliveryObj != null && levelDelivery != null) deliveryObj.transform.position = levelDelivery.position;

            if (!Application.isPlaying && startObj != null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) player.transform.position = startObj.transform.position;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_Text levelText = canvas != null
                ? canvas.transform.Find("LevelPanel/LevelText")?.GetComponent<TMP_Text>()
                : GameObject.Find("LevelText")?.GetComponent<TMP_Text>();
            if (levelText != null) levelText.text = $"LEVEL {Current + 1}";

            OnLevelChanged?.Invoke(Current);
        }
    }
}
