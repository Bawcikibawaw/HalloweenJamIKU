using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace aTES
{
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        [SerializeField] private GameObject pausePanel;

        private LevelManager levelManager;
        private SkillChooseScreen chooseScreen;
        private bool isPaused;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            Instance = this;
            if (pausePanel == null)
            {
                Transform t = transform.Find("PausePanel");
                pausePanel = t != null ? t.gameObject : gameObject;
            }
            if (pausePanel != null && pausePanel != gameObject)
                pausePanel.SetActive(false);
        }

        private void Start()
        {
            levelManager = FindFirstObjectByType<LevelManager>();
            chooseScreen = FindFirstObjectByType<SkillChooseScreen>();

            if (pausePanel != null)
            {
                Button resumeBtn = pausePanel.transform.Find("ResumeButton")?.GetComponent<Button>();
                if (resumeBtn != null) resumeBtn.onClick.AddListener(Resume);

                Button restartBtn = pausePanel.transform.Find("RestartButton")?.GetComponent<Button>();
                if (restartBtn != null) restartBtn.onClick.AddListener(RestartLevel);

                Button quitBtn = pausePanel.transform.Find("QuitButton")?.GetComponent<Button>();
                if (quitBtn != null) quitBtn.onClick.AddListener(QuitGame);
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                // Do not toggle pause if skill choose screen is currently open
                if (chooseScreen != null && chooseScreen.gameObject.activeInHierarchy)
                    return;

                if (isPaused) Resume();
                else Pause();
            }
        }

        public void Pause()
        {
            isPaused = true;
            if (pausePanel != null) pausePanel.SetActive(true);
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            isPaused = false;
            if (pausePanel != null) pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        public void RestartLevel()
        {
            Resume();
            if (levelManager != null)
            {
                levelManager.Show(levelManager.Current);
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
