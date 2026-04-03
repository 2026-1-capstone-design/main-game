using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainUIManager : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button gladiatorButton;
    [SerializeField] private Button battleButton;
    [SerializeField] private Button researchButton;
    [SerializeField] private Button missionButton;
    [SerializeField] private Button marketButton;
    [SerializeField] private Button eodButton;

    [Header("Optional Labels")]
    [SerializeField] private TMP_Text currentDayText;

    private MainFlowManager _flow;
    private SessionManager _sessionManager;
    private bool _initialized;

    public void Initialize(MainFlowManager flow, SessionManager sessionManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _sessionManager = sessionManager;

        BindButton(gladiatorButton, OnGladiatorClicked);
        BindButton(battleButton, OnBattleClicked);
        BindButton(researchButton, OnResearchClicked);
        BindButton(missionButton, OnMissionClicked);
        BindButton(marketButton, OnMarketClicked);
        BindButton(eodButton, OnEodClicked);

        if (_sessionManager != null)
        {
            _sessionManager.DayChanged += OnDayChanged;
            RefreshDayText(_sessionManager.CurrentDay);
        }

        _initialized = true;
    }

    private void OnDestroy()
    {
        if (_sessionManager != null)
        {
            _sessionManager.DayChanged -= OnDayChanged;
        }
    }

    public void SetMainMenuInteractable(bool value)
    {
        SetButtonInteractable(gladiatorButton, value);
        SetButtonInteractable(battleButton, value);
        SetButtonInteractable(researchButton, value);
        SetButtonInteractable(missionButton, value);
        SetButtonInteractable(marketButton, value);
        SetButtonInteractable(eodButton, value);
    }

    public void SetBattleButtonInteractable(bool value)
    {
        SetButtonInteractable(battleButton, value);
    }

    public void SetEodButtonInteractable(bool value)
    {
        SetButtonInteractable(eodButton, value);
    }

    public void RefreshDayText(int currentDay)
    {
        if (currentDayText == null)
        {
            return;
        }

        currentDayText.text = $"Day {currentDay}";
    }

    private void OnDayChanged(int currentDay)
    {
        RefreshDayText(currentDay);
    }

    private void OnGladiatorClicked()
    {
        if (_flow != null)
        {
            _flow.HandleGladiatorMenuRequested();
        }
    }

    private void OnBattleClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleMenuRequested();
        }
    }

    private void OnResearchClicked()
    {
        if (_flow != null)
        {
            _flow.HandleResearchMenuRequested();
        }
    }

    private void OnMissionClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMissionMenuRequested();
        }
    }

    private void OnMarketClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMarketMenuRequested();
        }
    }

    private void OnEodClicked()
    {
        if (_flow != null)
        {
            Debug.Log("EOd clicked");
            _flow.HandleEodRequested();
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetButtonInteractable(Button button, bool value)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = value;
    }
    
}