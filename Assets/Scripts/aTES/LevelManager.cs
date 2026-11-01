using UnityEngine;
using System;
using TMPro;

namespace aTES
{
    /// <summary>
    /// Put this on a parent object whose children are the levels (each child = one Grid with a PathCreator).
    /// Level order is the order of the children in the Hierarchy. Only the current level is enabled.
    /// </summary>
    [ExecuteAlways]
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private float cameraZoom = 9.6f;
        [SerializeField] private float rightOffset = 1.8f;
        [SerializeField] private float bottomOffset = -0.3f;

        public int Current { get; private set; }
        
        public event Action<int> OnLevelChanged;
        
        private void Awake()
        {
            if (Application.isPlaying) Show(0);
        }

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

            TMP_Text levelText = GameObject.Find("LevelText")?.GetComponent<TMP_Text>();
            if (levelText != null) levelText.text = $"LEVEL {Current + 1}";

            OnLevelChanged?.Invoke(Current);
        }
    }
}
