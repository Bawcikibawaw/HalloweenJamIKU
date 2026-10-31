using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
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
            "..R..1DDDD2..L..",
            "..R..R....L..L..",
            "..3UU4....3UU4..",
            "................",
            "..1DDDD2..1DD2..",
            "..R....L..R..L..",
            "..R....L..R..L..",
            "..R.......R..L..",
            "..3UUUUUUU4..L..",
            ".............L..",
            "..UUUUUUUUUUU4..",
            "................",
        };

        private static readonly string[] Level3Walls =
        {
            "................",
            "................",
            "..1DDDD2.1DDD2..",
            "..R....L.R...L..",
            "..R....L.....L..",
            "..3UU2.3UUUUU4..",
            ".....L..........",
            "..1DD4...1DDD2..",
            "..R......R...L..",
            "..R..R...R...L..",
            "..R..R.......L..",
            "..R..3UUUUUUU4..",
            "..R.............",
            "..3UUUUUUUUUU2..",
            ".............L..",
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
            foreach (string name in new[] { "Grid", "Levels", "PlayerStartPoint", "DeliveryPoint", "ItemManager", "GameTimer", "Canvas" })
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
                ['3'] = LoadTile("duvarlar jem_5_4"),
                ['4'] = LoadTile("duvarlar jem_10_4"),
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

            Transform handPoint = player.transform.Find("HandPoint");
            if (handPoint == null)
            {
                GameObject hp = new GameObject("HandPoint");
                hp.transform.SetParent(player.transform, false);
                hp.transform.localPosition = new Vector3(0.4f, 0.3f, 0f);
                handPoint = hp.transform;
            }

            if (!player.TryGetComponent(out PlayerMoveRays _)) player.AddComponent<PlayerMoveRays>();
            if (!player.TryGetComponent(out PlayerAI playerAI)) playerAI = player.AddComponent<PlayerAI>();

            SerializedObject aiSO = new SerializedObject(playerAI);
            aiSO.FindProperty("handPoint").objectReferenceValue = handPoint;
            aiSO.FindProperty("deliveryPoint").objectReferenceValue = deliveryPointObj.transform;
            aiSO.FindProperty("moveSpeed").floatValue = 4f;
            aiSO.ApplyModifiedPropertiesWithoutUndo();

            // 4. Item Prefab + ItemData + ItemManager
            ItemData itemData = CreateItemAssets(baseSprite);
            GameObject itemManagerObj = new GameObject("ItemManager");
            ItemManager itemManager = itemManagerObj.AddComponent<ItemManager>();

            SerializedObject imSO = new SerializedObject(itemManager);
            imSO.FindProperty("levelManager").objectReferenceValue = levelManager;
            imSO.FindProperty("playerStartPoint").objectReferenceValue = startPointObj.transform;
            imSO.FindProperty("deliveryPoint").objectReferenceValue = deliveryPointObj.transform;
            SerializedProperty entries = imSO.FindProperty("spawnEntries");
            entries.ClearArray();
            entries.InsertArrayElementAtIndex(0);
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemData").objectReferenceValue = itemData;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
            imSO.ApplyModifiedPropertiesWithoutUndo();

            // 5. GameTimer + UI Canvas
            SetupTimerUI();

            // 6. Camera
            if (Camera.main != null)
            {
                Camera.main.orthographicSize = Size / 2f + 1f;
                Camera.main.transform.position = new Vector3(0f, 0f, -10f);
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
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void SetupTimerUI()
        {
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            TextMeshProUGUI timerTMP = CreateTMP(canvasObj.transform, "TimerText", new Vector2(0f, -30f), 32, Color.white);
            TextMeshProUGUI warningTMP = CreateTMP(canvasObj.transform, "WarningText", new Vector2(0f, -70f), 24, new Color(1f, 0.4f, 0.2f));

            GameObject timerObj = new GameObject("GameTimer");
            GameTimer gameTimer = timerObj.AddComponent<GameTimer>();
            SerializedObject gtSO = new SerializedObject(gameTimer);
            gtSO.FindProperty("timerText").objectReferenceValue = timerTMP;
            gtSO.FindProperty("warningText").objectReferenceValue = warningTMP;
            gtSO.FindProperty("levelDuration").floatValue = 120f;
            gtSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, Vector2 anchoredPos, float fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 50f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.text = "";
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
