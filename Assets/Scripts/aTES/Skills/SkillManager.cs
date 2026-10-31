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

        private readonly List<Skill> equipped = new();

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
