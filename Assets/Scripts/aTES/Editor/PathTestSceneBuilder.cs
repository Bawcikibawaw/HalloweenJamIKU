using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using aTES;

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
        private const string FloorSheet = "Assets/Sprites/Final Tilemaps/GAME bc.png";
        private const string OuterWallSheet = "Assets/Sprites/Final Tilemaps/GAME duvar.png";
        private const string InnerWallAtlas = "Assets/Sprites/Final Tilemaps/TAYIL SET.png";
        private const string CharsSheet = "Assets/Sprites/chars.png";
        private const string TileFolder = "Assets/Sprites/Tilemaps/Generated";
        private const int Cell = 32;
        private const int Size = 18;
        private const int InnerSize = 16;

        // 9 Levels (16x16 interior grids inside the 18x18 outer wall frame):
        // Level 1 = Sketch 3 (GAME map 1), Level 2 = Sketch 1, Level 3 = Sketch 2 (Heart), Levels 4-9 = Custom Mazes
        private static readonly string[][] Levels =
        {
            // Level 1 (Sketch 3 / GAME map 1)
            new[]
            {
                "........##.#....",
                ".####.#....#.###",
                "....#...#..#.#..",
                ".##.#..##.......",
                "............###.",
                "##.#.######.#...",
                "###..#....#.#.##",
                ".......##...#...",
                ".###...##......#",
                ".....#....#.####",
                ".#...######.#..#",
                ".#..............",
                ".#.##.###.#..###",
                ".#........#.....",
                ".########.#.##..",
                "...............#",
            },
            // Level 2 (Sketch 1 - Scattered Blocks)
            new[]
            {
                "..##.....###..#.",
                "..##.####.....#.",
                "..##....#.####.#",
                "..##......####.#",
                "..##..#.......##",
                ".....##..###....",
                ".....##..###.##.",
                ".##........#.###",
                ".##..###......##",
                ".........####...",
                ".####..........#",
                ".####.....###.##",
                "..........##....",
                ".##....##.##.###",
                ".#..#..##.##.###",
                "...##.....##....",
            },
            // Level 3 (Sketch 2 - The Cursed Heart)
            new[]
            {
                "...#####........",
                ".##....#.#.#.##.",
                ".#..###..#...#..",
                "..............##",
                "..#..##...##....",
                "#.##.###.###.###",
                "#.....#####.....",
                "#..###.###...###",
                "#.......#....#..",
                "...#.#......###.",
                "##.###....#.....",
                "##.....#......##",
                "....####.#.####.",
                ".#.....#.#.#..#.",
                ".##..##....#.##.",
                "........##......",
            },
            // Level 4 (Double Spiral Labyrinth)
            new[]
            {
                "................",
                ".######..######.",
                ".#............#.",
                ".#.####..####.#.",
                ".#.#........#.#.",
                ".#.#.##..##.#.#.",
                ".#.#.#....#.#.#.",
                "...#.#.##.#.#...",
                "...#.#.##.#.#...",
                ".#.#.#....#.#.#.",
                ".#.#.##..##.#.#.",
                ".#.#........#.#.",
                ".#.####..####.#.",
                ".#............#.",
                ".######..######.",
                "................",
            },
            // Level 5 (The Skull Chamber)
            new[]
            {
                "..###......###..",
                ".#............#.",
                ".#...######...#.",
                "...#########....",
                ".#.###.##.###.#.",
                ".#.###.##.###.#.",
                ".#...######...#.",
                ".....#.##.#.....",
                ".###...##...###.",
                ".#............#.",
                ".#.###.##.###.#.",
                "...#...##...#...",
                ".###.######.###.",
                ".#............#.",
                "..####....####..",
                "................",
            },
            // Level 6 (Crossroad Citadel)
            new[]
            {
                "...#........#...",
                ".###.######.###.",
                ".#.....##.....#.",
                ".#.###.##.###.#.",
                "...#........#...",
                ".###.##..##.###.",
                ".....##..##.....",
                ".###........###.",
                ".###........###.",
                ".....##..##.....",
                ".###.##..##.###.",
                "...#........#...",
                ".#.###.##.###.#.",
                ".#.....##.....#.",
                ".###.######.###.",
                "...#........#...",
            },
            // Level 7 (The Hourglass)
            new[]
            {
                "..############..",
                "................",
                ".##.########.##.",
                ".##..######..##.",
                ".#....####....#.",
                ".#.....##.....#.",
                "...##......##...",
                ".####..##..####.",
                ".####..##..####.",
                "...##......##...",
                ".#.....##.....#.",
                ".#....####....#.",
                ".##..######..##.",
                ".##.########.##.",
                "................",
                "..############..",
            },
            // Level 8 (Serpent Columns)
            new[]
            {
                "................",
                ".##..##..##..##.",
                ".##..##..##..##.",
                ".....##......##.",
                ".##..##..##..##.",
                ".##......##.....",
                ".##..##..##..##.",
                ".##..##..##..##.",
                ".....##......##.",
                ".##..##..##..##.",
                ".##......##.....",
                ".##..##..##..##.",
                ".....##......##.",
                ".##..##..##..##.",
                ".##..##..##..##.",
                "................",
            },
            // Level 9 (Final Level - The Pizza Oven Plaza: large 8x6 central space at cols 5..12, rows 6..11)
            new[]
            {
                "..###......###..",
                ".#............#.",
                ".#.###.##.###.#.",
                "...#........#...",
                ".###.##..##.###.",
                "................",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                "................",
                ".###.##..##.###.",
                "...#........#...",
                ".#.###.##.###.#.",
                ".#............#.",
                "..###......###..",
            },
        };

        // Per-level (StartPoint, DeliveryPoint) in 1..16 inner floor coordinates (col, row):
        private static readonly (Vector2Int start, Vector2Int delivery)[] LevelPoints =
        {
            (new Vector2Int(1, 1),   new Vector2Int(1, 16)),  // Level 1: Top-Left -> Bottom-Left
            (new Vector2Int(1, 1),   new Vector2Int(16, 16)), // Level 2: Top-Left -> Bottom-Right
            (new Vector2Int(16, 1),  new Vector2Int(1, 16)),  // Level 3: Top-Right -> Bottom-Left
            (new Vector2Int(1, 1),   new Vector2Int(7, 7)),   // Level 4: Outer Corner -> Spiral Center
            (new Vector2Int(8, 16),  new Vector2Int(8, 2)),   // Level 5: Skull Jaw -> Skull Crown
            (new Vector2Int(1, 8),   new Vector2Int(16, 9)),  // Level 6: West Gate -> East Gate
            (new Vector2Int(8, 2),   new Vector2Int(8, 15)),  // Level 7: Top Hourglass -> Bottom Hourglass
            (new Vector2Int(1, 16),  new Vector2Int(16, 1)),  // Level 8: Bottom-Left -> Top-Right
            (new Vector2Int(8, 8),   new Vector2Int(8, 8)),   // Level 9 (Final): Center Pizza Oven Plaza (both spawn & gather)
        };

        [MenuItem("Tools/aTES/Build Path Test Scene")]
        public static void Build()
        {
            foreach (string sheet in new[] { FloorSheet, OuterWallSheet }) Slice(sheet);

            AssetDatabase.DeleteAsset(TileFolder);
            Directory.CreateDirectory(TileFolder);
            AssetDatabase.Refresh();

            // Clean up previous scene objects
            foreach (string name in new[] { "Grid", "Levels", "PlayerStartPoint", "DeliveryPoint", "ItemManager", "GameTimer", "SkillManager", "Canvas", "EventSystem" })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            // 1. Levels + LevelManager + PathCreator (9 levels using Final Tilemaps)
            GameObject levelsRoot = new GameObject("Levels");
            LevelManager levelManager = levelsRoot.AddComponent<LevelManager>();

            Dictionary<int, TileBase> innerTiles = BuildInnerWallTiles(InnerWallAtlas);
            Tilemap ground1 = null;

            for (int i = 0; i < Levels.Length; i++)
            {
                (Vector2Int start, Vector2Int delivery) = LevelPoints[i];
                Tilemap g = BuildCustomLevel(levelsRoot.transform, $"Level {i + 1}", OuterWallSheet, Levels[i], innerTiles, start, delivery);
                if (i == 0) ground1 = g;
            }

            // 2. Start & Delivery Points
            Sprite[] stuff = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff.png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            Sprite[] stuff3 = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff3-(stas work).png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            Sprite baseSprite = AssetDatabase.LoadAllAssetsAtPath(FloorSheet).OfType<Sprite>().First(s => s.name.EndsWith("_1_1"));

            Vector3 startWorld = ground1.GetCellCenterWorld(CellOf(LevelPoints[0].start.x, LevelPoints[0].start.y));
            Vector3 deliveryWorld = ground1.GetCellCenterWorld(CellOf(LevelPoints[0].delivery.x, LevelPoints[0].delivery.y));

            GameObject startPointObj = new GameObject("PlayerStartPoint");
            startPointObj.transform.position = startWorld;

            GameObject deliveryPointObj = new GameObject("DeliveryPoint");
            deliveryPointObj.transform.position = deliveryWorld;
            SpriteRenderer deliveryRenderer = deliveryPointObj.AddComponent<SpriteRenderer>();
            deliveryRenderer.sprite = stuff3.Length > 5 ? stuff3[5] : (stuff.Length > 3 ? stuff[3] : baseSprite);
            deliveryRenderer.color = Color.white;
            deliveryRenderer.sortingOrder = 2;
            deliveryPointObj.transform.localScale = Vector3.one;

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
            levelManager.Show(0);

            if (player.transform.Find("HandPoint") == null)
            {
                GameObject hp = new GameObject("HandPoint");
                hp.transform.SetParent(player.transform, false);
                hp.transform.localPosition = new Vector3(0.4f, 0.3f, 0f);
            }

            if (!player.TryGetComponent(out PlayerMoveRays _)) player.AddComponent<PlayerMoveRays>();
            if (!player.TryGetComponent(out PlayerStats _)) player.AddComponent<PlayerStats>();
            if (!player.TryGetComponent(out PlayerAI _)) player.AddComponent<PlayerAI>();

            // 4. Item Prefab + ItemData (from chars.png) + ItemManager
            List<ItemData> itemDatas = CreateItemAssets(baseSprite);
            GameObject itemManagerObj = new GameObject("ItemManager");
            ItemManager itemManager = itemManagerObj.AddComponent<ItemManager>();

            SerializedObject imSO = new SerializedObject(itemManager);
            imSO.FindProperty("itemsPerLevel").intValue = 5;
            SerializedProperty entries = imSO.FindProperty("spawnEntries");
            entries.ClearArray();
            for (int i = 0; i < itemDatas.Count; i++)
            {
                entries.InsertArrayElementAtIndex(i);
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("itemData").objectReferenceValue = itemDatas[i];
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = 1;
            }
            imSO.ApplyModifiedPropertiesWithoutUndo();

            // 5. GameTimer + UI Canvas (False God top-left, 15 Skill Boxes in grid below it, 5 Ingredient Boxes bottom-left)
            SetupGameUI(baseSprite, stuff, stuff3);

            // 6. Camera (Black screen, map shifted slightly left & up so its bottom aligns with IngredientsPanel)
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
                Camera.main.orthographicSize = 9.9f;
                float h = Camera.main.orthographicSize;
                Camera.main.transform.position = new Vector3(11.6f - h * Camera.main.aspect, -9.5f + h, -10f);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            Debug.Log($"[PathTestSceneBuilder] Built full system test in scene '{EditorSceneManager.GetActiveScene().name}'.");
        }

        private static List<ItemData> CreateItemAssets(Sprite fallbackSprite)
        {
            Sprite[] charSprites = AssetDatabase.LoadAllAssetsAtPath(CharsSheet)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (charSprites.Length == 0)
                charSprites = new[] { fallbackSprite };

            string prefabPath = $"{TileFolder}/TestItem.prefab";

            GameObject temp = new GameObject("TestItem");
            SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = charSprites[0];
            sr.color = Color.white;
            sr.sortingOrder = 4;
            temp.transform.localScale = Vector3.one * 0.5f;
            CircleCollider2D col = temp.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            temp.AddComponent<Item>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);

            List<ItemData> result = new List<ItemData>();
            for (int i = 0; i < charSprites.Length; i++)
            {
                ItemData data = ScriptableObject.CreateInstance<ItemData>();
                data.itemName = charSprites[i].name;
                data.itemPrefab = prefab;
                data.itemSprite = charSprites[i];
                data.gatheredSprite = charSprites[i];
                AssetDatabase.CreateAsset(data, $"{TileFolder}/Ingredient_{i:00}.asset");
                result.Add(data);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void SetupGameUI(Sprite placeholderSprite, Sprite[] stuff, Sprite[] stuff3)
        {
            Sprite[] stuff2 = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff2.png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            Sprite panelSprite = stuff.Length > 0 ? stuff[0] : placeholderSprite;
            Sprite leftPanelSprite = stuff2.Length > 0 ? stuff2[0] : panelSprite;

            // stuff3 sprite mapping:
            // 0: Skill Choose Panel
            // 1: Pause Panel
            // 2: Gathered Item Box
            // 3: Chosen Skill Box
            // 4: Gathered Item Background Panel
            // 5: Gathering Spot (DeliveryPoint)
            // 6: Cursor
            // 7: Chosen Skill Background Panel
            // 8: Skill Description Background Panel
            // 9: False Gods Background Panel
            Sprite choosePanelSprite = stuff3.Length > 0 ? stuff3[0] : panelSprite;
            Sprite pausePanelSprite = stuff3.Length > 1 ? stuff3[1] : panelSprite;
            Sprite gatheredBoxSprite = stuff3.Length > 2 ? stuff3[2] : (stuff.Length > 1 ? stuff[1] : placeholderSprite);
            Sprite skillBoxSprite = stuff3.Length > 3 ? stuff3[3] : (stuff.Length > 2 ? stuff[2] : placeholderSprite);
            Sprite gatheredPanelSprite = stuff3.Length > 4 ? stuff3[4] : leftPanelSprite;
            Sprite cursorSprite = stuff3.Length > 6 ? stuff3[6] : (stuff.Length > 4 ? stuff[4] : null);
            Sprite chosenSkillPanelSprite = stuff3.Length > 7 ? stuff3[7] : leftPanelSprite;
            Sprite skillPopupPanelSprite = stuff3.Length > 8 ? stuff3[8] : leftPanelSprite;
            Sprite falseGodPanelSprite = stuff3.Length > 9 ? stuff3[9] : leftPanelSprite;
            Sprite buttonSprite = stuff.Length > 5 ? stuff[5] : placeholderSprite;

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

            // 1. False God Design (Top-Left Corner, uses stuff3_9 panel background)
            GameObject falseGodPanel = CreatePanel(
                canvasObj.transform,
                "FalseGodPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(140f, -40f),
                new Vector2(460f, 300f),
                falseGodPanelSprite);

            // 2. Skills Grid (Under False God Design - 5 chosen skill boxes in a 5x1 grid using stuff3_7 background & stuff3_3 boxes)
            GameObject skillsGridObj = CreatePanel(
                canvasObj.transform,
                "SkillsGrid",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(140f, -352f),
                new Vector2(460f, 118f),
                chosenSkillPanelSprite);

            GridLayoutGroup grid = skillsGridObj.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(28, 28, 22, 22);
            grid.cellSize = new Vector2(72f, 72f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 1; i <= 5; i++)
            {
                GameObject box = new GameObject($"SkillBox_{i:00}");
                box.transform.SetParent(skillsGridObj.transform, false);
                Image boxBg = box.AddComponent<Image>();
                boxBg.sprite = skillBoxSprite;
                boxBg.color = Color.white;

                Button btn = box.AddComponent<Button>();
                btn.targetGraphic = boxBg;

                GameObject logo = new GameObject("Logo");
                logo.transform.SetParent(box.transform, false);
                RectTransform logoRt = logo.AddComponent<RectTransform>();
                logoRt.anchorMin = Vector2.zero;
                logoRt.anchorMax = Vector2.one;
                logoRt.offsetMin = new Vector2(6f, 6f);
                logoRt.offsetMax = new Vector2(-6f, -6f);
                Image logoImg = logo.AddComponent<Image>();
                logoImg.color = new Color(0.85f, 0.75f, 0.35f, 0.9f);
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;

                GameObject cooldown = new GameObject("Cooldown");
                cooldown.transform.SetParent(box.transform, false);
                RectTransform cdRt = cooldown.AddComponent<RectTransform>();
                cdRt.anchorMin = Vector2.zero;
                cdRt.anchorMax = Vector2.one;
                cdRt.offsetMin = new Vector2(6f, 6f);
                cdRt.offsetMax = new Vector2(-6f, -6f);
                Image cdImg = cooldown.AddComponent<Image>();
                cdImg.sprite = placeholderSprite;
                cdImg.color = new Color(0f, 0f, 0f, 0.7f);
                cdImg.type = Image.Type.Filled;
                cdImg.fillMethod = Image.FillMethod.Radial360;
                cdImg.fillOrigin = (int)Image.Origin360.Top;
                cdImg.fillClockwise = false;
                cdImg.fillAmount = 0f;
                cdImg.raycastTarget = false;

                TextMeshProUGUI keyTmp = CreateTMP(box.transform, "Key", new Vector2(0f, 1f), new Vector2(6f, -4f), 16, Color.white, i.ToString());
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
                new Vector2(140f, -482f),
                new Vector2(460f, 442f),
                skillPopupPanelSprite);

            GameObject popupLogoObj = new GameObject("PopupLogo");
            popupLogoObj.transform.SetParent(popupPanel.transform, false);
            RectTransform popupLogoRt = popupLogoObj.AddComponent<RectTransform>();
            popupLogoRt.anchorMin = new Vector2(0.5f, 1f);
            popupLogoRt.anchorMax = new Vector2(0.5f, 1f);
            popupLogoRt.pivot = new Vector2(0.5f, 1f);
            popupLogoRt.anchoredPosition = new Vector2(0f, -14f);
            popupLogoRt.sizeDelta = new Vector2(140f, 140f);
            Image popupLogoImg = popupLogoObj.AddComponent<Image>();
            popupLogoImg.color = new Color(0.85f, 0.75f, 0.35f, 0.9f);
            popupLogoImg.preserveAspect = true;

            TextMeshProUGUI popupTitle = CreateTMP(popupPanel.transform, "PopupTitle", new Vector2(0.5f, 1f), new Vector2(0f, -160f), 30, new Color(1f, 0.9f, 0.55f), "Skills");
            popupTitle.alignment = TextAlignmentOptions.Top;
            RectTransform titleRt = popupTitle.rectTransform;
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(400f, 36f);

            GameObject scrollObj = new GameObject("DescScrollView");
            scrollObj.transform.SetParent(popupPanel.transform, false);
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

            TextMeshProUGUI popupDesc = CreateTMP(scrollObj.transform, "PopupDescription", new Vector2(0.5f, 1f), Vector2.zero, 24, new Color(0.95f, 0.95f, 0.95f), "Hover a skill to see what it does.\n\nUse your skills with keys 1-5.");
            popupDesc.alignment = TextAlignmentOptions.Top;
            popupDesc.textWrappingMode = TextWrappingModes.Normal;
            popupDesc.enableAutoSizing = false;
            popupDesc.lineSpacing = 0f;
            popupDesc.paragraphSpacing = 10f;
            RectTransform descRt = popupDesc.rectTransform;
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.offsetMin = new Vector2(10f, 0f);
            descRt.offsetMax = new Vector2(-20f, 0f);

            ContentSizeFitter fitter = popupDesc.gameObject.AddComponent<ContentSizeFitter>();
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

            // 4. Gathered Ingredients Panel (Bottom-Left Corner - 5 little boxes using stuff3_4 & stuff3_2)
            GameObject ingredientsPanel = CreatePanel(
                canvasObj.transform,
                "IngredientsPanel",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(140f, 35f),
                new Vector2(460f, 114f),
                gatheredPanelSprite);

            GridLayoutGroup ingGrid = ingredientsPanel.AddComponent<GridLayoutGroup>();
            ingGrid.padding = new RectOffset(28, 28, 20, 20);
            ingGrid.cellSize = new Vector2(72f, 72f);
            ingGrid.spacing = new Vector2(10f, 0f);
            ingGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            ingGrid.constraintCount = 5;
            ingGrid.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 1; i <= 5; i++)
            {
                GameObject box = new GameObject($"IngredientBox_{i:00}");
                box.transform.SetParent(ingredientsPanel.transform, false);
                Image boxBg = box.AddComponent<Image>();
                boxBg.sprite = gatheredBoxSprite;
                boxBg.color = Color.white;

                GameObject icon = new GameObject("Icon");
                icon.transform.SetParent(box.transform, false);
                RectTransform iconRt = icon.AddComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(10f, 10f);
                iconRt.offsetMax = new Vector2(-10f, -10f);
                Image iconImg = icon.AddComponent<Image>();
                iconImg.sprite = placeholderSprite;
                iconImg.preserveAspect = true;
                iconImg.enabled = false;
            }

            // 5. Level UI (Top-Left corner of the Map) & Timer UI (Top-Right corner of the Map)
            GameObject levelPanel = CreatePanel(
                canvasObj.transform,
                "LevelPanel",
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(-1120f, -6f),
                new Vector2(210f, 56f),
                panelSprite);

            TextMeshProUGUI levelTmp = CreateTMP(levelPanel.transform, "LevelText", new Vector2(0.5f, 0.5f), Vector2.zero, 22, Color.white, "LEVEL 1");
            levelTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            levelTmp.alignment = TextAlignmentOptions.Center;

            GameObject timerPanel = CreatePanel(
                canvasObj.transform,
                "TimerPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-142f, -6f),
                new Vector2(230f, 56f),
                panelSprite);

            CreateTMP(timerPanel.transform, "TimerText", new Vector2(0.5f, 1f), new Vector2(0f, -6f), 22, Color.white, "02:00");
            CreateTMP(timerPanel.transform, "WarningText", new Vector2(0.5f, 1f), new Vector2(0f, -32f), 13, new Color(1f, 0.4f, 0.2f), "");

            new GameObject("GameTimer").AddComponent<GameTimer>();
            SkillManager skillManager = new GameObject("SkillManager").AddComponent<SkillManager>();
            SerializedObject smSO = new SerializedObject(skillManager);
            AssignSprites(smSO.FindProperty("curseIcons"), "Assets/Sprites/CurseTier.png");
            AssignSprites(smSO.FindProperty("trickeryIcons"), "Assets/Sprites/Tier1.png");
            AssignSprites(smSO.FindProperty("chaosIcons"), "Assets/Sprites/ChaosTier.png", 0, 4, 1, 2, 3);
            smSO.FindProperty("cursorSprite").objectReferenceValue = cursorSprite;
            smSO.FindProperty("buttonSprite").objectReferenceValue = buttonSprite;
            smSO.ApplyModifiedPropertiesWithoutUndo();

            // 6. Skill Choose Page (covers the map until 5 skills are chosen; columns are filled at runtime per skill tree)
            GameObject choosePanel = CreatePanel(
                canvasObj.transform,
                "SkillChoosePanel",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-140f, 0f),
                new Vector2(1000f, 1000f),
                choosePanelSprite);

            CreateTMP(choosePanel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -65f), 40, new Color(1f, 0.9f, 0.55f), "CHOOSE 5 SKILLS");
            TextMeshProUGUI hint = CreateTMP(choosePanel.transform, "Hint", new Vector2(0.5f, 1f), new Vector2(0f, -115f), 20, new Color(0.9f, 0.9f, 0.95f), "Tier 1 is always open. Each next tier needs the one above it.");
            hint.rectTransform.sizeDelta = new Vector2(820f, 30f);

            GameObject trees = new GameObject("Trees");
            trees.transform.SetParent(choosePanel.transform, false);
            RectTransform treesRt = trees.AddComponent<RectTransform>();
            treesRt.anchorMin = Vector2.zero;
            treesRt.anchorMax = Vector2.one;
            treesRt.offsetMin = new Vector2(90f, 220f);
            treesRt.offsetMax = new Vector2(-90f, -155f);
            HorizontalLayoutGroup treesLayout = trees.AddComponent<HorizontalLayoutGroup>();
            treesLayout.spacing = 20f;
            treesLayout.childControlWidth = treesLayout.childControlHeight = true;
            treesLayout.childForceExpandWidth = treesLayout.childForceExpandHeight = true;

            CreateTMP(choosePanel.transform, "CountText", new Vector2(0.5f, 0f), new Vector2(0f, 195f), 26, Color.white, "CHOSEN 0/5");

            GameObject startObj = CreatePanel(
                choosePanel.transform,
                "StartButton",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 75f),
                new Vector2(310f, 80f),
                buttonSprite);
            startObj.AddComponent<Button>().targetGraphic = startObj.GetComponent<Image>();
            TextMeshProUGUI startTmp = CreateTMP(startObj.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white, "START");
            startTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            startTmp.raycastTarget = false;

            choosePanel.AddComponent<SkillChooseScreen>();

            // 7. Pause Game Menu Panel (uses stuff3_1 sprite)
            GameObject pausePanel = CreatePanel(
                canvasObj.transform,
                "PausePanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(480f, 680f),
                pausePanelSprite);

            CreateTMP(pausePanel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -80f), 42, new Color(1f, 0.9f, 0.55f), "PAUSED");

            Action<string, string, float> makePauseBtn = (bName, bText, yPos) =>
            {
                GameObject bObj = CreatePanel(
                    pausePanel.transform,
                    bName,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, yPos),
                    new Vector2(300f, 75f),
                    buttonSprite);
                bObj.AddComponent<Button>().targetGraphic = bObj.GetComponent<Image>();
                TextMeshProUGUI bTmp = CreateTMP(bObj.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 28, Color.white, bText);
                bTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                bTmp.raycastTarget = false;
            };

            makePauseBtn("ResumeButton", "RESUME", 50f);
            makePauseBtn("RestartButton", "RESTART", -50f);
            makePauseBtn("QuitButton", "QUIT", -150f);

            pausePanel.SetActive(false);
            canvasObj.AddComponent<PauseMenu>();

            // 8. Level Won & Level Failed UI Panels
            CreateResultModal(
                canvasObj.transform,
                "LevelWonPanel",
                "LEVEL WON!",
                "The chef ran out of time!",
                "NEXT LEVEL",
                new Color(0.25f, 0.85f, 0.45f, 1f),
                panelSprite,
                buttonSprite,
                placeholderSprite);

            CreateResultModal(
                canvasObj.transform,
                "LevelFailedPanel",
                "LEVEL FAILED!",
                "The chef gathered all the ingredients!",
                "TRY AGAIN",
                new Color(0.9f, 0.25f, 0.25f, 1f),
                panelSprite,
                buttonSprite,
                placeholderSprite);
        }

        private static GameObject CreateResultModal(Transform parent, string name, string title, string subtitle, string buttonLabel, Color accentColor, Sprite panelSprite, Sprite buttonSprite, Sprite placeholderSprite)
        {
            GameObject modal = CreatePanel(
                parent,
                name,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-140f, 0f),
                new Vector2(1000f, 1000f),
                panelSprite);

            GameObject artworkObj = new GameObject("Artwork");
            artworkObj.transform.SetParent(modal.transform, false);
            RectTransform artRt = artworkObj.AddComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.5f, 0.5f);
            artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.anchoredPosition = new Vector2(0f, 75f);
            artRt.sizeDelta = new Vector2(440f, 440f);
            Image artImg = artworkObj.AddComponent<Image>();
            artImg.sprite = placeholderSprite;
            artImg.color = Color.Lerp(accentColor, Color.white, 0.25f);
            artImg.preserveAspect = true;

            CreateTMP(modal.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -85f), 50, accentColor, title);
            TextMeshProUGUI subTmp = CreateTMP(modal.transform, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, 225f), 26, new Color(0.95f, 0.95f, 0.95f), subtitle);
            subTmp.rectTransform.sizeDelta = new Vector2(760f, 45f);

            GameObject actionBtn = CreatePanel(
                modal.transform,
                "ActionButton",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 85f),
                new Vector2(340f, 86f),
                buttonSprite);
            actionBtn.AddComponent<Button>().targetGraphic = actionBtn.GetComponent<Image>();

            TextMeshProUGUI btnTmp = CreateTMP(actionBtn.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white, buttonLabel);
            btnTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            btnTmp.raycastTarget = false;

            modal.SetActive(false);
            return modal;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite)
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
            img.sprite = sprite;
            img.color = Color.white;
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
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MesseDuesseldorf-O1d3 SDF.asset");
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.text = text;
            return tmp;
        }

        private static void AssignSprites(SerializedProperty arrayProp, string assetPath, params int[] order)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.rect.x)
                .ToArray();
            if (order != null && order.Length == sprites.Length)
                sprites = order.Select(idx => sprites[idx]).ToArray();

            arrayProp.ClearArray();
            for (int i = 0; i < sprites.Length; i++)
            {
                arrayProp.InsertArrayElementAtIndex(i);
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        private static Dictionary<int, TileBase> BuildInnerWallTiles(string atlasPath)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, Cell) || importer.filterMode != FilterMode.Point)
            {
                importer.spritePixelsPerUnit = Cell;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(atlasPath));

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
            Dictionary<int, TileBase> result = new Dictionary<int, TileBase>();

            foreach (Sprite s in sprites)
            {
                Rect r = s.rect;
                int rx = Mathf.RoundToInt(r.x);
                int ry = Mathf.RoundToInt(r.y);

                if (tex.GetPixel(rx + 16, ry + 16).a < 0.5f) continue;

                bool topBorder = tex.GetPixel(rx + 16, ry + 30).r < 0.35f;
                bool bottomBorder = tex.GetPixel(rx + 16, ry + 1).r < 0.35f;
                bool leftBorder = tex.GetPixel(rx + 1, ry + 16).r < 0.35f;
                bool rightBorder = tex.GetPixel(rx + 30, ry + 16).r < 0.35f;

                int mask = (topBorder ? 1 : 0) | (bottomBorder ? 2 : 0) | (leftBorder ? 4 : 0) | (rightBorder ? 8 : 0);
                if (!result.ContainsKey(mask))
                {
                    result[mask] = NewTile($"{TileFolder}/InnerWall_{mask:00}.asset", s);
                }
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static (Tilemap ground, Tilemap walls) NewBaseLevel(Transform parent, string name, string outerPath)
        {
            GameObject level = new GameObject(name);
            level.transform.SetParent(parent, false);
            level.AddComponent<Grid>();
            Tilemap ground = NewTilemap(level, "Ground", 0);
            Tilemap walls = NewTilemap(level, "Walls", 1);
            level.AddComponent<PathCreator>();

            PaintSheet(FloorSheet, ground);
            PaintSheet(outerPath, walls);
            return (ground, walls);
        }

        private static Tilemap BuildCustomLevel(Transform parent, string name, string outerPath, string[] layout, Dictionary<int, TileBase> innerTiles, Vector2Int start, Vector2Int delivery)
        {
            (Tilemap ground, Tilemap walls) = NewBaseLevel(parent, name, outerPath);
            for (int r = 0; r < InnerSize; r++)
            {
                for (int c = 0; c < InnerSize; c++)
                {
                    if (layout[r][c] != '#') continue;

                    bool hasUp = r > 0 && layout[r - 1][c] == '#';
                    bool hasDown = r < InnerSize - 1 && layout[r + 1][c] == '#';
                    bool hasLeft = c > 0 && layout[r][c - 1] == '#';
                    bool hasRight = c < InnerSize - 1 && layout[r][c + 1] == '#';

                    int borderMask = (!hasUp ? 1 : 0) | (!hasDown ? 2 : 0) | (!hasLeft ? 4 : 0) | (!hasRight ? 8 : 0);
                    if (innerTiles.TryGetValue(borderMask, out TileBase tile))
                    {
                        walls.SetTile(CellOf(c + 1, r + 1), tile);
                    }
                }
            }

            Transform levelRoot = ground.transform.parent;
            GameObject sp = new GameObject("StartPoint");
            sp.transform.SetParent(levelRoot, false);
            sp.transform.position = ground.GetCellCenterWorld(CellOf(start.x, start.y));

            GameObject dp = new GameObject("DeliveryPoint");
            dp.transform.SetParent(levelRoot, false);
            dp.transform.position = ground.GetCellCenterWorld(CellOf(delivery.x, delivery.y));

            ground.GetComponentInParent<PathCreator>().ScanMap();
            return ground;
        }

        private static Vector3Int CellOf(int col, int row) => new Vector3Int(col - Size / 2, Size / 2 - 1 - row, 0);

        private static void Slice(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.spriteImportMode == SpriteImportMode.Multiple
                && Mathf.Approximately(importer.spritePixelsPerUnit, Cell)
                && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == Size * Size)
                return;

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Cell;
            importer.filterMode = FilterMode.Point;
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

        private static void PaintSheet(string path, Tilemap target)
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
                           ?? NewTile($"{TileFolder}/{name}.asset", sprites[name]);
                    tiles[hash.ToHashCode()] = tile;
                }

                target.SetTile(CellOf(col, row), tile);
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
        }

        private static TileBase NewTile(string path, Sprite sprite)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
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
