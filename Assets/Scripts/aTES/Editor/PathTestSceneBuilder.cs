using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace aTES.EditorTools
{
    /// <summary>
    /// Builds the full system test setup in the currently open scene:
    /// - 3 full 16x16 levels under "Levels" (LevelManager + PathCreator + BarrierTile)
    /// - Player with PlayerAI, PlayerMoveRays, and HandPoint
    /// - PlayerStartPoint and DeliveryPoint (with visual indicator)
    /// - Item prefab, ItemData asset, and ItemManager (spawns 4 items per level)
    /// - GameTimer with a UI Canvas (TimerText + WarningText)
    /// </summary>
    public static class PathTestSceneBuilder
    {
        private const string FloorSheet = "Assets/Sprites/bc conspt jem.png";
        private const string TileFolder = "Assets/Sprites/Tilemaps/Generated";
        private const int Cell = 16;
        private const int Size = 16;
        private const int EdgeMinPixels = 40;

        private static readonly string[] Level2Walls =
        {
            "................",
            "................",
            "..1DDDDDDDDDD2..",
            "..R..........L..",
            "..R..1DDDD...L..",
            "..R..R.......L..",
            "..R..3UUUUUUU4..",
            "..R.............",
            "..R..1DDDDDDD2..",
            "..R..R.......L..",
            "..R..3UUUU...L..",
            "..R..........L..",
            "..3UUUUUUU...L..",
            ".............L..",
            "...UUUUUUUUUU4..",
            "................",
        };

        private static readonly string[] Level3Walls =
        {
            "................",
            "................",
            "..1DDDD2..1DD2..",
            "..R....L..R..L..",
            "..R....L..R..L..",
            "..3U..U4..3U.L..",
            ".............L..",
            "..1DDDDDDDD..L..",
            "..R..........L..",
            "..R..1DDDD2..L..",
            "..R..R....L..L..",
            "..R..3U..U4..L..",
            "..R..........L..",
            "..3UUUUUU....L..",
            "........UUUUU4..",
            "................",
        };

        [MenuItem("Tools/aTES/Build Path Test Scene")]
        public static void Build()
        {
            string[] walls = AssetDatabase.FindAssets("duvarlar jem t:Texture2D", new[] { "Assets/Sprites" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string inner = walls.Single(p => p.EndsWith("/duvarlar jem.png"));
            string outer = walls.Single(p => p != inner);

            foreach (string sheet in new[] { FloorSheet, inner, outer }) Slice(sheet);

            AssetDatabase.DeleteAsset(TileFolder);
            Directory.CreateDirectory(TileFolder);
            AssetDatabase.Refresh();

            // Clean up previous scene objects
            foreach (string name in new[] { "Grid", "Levels", "PlayerStartPoint", "DeliveryPoint", "ItemManager", "GameTimer", "SkillManager", "Canvas", "EventSystem" })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            // 1. Levels + LevelManager + PathCreator
            GameObject levelsRoot = new GameObject("Levels");
            LevelManager levelManager = levelsRoot.AddComponent<LevelManager>();

            (Tilemap ground1, Tilemap walls1) = NewBaseLevel(levelsRoot.transform, "Level 1", outer);
            PaintSheet(inner, walls1, true);
            ground1.GetComponentInParent<PathCreator>().ScanMap();

            Dictionary<char, TileBase> innerTiles = new Dictionary<char, TileBase>
            {
                ['U'] = LoadTile("duvarlar jem_6_4"),
                ['D'] = LoadTile("duvarlar jem_4_2"),
                ['L'] = LoadTile("duvarlar jem_12_3"),
                ['R'] = LoadTile("duvarlar jem_3_3"),
                ['1'] = LoadTile("duvarlar jem_3_2"),
                ['2'] = LoadTile("duvarlar jem_12_2"),
                ['3'] = LoadTile("duvarlar jem_3_6"),
                ['4'] = LoadTile("duvarlar jem_10_6"),
            };

            BuildCustomLevel(levelsRoot.transform, "Level 2", outer, Level2Walls, innerTiles);
            BuildCustomLevel(levelsRoot.transform, "Level 3", outer, Level3Walls, innerTiles);
            levelManager.Show(0);

            // 2. Start & Delivery Points
            Vector3 startWorld = ground1.GetCellCenterWorld(CellOf(2, 2));
            Vector3 deliveryWorld = ground1.GetCellCenterWorld(CellOf(1, 13));

            GameObject startPointObj = new GameObject("PlayerStartPoint");
            startPointObj.transform.position = startWorld;

            GameObject deliveryPointObj = new GameObject("DeliveryPoint");
            deliveryPointObj.transform.position = deliveryWorld;
            Sprite baseSprite = AssetDatabase.LoadAllAssetsAtPath(FloorSheet).OfType<Sprite>().First();
            SpriteRenderer deliveryRenderer = deliveryPointObj.AddComponent<SpriteRenderer>();
            deliveryRenderer.sprite = baseSprite;
            deliveryRenderer.color = new Color(0.2f, 0.9f, 1f, 0.85f);
            deliveryRenderer.sortingOrder = 2;
            deliveryPointObj.transform.localScale = Vector3.one * 0.7f;

            // 3. Player + HandPoint + PlayerMoveRays + PlayerAI
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                SpriteRenderer sr = player.AddComponent<SpriteRenderer>();
                sr.sprite = baseSprite;
                sr.color = Color.black;
                sr.sortingOrder = 5;
                player.AddComponent<CircleCollider2D>().radius = 0.3f;
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
            player.transform.position = startWorld;

            if (player.transform.Find("HandPoint") == null)
            {
                GameObject hp = new GameObject("HandPoint");
                hp.transform.SetParent(player.transform, false);
                hp.transform.localPosition = new Vector3(0.4f, 0.3f, 0f);
            }

            if (!player.TryGetComponent(out PlayerMoveRays _)) player.AddComponent<PlayerMoveRays>();
            if (!player.TryGetComponent(out PlayerStats _)) player.AddComponent<PlayerStats>();
            if (!player.TryGetComponent(out PlayerAI _)) player.AddComponent<PlayerAI>();

            // 4. Item Prefab + ItemData + ItemManager
            ItemData itemData = CreateItemAssets(baseSprite);
            GameObject itemManagerObj = new GameObject("ItemManager");
            ItemManager itemManager = itemManagerObj.AddComponent<ItemManager>();

            SerializedObject imSO = new SerializedObject(itemManager);
            SerializedProperty entries = imSO.FindProperty("spawnEntries");
            entries.ClearArray();
            entries.InsertArrayElementAtIndex(0);
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemData").objectReferenceValue = itemData;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 5;
            imSO.ApplyModifiedPropertiesWithoutUndo();

            // 5. GameTimer + UI Canvas (False God top-left, 15 Skill Boxes in grid below it, 5 Ingredient Boxes bottom-left)
            SetupGameUI(baseSprite);

            // 6. Camera (Black screen, map framed in bottom-right corner with top space for Time UI)
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
                Camera.main.orthographicSize = 8.6f;
                float h = Camera.main.orthographicSize;
                Camera.main.transform.position = new Vector3(9.8f - h * Camera.main.aspect, -7.7f + h, -10f);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            Debug.Log($"[PathTestSceneBuilder] Built full system test in scene '{EditorSceneManager.GetActiveScene().name}'.");
        }

        private static ItemData CreateItemAssets(Sprite sprite)
        {
            string prefabPath = $"{TileFolder}/TestItem.prefab";
            string dataPath = $"{TileFolder}/TestItemData.asset";

            GameObject temp = new GameObject("TestItem");
            SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.85f, 0.1f, 1f);
            sr.sortingOrder = 4;
            temp.transform.localScale = Vector3.one * 0.5f;
            CircleCollider2D col = temp.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            temp.AddComponent<Item>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);

            ItemData data = ScriptableObject.CreateInstance<ItemData>();
            data.itemName = "Candy";
            data.itemPrefab = prefab;
            data.itemSprite = sprite;
            data.gatheredSprite = sprite;
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void SetupGameUI(Sprite placeholderSprite)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 1. False God Design (Top-Left Corner, shifted right)
            GameObject falseGodPanel = CreatePanel(
                canvasObj.transform,
                "FalseGodPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(140f, -40f),
                new Vector2(460f, 300f),
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.75f, 0.2f, 0.25f, 1f));

            GameObject godArtwork = new GameObject("FalseGodDesign");
            godArtwork.transform.SetParent(falseGodPanel.transform, false);
            RectTransform godRt = godArtwork.AddComponent<RectTransform>();
            godRt.anchorMin = new Vector2(0.5f, 0.5f);
            godRt.anchorMax = new Vector2(0.5f, 0.5f);
            godRt.anchoredPosition = new Vector2(0f, 15f);
            godRt.sizeDelta = new Vector2(200f, 200f);
            Image godImg = godArtwork.AddComponent<Image>();
            godImg.sprite = placeholderSprite;
            godImg.color = new Color(0.6f, 0.15f, 0.2f, 1f);

            CreateTMP(falseGodPanel.transform, "FalseGodTitle", new Vector2(0.5f, 0f), new Vector2(0f, 35f), 26, new Color(0.95f, 0.8f, 0.8f), "FALSE GOD");

            // 2. Skills Grid (Under False God Design - 5 chosen skill boxes in a 5x1 grid)
            GameObject skillsGridObj = CreatePanel(
                canvasObj.transform,
                "SkillsGrid",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(140f, -360f),
                new Vector2(460f, 106f),
                new Color(0.05f, 0.05f, 0.07f, 0.9f),
                new Color(0.35f, 0.35f, 0.4f, 1f));

            GridLayoutGroup grid = skillsGridObj.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.cellSize = new Vector2(78f, 78f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 1; i <= 5; i++)
            {
                GameObject box = new GameObject($"SkillBox_{i:00}");
                box.transform.SetParent(skillsGridObj.transform, false);
                Image boxBg = box.AddComponent<Image>();
                boxBg.color = new Color(0.14f, 0.14f, 0.18f, 1f);

                Outline boxOutline = box.AddComponent<Outline>();
                boxOutline.effectColor = new Color(0.5f, 0.5f, 0.58f, 1f);
                boxOutline.effectDistance = new Vector2(2f, -2f);

                Button btn = box.AddComponent<Button>();
                btn.targetGraphic = boxBg;

                GameObject logo = new GameObject("Logo");
                logo.transform.SetParent(box.transform, false);
                RectTransform logoRt = logo.AddComponent<RectTransform>();
                logoRt.anchorMin = Vector2.zero;
                logoRt.anchorMax = Vector2.one;
                logoRt.offsetMin = new Vector2(10f, 10f);
                logoRt.offsetMax = new Vector2(-10f, -10f);
                Image logoImg = logo.AddComponent<Image>();
                logoImg.sprite = placeholderSprite;
                logoImg.color = new Color(0.85f, 0.75f, 0.35f, 0.9f);
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;

                GameObject cooldown = new GameObject("Cooldown");
                cooldown.transform.SetParent(box.transform, false);
                RectTransform cdRt = cooldown.AddComponent<RectTransform>();
                cdRt.anchorMin = Vector2.zero;
                cdRt.anchorMax = Vector2.one;
                cdRt.offsetMin = cdRt.offsetMax = Vector2.zero;
                Image cdImg = cooldown.AddComponent<Image>();
                cdImg.sprite = placeholderSprite;
                cdImg.color = new Color(0f, 0f, 0f, 0.7f);
                cdImg.type = Image.Type.Filled;
                cdImg.fillMethod = Image.FillMethod.Radial360;
                cdImg.fillOrigin = (int)Image.Origin360.Top;
                cdImg.fillClockwise = false;
                cdImg.fillAmount = 0f;
                cdImg.raycastTarget = false;

                TextMeshProUGUI keyTmp = CreateTMP(box.transform, "Key", new Vector2(0f, 1f), new Vector2(4f, -2f), 18, Color.white, i.ToString());
                keyTmp.rectTransform.pivot = new Vector2(0f, 1f);
                keyTmp.rectTransform.sizeDelta = new Vector2(30f, 24f);
                keyTmp.alignment = TextAlignmentOptions.TopLeft;
                keyTmp.raycastTarget = false;

                box.AddComponent<SkillSlotUI>();
            }

            // 3. Skill Explanation Popup (Under the 5 Skill Boxes, extending down until the Ingredients Boxes)
            GameObject popupPanel = CreatePanel(
                canvasObj.transform,
                "SkillPopupPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(140f, -486f),
                new Vector2(460f, 433f),
                new Color(0.08f, 0.08f, 0.11f, 0.95f),
                new Color(0.85f, 0.75f, 0.35f, 0.9f));

            GameObject popupLogoObj = new GameObject("PopupLogo");
            popupLogoObj.transform.SetParent(popupPanel.transform, false);
            RectTransform popupLogoRt = popupLogoObj.AddComponent<RectTransform>();
            popupLogoRt.anchorMin = new Vector2(0.5f, 1f);
            popupLogoRt.anchorMax = new Vector2(0.5f, 1f);
            popupLogoRt.pivot = new Vector2(0.5f, 1f);
            popupLogoRt.anchoredPosition = new Vector2(0f, -20f);
            popupLogoRt.sizeDelta = new Vector2(140f, 140f);
            Image popupLogoImg = popupLogoObj.AddComponent<Image>();
            popupLogoImg.sprite = placeholderSprite;
            popupLogoImg.color = new Color(0.85f, 0.75f, 0.35f, 0.9f);
            popupLogoImg.preserveAspect = true;

            TextMeshProUGUI popupTitle = CreateTMP(popupPanel.transform, "PopupTitle", new Vector2(0.5f, 1f), new Vector2(0f, -172f), 26, new Color(1f, 0.9f, 0.55f), "Skills");
            popupTitle.alignment = TextAlignmentOptions.Top;
            RectTransform titleRt = popupTitle.rectTransform;
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(410f, 36f);

            TextMeshProUGUI popupDesc = CreateTMP(popupPanel.transform, "PopupDescription", new Vector2(0.5f, 1f), new Vector2(0f, -214f), 20, new Color(0.85f, 0.85f, 0.9f), "Hover a skill to see what it does.\nUse your skills with keys 1-5.");
            popupDesc.alignment = TextAlignmentOptions.Top;
            popupDesc.textWrappingMode = TextWrappingModes.Normal;
            RectTransform descRt = popupDesc.rectTransform;
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.sizeDelta = new Vector2(410f, 200f);

            // 3. Gathered Ingredients Panel (Bottom-Left Corner - 5 little boxes)
            GameObject ingredientsPanel = CreatePanel(
                canvasObj.transform,
                "IngredientsPanel",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(140f, 45f),
                new Vector2(460f, 96f),
                new Color(0.05f, 0.05f, 0.07f, 0.9f),
                new Color(0.45f, 0.35f, 0.2f, 1f));

            GridLayoutGroup ingGrid = ingredientsPanel.AddComponent<GridLayoutGroup>();
            ingGrid.padding = new RectOffset(14, 14, 11, 11);
            ingGrid.cellSize = new Vector2(74f, 74f);
            ingGrid.spacing = new Vector2(15f, 0f);
            ingGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            ingGrid.constraintCount = 5;
            ingGrid.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 1; i <= 5; i++)
            {
                GameObject box = new GameObject($"IngredientBox_{i:00}");
                box.transform.SetParent(ingredientsPanel.transform, false);
                Image boxBg = box.AddComponent<Image>();
                boxBg.color = new Color(0.12f, 0.11f, 0.1f, 1f);

                Outline boxOutline = box.AddComponent<Outline>();
                boxOutline.effectColor = new Color(0.65f, 0.5f, 0.25f, 1f);
                boxOutline.effectDistance = new Vector2(2f, -2f);

                GameObject icon = new GameObject("Icon");
                icon.transform.SetParent(box.transform, false);
                RectTransform iconRt = icon.AddComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(8f, 8f);
                iconRt.offsetMax = new Vector2(-8f, -8f);
                Image iconImg = icon.AddComponent<Image>();
                iconImg.sprite = placeholderSprite;
                iconImg.preserveAspect = true;
                iconImg.enabled = false;
            }

            // 5. Level UI (Top-Left corner of the Map) & Timer UI (Top-Right corner of the Map, aligned with False God Y-axis)
            GameObject levelPanel = CreatePanel(
                canvasObj.transform,
                "LevelPanel",
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(-1025f, -40f),
                new Vector2(220f, 62f),
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.75f, 0.2f, 0.25f, 1f));

            TextMeshProUGUI levelTmp = CreateTMP(levelPanel.transform, "LevelText", new Vector2(0.5f, 0.5f), Vector2.zero, 28, Color.white, "LEVEL 1");
            levelTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            levelTmp.alignment = TextAlignmentOptions.Center;

            GameObject timerPanel = CreatePanel(
                canvasObj.transform,
                "TimerPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-210f, -40f),
                new Vector2(260f, 62f),
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.75f, 0.2f, 0.25f, 1f));

            CreateTMP(timerPanel.transform, "TimerText", new Vector2(0.5f, 1f), new Vector2(0f, -4f), 28, Color.white, "02:00");
            CreateTMP(timerPanel.transform, "WarningText", new Vector2(0.5f, 1f), new Vector2(0f, -36f), 17, new Color(1f, 0.4f, 0.2f), "");

            new GameObject("GameTimer").AddComponent<GameTimer>();
            new GameObject("SkillManager").AddComponent<SkillManager>();

            // 6. Skill Choose Page (covers the map until 5 skills are chosen; columns are filled at runtime per skill tree)
            GameObject choosePanel = CreatePanel(
                canvasObj.transform,
                "SkillChoosePanel",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-140f, 0f),
                new Vector2(1000f, 1000f),
                new Color(0.04f, 0.04f, 0.06f, 0.98f),
                new Color(0.75f, 0.2f, 0.25f, 1f));

            CreateTMP(choosePanel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -25f), 40, new Color(1f, 0.9f, 0.55f), "CHOOSE 5 SKILLS");
            TextMeshProUGUI hint = CreateTMP(choosePanel.transform, "Hint", new Vector2(0.5f, 1f), new Vector2(0f, -75f), 20, new Color(0.75f, 0.75f, 0.8f), "Tier 1 is always open. Each next tier needs the one above it.");
            hint.rectTransform.sizeDelta = new Vector2(900f, 30f);

            GameObject trees = new GameObject("Trees");
            trees.transform.SetParent(choosePanel.transform, false);
            RectTransform treesRt = trees.AddComponent<RectTransform>();
            treesRt.anchorMin = Vector2.zero;
            treesRt.anchorMax = Vector2.one;
            treesRt.offsetMin = new Vector2(30f, 170f);
            treesRt.offsetMax = new Vector2(-30f, -120f);
            HorizontalLayoutGroup treesLayout = trees.AddComponent<HorizontalLayoutGroup>();
            treesLayout.spacing = 24f;
            treesLayout.childControlWidth = treesLayout.childControlHeight = true;
            treesLayout.childForceExpandWidth = treesLayout.childForceExpandHeight = true;

            CreateTMP(choosePanel.transform, "CountText", new Vector2(0.5f, 0f), new Vector2(0f, 155f), 26, Color.white, "CHOSEN 0/5");

            GameObject startObj = CreatePanel(
                choosePanel.transform,
                "StartButton",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 25f),
                new Vector2(300f, 64f),
                new Color(0.55f, 0.15f, 0.2f, 1f),
                new Color(1f, 0.9f, 0.55f, 1f));
            startObj.AddComponent<Button>().targetGraphic = startObj.GetComponent<Image>();
            TextMeshProUGUI startTmp = CreateTMP(startObj.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white, "START");
            startTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            startTmp.raycastTarget = false;

            choosePanel.AddComponent<SkillChooseScreen>();

            // 7. Level Won & Level Failed UI Panels (ready for custom UI artwork sprites)
            CreateResultModal(
                canvasObj.transform,
                "LevelWonPanel",
                "LEVEL WON!",
                "The chef ran out of time!",
                "NEXT LEVEL",
                new Color(0.25f, 0.85f, 0.45f, 1f),
                placeholderSprite);

            CreateResultModal(
                canvasObj.transform,
                "LevelFailedPanel",
                "LEVEL FAILED!",
                "The chef gathered all the ingredients!",
                "TRY AGAIN",
                new Color(0.9f, 0.25f, 0.25f, 1f),
                placeholderSprite);
        }

        private static GameObject CreateResultModal(Transform parent, string name, string title, string subtitle, string buttonLabel, Color accentColor, Sprite placeholderSprite)
        {
            GameObject modal = CreatePanel(
                parent,
                name,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-140f, 0f),
                new Vector2(1000f, 1000f),
                new Color(0.04f, 0.04f, 0.06f, 0.96f),
                accentColor);

            GameObject artworkObj = new GameObject("Artwork");
            artworkObj.transform.SetParent(modal.transform, false);
            RectTransform artRt = artworkObj.AddComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.5f, 0.5f);
            artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.anchoredPosition = new Vector2(0f, 90f);
            artRt.sizeDelta = new Vector2(520f, 520f);
            Image artImg = artworkObj.AddComponent<Image>();
            artImg.sprite = placeholderSprite;
            artImg.color = Color.Lerp(accentColor, Color.white, 0.25f);
            artImg.preserveAspect = true;

            CreateTMP(modal.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -50f), 52, accentColor, title);
            TextMeshProUGUI subTmp = CreateTMP(modal.transform, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, 200f), 26, new Color(0.85f, 0.85f, 0.9f), subtitle);
            subTmp.rectTransform.sizeDelta = new Vector2(800f, 45f);

            GameObject actionBtn = CreatePanel(
                modal.transform,
                "ActionButton",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 55f),
                new Vector2(340f, 76f),
                new Color(accentColor.r * 0.45f, accentColor.g * 0.45f, accentColor.b * 0.45f, 1f),
                accentColor);
            actionBtn.AddComponent<Button>().targetGraphic = actionBtn.GetComponent<Image>();

            TextMeshProUGUI btnTmp = CreateTMP(actionBtn.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 32, Color.white, buttonLabel);
            btnTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            btnTmp.raycastTarget = false;

            modal.SetActive(false);
            return modal;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bgColor, Color borderColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = panel.AddComponent<Image>();
            img.color = bgColor;

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, float fontSize, Color color, string text)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 50f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.text = text;
            return tmp;
        }

        private static TileBase LoadTile(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

        private static (Tilemap ground, Tilemap walls) NewBaseLevel(Transform parent, string name, string outerPath)
        {
            GameObject level = new GameObject(name);
            level.transform.SetParent(parent, false);
            level.AddComponent<Grid>();
            Tilemap ground = NewTilemap(level, "Ground", 0);
            Tilemap walls = NewTilemap(level, "Walls", 1);
            level.AddComponent<PathCreator>();

            PaintSheet(FloorSheet, ground, false);
            PaintSheet(outerPath, walls, false);
            return (ground, walls);
        }

        private static void BuildCustomLevel(Transform parent, string name, string outerPath, string[] layout, Dictionary<char, TileBase> tiles)
        {
            (Tilemap ground, Tilemap walls) = NewBaseLevel(parent, name, outerPath);
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    if (tiles.TryGetValue(layout[r][c], out TileBase tile))
                    {
                        walls.SetTile(CellOf(c, r), tile);
                    }
                }
            }
            ground.GetComponentInParent<PathCreator>().ScanMap();
        }

        private static Vector3Int CellOf(int col, int row) => new Vector3Int(col - Size / 2, Size / 2 - 1 - row, 0);

        private static void Slice(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.spriteImportMode == SpriteImportMode.Multiple && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == Size * Size) return;

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            string sheet = Path.GetFileNameWithoutExtension(path);
            SpriteRect[] rects = Enumerable.Range(0, Size * Size).Select(i => new SpriteRect
            {
                name = $"{sheet}_{i % Size}_{i / Size}",
                spriteID = GUID.Generate(),
                rect = new Rect(i % Size * Cell, (Size - 1 - i / Size) * Cell, Cell, Cell),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void PaintSheet(string path, Tilemap target, bool barriers)
        {
            string sheet = Path.GetFileNameWithoutExtension(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            Dictionary<int, TileBase> tiles = new Dictionary<int, TileBase>();

            for (int i = 0; i < Size * Size; i++)
            {
                int col = i % Size, row = i / Size;
                Color[] px = tex.GetPixels(col * Cell, (Size - 1 - row) * Cell, Cell, Cell);
                if (px.All(p => p.a < 0.08f)) continue;

                System.HashCode hash = new System.HashCode();
                foreach (Color p in px) hash.Add(p);

                string name = $"{sheet}_{col}_{row}";
                if (!tiles.TryGetValue(hash.ToHashCode(), out TileBase tile))
                {
                    tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset")
                           ?? NewTile($"{TileFolder}/{name}.asset", sprites[name], barriers ? px : null);
                    tiles[hash.ToHashCode()] = tile;
                }

                target.SetTile(CellOf(col, row), tile);
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
        }

        private static TileBase NewTile(string path, Sprite sprite, Color[] barrierPixels)
        {
            Tile tile = barrierPixels != null ? ScriptableObject.CreateInstance<BarrierTile>() : ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;

            if (barrierPixels != null)
            {
                SerializedObject so = new SerializedObject(tile);
                so.FindProperty("blockedSides").intValue = (int)DetectSides(barrierPixels);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static TileSides DetectSides(Color[] px)
        {
            TileSides sides = TileSides.None;
            for (int i = 0; i < 4; i++)
            {
                Vector3Int d = Sides.Dir[i];
                int count = px.Where((p, k) => p.a >= 0.08f && d.x * (k % Cell - 7.5f) + d.y * (k / Cell - 7.5f) > 2.5f).Count();
                if (count >= EdgeMinPixels) sides |= (TileSides)(1 << i);
            }
            return sides;
        }

        private static Tilemap NewTilemap(GameObject parent, string name, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Tilemap tm = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tm;
        }
    }
}
