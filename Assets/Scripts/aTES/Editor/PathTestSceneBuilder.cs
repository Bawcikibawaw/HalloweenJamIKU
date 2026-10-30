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
    /// Builds the "Path" test scene from the user's concept sheets.
    /// Each 256x256 sheet is sliced into a 16x16 grid of 16px sprites (Multiple mode). The sheets share one grid:
    ///   floor sheet  -> Ground tilemap (any cell with pixels)
    ///   wall sheets  -> Walls tilemap  (any cell with pixels = unwalkable)
    /// Identical-looking cells share one Tile asset (Assets/Sprites/Tilemaps/Generated).
    /// </summary>
    public static class PathTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Path.unity";
        private const string TileFolder = "Assets/Sprites/Tilemaps/Generated";
        private const int Cell = 16;
        private const int Grid = 16;   // 16 x 16 cells per sheet

        private const string FloorSheet = "bc conspt jem";
        private const string InnerWallSheet = "duvarlar jem";

        // A wall line counts as a barrier on a side when this many opaque pixels sit in that side's edge band.
        private const int EdgeBand = 5;
        private const int EdgeMinPixels = 40;

        [MenuItem("Tools/aTES/Build Path Test Scene")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            string floorPath = FindSheet(f => f == FloorSheet);
            string innerPath = FindSheet(f => f == InnerWallSheet);
            string outerPath = FindSheet(f => f.EndsWith("duvarlar jem") && f != InnerWallSheet);

            if (floorPath == null || innerPath == null || outerPath == null)
            {
                Debug.LogError("[PathTestSceneBuilder] Could not find all three sprite sheets in Assets/Sprites.");
                return;
            }

            SliceSheet(floorPath);
            SliceSheet(innerPath);
            SliceSheet(outerPath);

            // Remove the placeholder tiles from the previous version.
            if (AssetDatabase.IsValidFolder("Assets/Sprites/PathTest")) AssetDatabase.DeleteAsset("Assets/Sprites/PathTest");

            // Regenerate tiles from scratch so tile types (solid vs barrier) are always correct.
            if (AssetDatabase.IsValidFolder(TileFolder)) AssetDatabase.DeleteAsset(TileFolder);
            Directory.CreateDirectory(TileFolder);
            AssetDatabase.Refresh();

            // Rebuild the scene objects.
            GameObject old = GameObject.Find("Grid");
            if (old != null) Object.DestroyImmediate(old);

            GameObject gridObj = new GameObject("Grid");
            gridObj.AddComponent<UnityEngine.Grid>();
            Tilemap ground = CreateTilemap(gridObj.transform, "Ground", 0);
            Tilemap walls = CreateTilemap(gridObj.transform, "Walls", 1);

            Paint(floorPath, ground, false);
            Paint(outerPath, walls, false);   // outer ring: solid tiles (no ground under them anyway)
            Paint(innerPath, walls, true);    // inner walls: barrier tiles (cell stays walkable, edges close)

            PathCreator creator = gridObj.AddComponent<PathCreator>();
            SerializedObject so = new SerializedObject(creator);
            so.FindProperty("groundTilemap").objectReferenceValue = ground;
            so.FindProperty("obstacleTilemap").objectReferenceValue = walls;
            so.ApplyModifiedPropertiesWithoutUndo();
            creator.ScanMap();

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
                player.transform.position = ground.GetCellCenterWorld(CellFor(2, 2));

                PlayerMoveRays rays = player.GetComponent<PlayerMoveRays>();
                if (rays == null) rays = player.AddComponent<PlayerMoveRays>();
                SerializedObject rso = new SerializedObject(rays);
                rso.FindProperty("pathCreator").objectReferenceValue = creator;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = Grid * 0.5f + 1f;
                cam.transform.position = new Vector3(0.5f, 0.5f, -10f);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            int walkable = creator.Cells.Count(c => c.walkable);
            Debug.Log($"[PathTestSceneBuilder] Built from your sprites. Cells: {creator.Cells.Count}, walkable: {walkable}, unwalkable: {creator.Cells.Count - walkable}");
        }

        /// <summary>Cell coordinate for a sheet cell (col, row from the top). Sheet is centred on the origin.</summary>
        private static Vector3Int CellFor(int col, int rowFromTop)
        {
            return new Vector3Int(col - Grid / 2, (Grid - 1 - rowFromTop) - Grid / 2, 0);
        }

        private static string FindSheet(System.Func<string, bool> match)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tilemaps/")) continue;
                if (match(Path.GetFileNameWithoutExtension(path))) return path;
            }
            return null;
        }

        /// <summary>Switches a sheet to Multiple and slices it into 16x16 sprites named "{sheet}_{col}_{row}" (row from top).</summary>
        private static void SliceSheet(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            string sheet = Path.GetFileNameWithoutExtension(path);

            bool alreadySliced = importer.spriteImportMode == SpriteImportMode.Multiple
                                 && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == Grid * Grid;
            if (alreadySliced) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Cell;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            List<SpriteRect> rects = new List<SpriteRect>();
            for (int row = 0; row < Grid; row++)
            {
                for (int col = 0; col < Grid; col++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = $"{sheet}_{col}_{row}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(col * Cell, (Grid - 1 - row) * Cell, Cell, Cell),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Paints every non-empty 16x16 cell of a sheet onto a tilemap, sharing one Tile per unique cell image.
        /// With barriers = true the tiles are <see cref="BarrierTile"/>s whose closed sides come from where the line sits in the cell.
        /// </summary>
        private static void Paint(string path, Tilemap target, bool barriers)
        {
            string sheet = Path.GetFileNameWithoutExtension(path);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));

            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            Dictionary<ulong, TileBase> tilesByImage = new Dictionary<ulong, TileBase>();

            for (int row = 0; row < Grid; row++)
            {
                for (int col = 0; col < Grid; col++)
                {
                    Color[] raw = tex.GetPixels(col * Cell, (Grid - 1 - row) * Cell, Cell, Cell);
                    Color32[] px = System.Array.ConvertAll(raw, c => (Color32)c);
                    if (!px.Any(p => p.a > 20)) continue;

                    ulong hash = Hash(px);
                    if (!tilesByImage.TryGetValue(hash, out TileBase tile))
                    {
                        string spriteName = $"{sheet}_{col}_{row}";
                        string assetPath = $"{TileFolder}/{spriteName}.asset";
                        tile = barriers
                            ? CreateTile<BarrierTile>(assetPath, sprites[spriteName], t => t.blockedSides = DetectSides(raw))
                            : CreateTile<Tile>(assetPath, sprites[spriteName], null);
                        tilesByImage[hash] = tile;
                    }

                    target.SetTile(CellFor(col, row), tile);
                }
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Which edges of the cell carry a wall line (pixels are bottom-left origin, so y = 0 is the bottom).</summary>
        private static TileSides DetectSides(Color[] raw)
        {
            int up = 0, down = 0, left = 0, right = 0;
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    if (raw[y * Cell + x].a <= 20f / 255f) continue;
                    if (y >= Cell - EdgeBand) up++;
                    if (y < EdgeBand) down++;
                    if (x < EdgeBand) left++;
                    if (x >= Cell - EdgeBand) right++;
                }
            }

            TileSides sides = TileSides.None;
            if (up >= EdgeMinPixels) sides |= TileSides.Up;
            if (down >= EdgeMinPixels) sides |= TileSides.Down;
            if (left >= EdgeMinPixels) sides |= TileSides.Left;
            if (right >= EdgeMinPixels) sides |= TileSides.Right;
            return sides;
        }

        private static T CreateTile<T>(string assetPath, Sprite sprite, System.Action<T> setup) where T : Tile
        {
            T tile = ScriptableObject.CreateInstance<T>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            setup?.Invoke(tile);
            AssetDatabase.CreateAsset(tile, assetPath);
            return tile;
        }

        private static ulong Hash(Color32[] px)
        {
            ulong h = 1469598103934665603UL;
            foreach (Color32 c in px)
            {
                bool clear = c.a == 0;
                h = (h ^ (clear ? 0u : c.r)) * 1099511628211UL;
                h = (h ^ (clear ? 0u : c.g)) * 1099511628211UL;
                h = (h ^ (clear ? 0u : c.b)) * 1099511628211UL;
                h = (h ^ c.a) * 1099511628211UL;
            }
            return h;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Tilemap tm = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tm;
        }

        [InitializeOnLoadMethod]
        private static void AutoRun()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("aTES_PathBuilt_v4", false)) return;
                SessionState.SetBool("aTES_PathBuilt_v4", true);
                Build();
            };
        }
    }
}
