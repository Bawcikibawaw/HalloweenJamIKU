
using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Header("Timer UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text warningText;

    [Header("Timer Settings")]
    [SerializeField] private float lastMinuteThreshold = 60f;

    private float initialTime;
    private float remainingTime;

    private bool isTimerRunning;
    private bool halfTimeTriggered;
    private bool lastMinuteTriggered;
    private bool timeUpTriggered;


    // Initializes the timer for a new level
    public void StartTimer(float levelDuration)
    {
        initialTime = Mathf.Max(0f, levelDuration);
        remainingTime = initialTime;

        halfTimeTriggered = false;
        lastMinuteTriggered = false;
        timeUpTriggered = false;

        isTimerRunning = initialTime > 0f;

        UpdateTimerUI();

        if (warningText != null)
            warningText.text = "";

        if (!isTimerRunning)
            TimeUp();
    }


    private void Update()
    {
        if (!isTimerRunning)
            return;

        // Countdown
        remainingTime -= Time.deltaTime;
        remainingTime = Mathf.Max(remainingTime, 0f);

        UpdateTimerUI();
        CheckTimeStamps();

        // Check if time is up
        if (remainingTime <= 0f && !timeUpTriggered)
            TimeUp();
    }


    private void UpdateTimerUI()
    {
        if (timerText == null)
            return;

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";
    }


    private void CheckTimeStamps()
    {
        // Half-time warning
        if (!halfTimeTriggered &&
            remainingTime <= initialTime / 2f)
        {
            halfTimeTriggered = true;
            ShowWarning("HALF TIME!");
        }

        // Last minute warning
        if (!lastMinuteTriggered &&
            remainingTime <= lastMinuteThreshold)
        {
            lastMinuteTriggered = true;
            ShowWarning("1 MINUTE LEFT!");
        }
    }


    private void ShowWarning(string message)
    {
        if (warningText != null)
            warningText.text = message;

        Debug.Log(message);
    }


    private void TimeUp()
    {
        if (timeUpTriggered)
            return;

        timeUpTriggered = true;
        isTimerRunning = false;

        Debug.Log("TIME IS UP!");

       // if (GameManager.Instance != null)
         //   GameManager.Instance.HandleTimeExpired();
    }
}
