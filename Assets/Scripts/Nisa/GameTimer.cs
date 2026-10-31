using UnityEngine;
using TMPro;
using aTES;

public class GameTimer : MonoBehaviour
{
    [SerializeField] private float levelDuration = 120f;
    [SerializeField] private float lastMinuteThreshold = 60f;

    private TMP_Text timerText;
    private TMP_Text warningText;
    private LevelManager levelManager;

    private float initialTime;
    private float remainingTime;
    private bool isRunning;
    private bool halfTriggered;
    private bool minuteTriggered;

    private void Start()
    {
        timerText = GameObject.Find("TimerText")?.GetComponent<TMP_Text>();
        warningText = GameObject.Find("WarningText")?.GetComponent<TMP_Text>();
        levelManager = FindFirstObjectByType<LevelManager>();

        if (levelManager != null)
            levelManager.OnLevelChanged += _ => StartTimer(levelDuration);

        StartTimer(levelDuration);
    }

    public void StartTimer(float duration)
    {
        initialTime = Mathf.Max(0f, duration);
        remainingTime = initialTime;
        halfTriggered = false;
        minuteTriggered = false;
        isRunning = initialTime > 0f;

        if (warningText != null) warningText.text = "";
        UpdateUI();
    }

    /// <summary>Adds (or with a negative value, removes) time from the running timer.</summary>
    public void AddTime(float seconds)
    {
        remainingTime = Mathf.Max(0f, remainingTime + seconds);
        UpdateUI();
    }

    private void Update()
    {
        if (!isRunning) return;

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        UpdateUI();

        if (!halfTriggered && remainingTime <= initialTime * 0.5f)
        {
            halfTriggered = true;
            ShowWarning("HALF TIME!");
        }

        if (!minuteTriggered && remainingTime <= lastMinuteThreshold)
        {
            minuteTriggered = true;
            ShowWarning("1 MINUTE LEFT!");
        }

        if (remainingTime <= 0f)
        {
            isRunning = false;
            ShowWarning("TIME IS UP!");
        }
    }

    private void UpdateUI()
    {
        if (timerText != null)
            timerText.text = $"{Mathf.FloorToInt(remainingTime / 60f):00}:{Mathf.FloorToInt(remainingTime % 60f):00}";
    }

    private void ShowWarning(string message)
    {
        if (warningText != null) warningText.text = message;
        Debug.Log(message);
    }
}
