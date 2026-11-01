using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace aTES
{
    /// <summary>
    /// Finds every <see cref="Skill"/> script automatically, holds the 5 chosen on the skill choose page,
    /// and fires them with keys 1-5 (top row or numpad). Everything it needs is found at runtime.
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public const int SlotCount = 5;
        public static SkillManager Instance { get; private set; }

        [SerializeField] private Sprite[] curseIcons;
        [SerializeField] private Sprite[] trickeryIcons;
        [SerializeField] private Sprite[] chaosIcons;
        [SerializeField] private Sprite cursorSprite;
        [SerializeField] private Sprite buttonSprite;

        private readonly List<Skill> equipped = new();

        public Sprite ButtonSprite => buttonSprite;
        public IReadOnlyList<Skill> AllSkills { get; private set; }
        public IReadOnlyList<Skill> Equipped => equipped;
        public PlayerAI Player { get; private set; }
        public PlayerStats Stats { get; private set; }
        public GameTimer Timer { get; private set; }
        public ItemManager Items => ItemManager.Instance;
        public PathCreator Map => PathCreator.Active;

        private LevelManager levelManager;

        public event Action OnEquippedChanged;
        public event Action<Skill> OnSkillUsed;

        private void Awake()
        {
            Instance = this;
            AllSkills = typeof(Skill).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Skill)) && !t.IsAbstract)
                .Select(t => (Skill)Activator.CreateInstance(t))
                .OrderBy(s => s.Tree).ThenBy(s => s.Tier)
                .ToList();

#if UNITY_EDITOR
            if (curseIcons == null || curseIcons.Length == 0)
            {
                curseIcons = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/CurseTier.png")
                    .OfType<Sprite>()
                    .OrderBy(s => s.rect.x)
                    .ToArray();
            }
            if (trickeryIcons == null || trickeryIcons.Length == 0)
            {
                trickeryIcons = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Tier1.png")
                    .OfType<Sprite>()
                    .OrderBy(s => s.rect.x)
                    .ToArray();
            }
            Sprite[] chaosRaw = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/ChaosTier.png")
                .OfType<Sprite>()
                .OrderBy(s => s.rect.x)
                .ToArray();
            if (chaosRaw.Length >= 5)
            {
                chaosIcons = new[] { chaosRaw[0], chaosRaw[4], chaosRaw[1], chaosRaw[2], chaosRaw[3] };
            }
            Sprite[] stuff3 = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff3-(stas work).png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            if (stuff3.Length > 6) cursorSprite = stuff3[6];

            if (cursorSprite == null || buttonSprite == null)
            {
                Sprite[] stuff = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/stuff.png")
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();
                if (stuff.Length > 4 && cursorSprite == null) cursorSprite = stuff[4];
                if (stuff.Length > 5 && buttonSprite == null) buttonSprite = stuff[5];
            }
#endif

            ApplyCursor(cursorSprite);

            foreach (Skill s in AllSkills)
            {
                Sprite[] icons = s.Tree switch
                {
                    SkillTree.Curse => curseIcons,
                    SkillTree.Trickery => trickeryIcons,
                    SkillTree.Chaos => chaosIcons,
                    _ => null
                };
                if (icons != null && s.Tier >= 1 && s.Tier <= icons.Length && icons[s.Tier - 1] != null)
                    s.Icon = icons[s.Tier - 1];
            }
        }

        private static void ApplyCursor(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return;
            Rect r = sprite.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels(sprite.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h));
            tex.Apply();
            Cursor.SetCursor(tex, new Vector2(w * 0.5f, h * 0.2f), CursorMode.ForceSoftware);
        }

        private void Start()
        {
            Player = FindFirstObjectByType<PlayerAI>();
            Stats = Player != null ? Player.GetComponent<PlayerStats>() : null;
            Timer = FindFirstObjectByType<GameTimer>();
            levelManager = FindFirstObjectByType<LevelManager>();

            if (levelManager != null)
                levelManager.OnLevelChanged += HandleLevelChanged;
        }

        private void OnDestroy()
        {
            if (levelManager != null)
                levelManager.OnLevelChanged -= HandleLevelChanged;

            if (Instance == this) Instance = null;
        }

        private void HandleLevelChanged(int _) => ResetCooldowns();

        public void ResetCooldowns()
        {
            if (AllSkills == null) return;
            foreach (Skill s in AllSkills) s.ResetCooldown();
        }

        public void ClearEquipped()
        {
            equipped.Clear();
            OnEquippedChanged?.Invoke();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            for (int i = 0; i < SlotCount; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    Use(i);
            }
        }

        /// <summary>Uses the skill in slot 0-4. Does nothing while paused (skill choose page).</summary>
        public bool Use(int slot)
        {
            if (slot >= equipped.Count || Time.timeScale == 0f || Player == null || Map == null || !equipped[slot].TryUse(this))
                return false;

            OnSkillUsed?.Invoke(equipped[slot]);
            return true;
        }

        public bool CanEquip(Skill skill) =>
            equipped.Count < SlotCount && !equipped.Contains(skill) &&
            (skill.Tier == 1 || equipped.Exists(e => e.Tree == skill.Tree && e.Tier == skill.Tier - 1));

        /// <summary>Choose / unchoose a skill. Unchoosing also drops the higher tiers of that tree that needed it.</summary>
        public void Toggle(Skill skill)
        {
            if (equipped.Contains(skill)) equipped.RemoveAll(e => e.Tree == skill.Tree && e.Tier >= skill.Tier);
            else if (CanEquip(skill)) equipped.Add(skill);
            else return;

            OnEquippedChanged?.Invoke();
        }
    }
}
