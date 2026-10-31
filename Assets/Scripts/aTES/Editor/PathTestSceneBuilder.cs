using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace aTES.EditorTools
{
    /// <summary>
    /// Builds 3 test levels under "Levels" in the "Path" scene using the user's sliced sprites:
    /// - Level 1: Original 16x16 concept sheet layout.
    /// - Level 2: 16x16 chamber/corridor layout with half-barriers and solid blocks.
    /// - Level 3: 12x10 compact arena to test automatic bounds calculation on different map sizes.
    /// </summary>
    public static class PathTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Path.unity";
        private const string FloorSheet = "Assets/Sprites/bc conspt jem.png";
        private const string TileFolder = "Assets/Sprites/Tilemaps/Generated";
        private const int Cell = 16;
        private const int Size = 16;
        private const int EdgeMinPixels = 40;

        // Legend for custom layouts:
        // '#' = outer solid wall, '.' = floor, '^'/'v'/'<'/'>' = floor + half-barrier on Up/Down/Left/Right
        private static readonly string[] Level2Map =
        {
            "################",
            "#..............#",
            "#..^^^^^^..vv..#",
            "#..<....>......#",
            "#..<....>..##..#",
            "#..<....>..##..#",
            "#..vvvvvv......#",
            "#..............#",
            "#....^^....^^..#",
            "#...<..>..<..>.#",
            "#...<..>..<..>.#",
            "#....vv....vv..#",
            "#..............#",
            "#..>>>>..<<<<..#",
            "#..............#",
            "################",
        };

        private static readonly string[] Level3Map =
        {
            "############",
            "#..........#",
            "#..^^^^....#",
            "#..<..>..#.#",
            "#........#.#",
            "#.#..vvvv..#",
            "#.#........#",
            "#....^^^^..#",
            "#..........#",
            "############",
        };

        [MenuItem("Tools/aTES/Build Path Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);

            string[] walls = AssetDatabase.FindAssets("duvarlar jem t:Texture2D", new[] { "Assets/Sprites" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string inner = walls.Single(p => p.EndsWith("/duvarlar jem.png"));
            string outer = walls.Single(p => p != inner);

            foreach (string sheet in new[] { FloorSheet, inner, outer }) Slice(sheet);

            AssetDatabase.DeleteAsset(TileFolder);
            Directory.CreateDirectory(TileFolder);
            AssetDatabase.Refresh();

            Object.DestroyImmediate(GameObject.Find("Grid"));
            Object.DestroyImmediate(GameObject.Find("Levels"));

            GameObject levelsRoot = new GameObject("Levels");
            LevelManager manager = levelsRoot.AddComponent<LevelManager>();

            // Level 1: painted directly from the 3 concept sheets.
            (Tilemap ground1, Tilemap walls1) = NewLevel(levelsRoot.transform, "Level 1");
            PaintSheet(FloorSheet, ground1, false);
            PaintSheet(outer, walls1, false);
            PaintSheet(inner, walls1, true);
            ground1.GetComponentInParent<PathCreator>().ScanMap();

            // Reuse the generated tiles from Level 1 to paint Level 2 and Level 3.
            TileBase[] floors = new[] { "bc conspt jem_1_1", "bc conspt jem_2_1", "bc conspt jem_3_1" }
                .Select(n => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{n}.asset")).ToArray();
            TileBase solidWall = AssetDatabase.LoadAssetAtPath<TileBase>(
                AssetDatabase.FindAssets("duvarlar t:Tile", new[] { TileFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .First(p => !(AssetDatabase.LoadAssetAtPath<TileBase>(p) is BarrierTile)));
            Dictionary<TileSides, TileBase> barrierBySide = AssetDatabase.FindAssets("t:BarrierTile", new[] { TileFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BarrierTile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b.BlockedSides != TileSides.None)
                .GroupBy(b => b.BlockedSides)
                .ToDictionary(g => g.Key, g => (TileBase)g.First());

            BuildPatternLevel(levelsRoot.transform, "Level 2", Level2Map, floors, solidWall, barrierBySide);
            BuildPatternLevel(levelsRoot.transform, "Level 3", Level3Map, floors, solidWall, barrierBySide);

            manager.Show(0);

            GameObject player = GameObject.Find("Player");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
            player.transform.position = ground1.GetCellCenterWorld(CellOf(2, 2, Size, Size));
            if (!player.TryGetComponent(out PlayerMoveRays _)) player.AddComponent<PlayerMoveRays>();

            Camera.main.orthographicSize = Size / 2f + 1f;
            Camera.main.transform.position = new Vector3(0f, 0f, -10f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[PathTestSceneBuilder] Built 3 test levels under 'Levels'.");
        }

        private static (Tilemap ground, Tilemap walls) NewLevel(Transform parent, string name)
        {
            GameObject level = new GameObject(name);
            level.transform.SetParent(parent, false);
            level.AddComponent<Grid>();
            Tilemap ground = NewTilemap(level, "Ground", 0);
            Tilemap walls = NewTilemap(level, "Walls", 1);
            level.AddComponent<PathCreator>();
            return (ground, walls);
        }

        private static void BuildPatternLevel(
            Transform parent,
            string name,
            string[] rows,
            TileBase[] floors,
            TileBase solidWall,
            Dictionary<TileSides, TileBase> barriers)
        {
            (Tilemap ground, Tilemap walls) = NewLevel(parent, name);
            int h = rows.Length, w = rows[0].Length;

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    Vector3Int cell = CellOf(c, r, w, h);
                    char ch = rows[r][c];
                    if (ch == '#')
                    {
                        walls.SetTile(cell, solidWall);
                        continue;
                    }

                    ground.SetTile(cell, floors[(c + r) % floors.Length]);
                    TileSides side = ch == '^' ? TileSides.Up : ch == 'v' ? TileSides.Down : ch == '<' ? TileSides.Left : ch == '>' ? TileSides.Right : TileSides.None;
                    if (side != TileSides.None && barriers.TryGetValue(side, out TileBase b)) walls.SetTile(cell, b);
                }
            }

            ground.GetComponentInParent<PathCreator>().ScanMap();
        }

        private static Vector3Int CellOf(int col, int row, int cols, int rows) => new Vector3Int(col - cols / 2, rows / 2 - 1 - row, 0);

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
                    tile = NewTile($"{TileFolder}/{name}.asset", sprites[name], barriers ? px : null);
                    tiles[hash.ToHashCode()] = tile;
                }

                target.SetTile(CellOf(col, row, Size, Size), tile);
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

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("aTES_3LevelsBuilt_v2", false)) return;
                SessionState.SetBool("aTES_3LevelsBuilt_v2", true);
                Build();
            };
        }
    }
}
