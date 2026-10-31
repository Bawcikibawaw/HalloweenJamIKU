using UnityEngine;
using System;

namespace aTES
{
    /// <summary>
    /// Put this on a parent object whose children are the levels (each child = one Grid with a PathCreator).
    /// Level order is the order of the children in the Hierarchy. Only the current level is enabled.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public int Current { get; private set; }
        
        public event Action<int> OnLevelChanged;
        
        private void Awake() => Show(0);

        [ContextMenu("Next Level")]
        public void Next() => Show((Current + 1) % transform.childCount);

        public void Show(int index)
        {
            Current = Mathf.Clamp(index, 0, transform.childCount - 1);

            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(i == Current);
            }

            OnLevelChanged?.Invoke(Current);
        }
    }
}
