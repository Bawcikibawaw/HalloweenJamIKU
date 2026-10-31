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
    /// Builds 3 full 16x16 test levels under "Levels" in the "Path" scene using the user's sliced sprites.
    /// Every level uses the full floor sheet and full outer-wall frame (with proper corners and edges),
    /// plus connected interior barrier walls and corner caps from "duvarlar jem".
    /// </summary>
    public static class PathTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Path.unity";
        private const string FloorSheet = "Assets/Sprites/bc conspt jem.png";
        private const string TileFolder = "Assets/Sprites/Tilemaps/Generated";
        private const int Cell = 16;
        private const int Size = 16;
        private const int EdgeMinPixels = 40;

        // Legend for 16x16 interior barrier layouts (outer ring and floor come from the full sheets):
        // '.' = empty, 'U'/'D'/'L'/'R' = straight barrier on Up/Down/Left/Right edge
        // '1' = bottom-right cap (3_2), '2' = bottom-left cap (12_2), '3' = top-right cap (5_4), '4' = top-left cap (10_4)
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
            (Tilemap ground1, Tilemap walls1) = NewBaseLevel(levelsRoot.transform, "Level 1", outer);
            PaintSheet(inner, walls1, true);
            ground1.GetComponentInParent<PathCreator>().ScanMap();

            // Load the 8 interior barrier/corner tiles generated from "duvarlar jem".
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

            manager.Show(0);

            GameObject player = GameObject.Find("Player");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
            player.transform.position = ground1.GetCellCenterWorld(CellOf(2, 2));
            if (!player.TryGetComponent(out PlayerMoveRays _)) player.AddComponent<PlayerMoveRays>();

            Camera.main.orthographicSize = Size / 2f + 1f;
            Camera.main.transform.position = new Vector3(0f, 0f, -10f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[PathTestSceneBuilder] Built 3 full 16x16 levels under 'Levels'.");
        }

        private static TileBase LoadTile(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

        /// <summary>Creates a 16x16 level with the full floor sheet and full outer-wall frame already painted.</summary>
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

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("aTES_3LevelsBuilt_v3", false)) return;
                SessionState.SetBool("aTES_3LevelsBuilt_v3", true);
                Build();
            };
        }
    }
}
