// Records a microphone clip and transcribes it with whisper.unity.
// Applies battle order slow-motion while recording is active.
// Disables recording controls while a server command is processing.
// Submits the recognized text through BattleSceneUIManager as a global order.
// Builds a Korean battle-command initial prompt from current battle unit names.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public sealed class BattleVoiceOrderInputController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField]
    private BattleSceneUIManager battleSceneUIManager;

    [SerializeField]
    private BattleOrdersManager battleOrdersManager;

    [SerializeField]
    private WhisperManager whisperManager;

    [SerializeField]
    private MicrophoneRecord microphoneRecord;

    [Header("Buttons")]
    [SerializeField]
    private Button startRecordingButton;

    [SerializeField]
    private Button stopRecordingButton;

    [Header("Whisper Model")]
    [SerializeField]
    private string modelPath = "Whisper/ggml-medium-q5_0.bin";

    [SerializeField]
    private bool isModelPathInStreamingAssets = true;

    [SerializeField]
    private bool initializeWhisperOnStart = true;

    [Header("Microphone")]
    [SerializeField]
    private int recordFrequency = 16000;

    [SerializeField]
    private int maxRecordLengthSec = 20;

    [SerializeField]
    private float minimumRecordLengthSec = 0.15f;

    [Header("Prompt")]
    [SerializeField]
    private string battleCommandPromptPrefix = "한국어 전투 명령 받아쓰기.";

    [SerializeField]
    private string battleCommandKeywordPrompt =
        "주요 단어: 공격, 이동, 후퇴, 스킬, 보호, 따라가, 붙어, 빠져, 왼쪽, 오른쪽, 앞, 뒤, 적, 아군.";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private bool _isRecording;
    private bool _isTranscribing;
    private bool _isWhisperReady;
    private BattleOrdersManager _subscribedBattleOrdersManager;

    private void Awake()
    {
        EnsureReferences();
        ConfigureMicrophoneRecord();
        BindButtons();
        RefreshButtonStates();
    }

    private async void Start()
    {
        if (!initializeWhisperOnStart)
        {
            _isWhisperReady = whisperManager != null && whisperManager.IsLoaded;
            RefreshButtonStates();
            return;
        }

        if (whisperManager == null)
        {
            RefreshButtonStates();
            return;
        }

        if (!ConfiguredModelFileExists())
        {
            _isWhisperReady = false;
            RefreshButtonStates();
            return;
        }

        try
        {
            if (!whisperManager.IsLoaded && !whisperManager.IsLoading)
            {
                ApplyWhisperStaticSettings();
                await whisperManager.InitModel();
            }

            _isWhisperReady = whisperManager.IsLoaded;

            if (verboseLog)
            {
                Debug.Log(
                    $"[BattleVoiceOrderInputController] Whisper ready={_isWhisperReady}, modelPath={modelPath}",
                    this
                );
            }
        }
        catch (Exception exception)
        {
            _isWhisperReady = false;
            Debug.LogError($"[BattleVoiceOrderInputController] Whisper init failed. {exception}", this);
        }

        RefreshButtonStates();
    }

    private void OnDestroy()
    {
        if (startRecordingButton != null)
        {
            startRecordingButton.onClick.RemoveListener(BeginRecordingFromButton);
        }

        if (stopRecordingButton != null)
        {
            stopRecordingButton.onClick.RemoveListener(EndRecordingFromButton);
        }

        if (microphoneRecord != null)
        {
            microphoneRecord.OnRecordStop -= HandleRecordStop;
        }

        UnbindBattleOrdersManagerEvents();
    }

    public void BeginRecordingFromButton()
    {
        if (_isRecording || _isTranscribing || IsBattleOrderCommandProcessing())
            return;

        StartCoroutine(BeginRecordingRoutine());
    }

    public void EndRecordingFromButton()
    {
        if (!_isRecording || _isTranscribing || IsBattleOrderCommandProcessing())
            return;

        _isRecording = false;
        _isTranscribing = true;
        RefreshButtonStates();

        if (microphoneRecord == null)
        {
            Debug.LogError("[BattleVoiceOrderInputController] MicrophoneRecord is not assigned.", this);
            CancelVoiceOrderInputMode();
            _isTranscribing = false;
            RefreshButtonStates();
            return;
        }

        microphoneRecord.StopRecord();

        if (verboseLog)
        {
            Debug.Log("[BattleVoiceOrderInputController] Recording stop requested.", this);
        }
    }

    private IEnumerator BeginRecordingRoutine()
    {
        EnsureReferences();

        if (IsBattleOrderCommandProcessing())
        {
            RefreshButtonStates();
            yield break;
        }

        if (!ConfiguredModelFileExists())
        {
            _isWhisperReady = false;
            RefreshButtonStates();
            yield break;
        }

        if (!_isWhisperReady && whisperManager != null)
        {
            _isWhisperReady = whisperManager.IsLoaded;
        }

        if (!_isWhisperReady)
        {
            RefreshButtonStates();
            yield break;
        }

        if (microphoneRecord == null)
        {
            Debug.LogError(
                "[BattleVoiceOrderInputController] Recording blocked. MicrophoneRecord is not assigned.",
                this
            );
            yield break;
        }

        if (!TryBeginVoiceOrderInputMode())
        {
            yield break;
        }

        yield return RequestMicrophonePermissionIfNeeded();

        if (!HasMicrophonePermission())
        {
            Debug.LogWarning(
                "[BattleVoiceOrderInputController] Recording blocked. Microphone permission was not granted.",
                this
            );
            CancelVoiceOrderInputMode();
            yield break;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[BattleVoiceOrderInputController] Recording blocked. No microphone device found.", this);
            CancelVoiceOrderInputMode();
            yield break;
        }

        ConfigureMicrophoneRecord();
        microphoneRecord.StartRecord();
        _isRecording = true;
        _isTranscribing = false;
        RefreshButtonStates();

        if (verboseLog)
        {
            Debug.Log("[BattleVoiceOrderInputController] Recording started.", this);
        }
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        if (!_isTranscribing)
        {
            return;
        }

        bool submitted = false;

        try
        {
            if (
                recordedAudio.Data == null
                || recordedAudio.Data.Length == 0
                || recordedAudio.Length < minimumRecordLengthSec
            )
            {
                Debug.LogWarning(
                    $"[BattleVoiceOrderInputController] Transcription skipped. Recorded audio is too short. Length={recordedAudio.Length:F3}s",
                    this
                );
                return;
            }

            if (whisperManager == null)
            {
                Debug.LogError(
                    "[BattleVoiceOrderInputController] Transcription failed. WhisperManager is not assigned.",
                    this
                );
                return;
            }

            ApplyWhisperDynamicPrompt();

            if (verboseLog)
            {
                Debug.Log(
                    $"[BattleVoiceOrderInputController] Transcription started. Length={recordedAudio.Length:F2}s, Frequency={recordedAudio.Frequency}, Channels={recordedAudio.Channels}",
                    this
                );
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            WhisperResult result = await whisperManager.GetTextAsync(
                recordedAudio.Data,
                recordedAudio.Frequency,
                recordedAudio.Channels
            );
            stopwatch.Stop();

            string recognizedText = result != null ? (result.Result ?? string.Empty).Trim() : string.Empty;
            float realtimeRate =
                stopwatch.ElapsedMilliseconds > 0
                    ? recordedAudio.Length / (stopwatch.ElapsedMilliseconds * 0.001f)
                    : 0f;

            Debug.Log(
                $"[BattleVoiceOrderInputController] STT completed. Elapsed={stopwatch.ElapsedMilliseconds}ms, Rate={realtimeRate:F2}x, Text=\"{recognizedText}\"",
                this
            );

            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                Debug.LogWarning("[BattleVoiceOrderInputController] Recognized text is empty.", this);
                return;
            }

            SubmitVoiceOrderText(recognizedText);
            submitted = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BattleVoiceOrderInputController] Transcription failed. {exception}", this);
        }
        finally
        {
            if (!submitted)
            {
                CancelVoiceOrderInputMode();
            }

            _isRecording = false;
            _isTranscribing = false;
            RefreshButtonStates();
        }
    }

    private void ApplyWhisperStaticSettings()
    {
        if (whisperManager == null)
            return;

        try
        {
            whisperManager.ModelPath = modelPath;
            whisperManager.IsModelPathInStreamingAssets = isModelPathInStreamingAssets;
        }
        catch (InvalidOperationException)
        {
            // The model is already loading or loaded. Keep the current model path.
        }

        whisperManager.language = "ko";
        whisperManager.translateToEnglish = false;
        whisperManager.noContext = true;
        whisperManager.singleSegment = false;
        whisperManager.enableTokens = false;
    }

    private void ApplyWhisperDynamicPrompt()
    {
        ApplyWhisperStaticSettings();

        if (whisperManager == null)
            return;

        whisperManager.initialPrompt = BuildInitialPrompt();
    }

    private string BuildInitialPrompt()
    {
        string namesSegment =
            battleOrdersManager != null ? battleOrdersManager.BuildCurrentUnitDisplayNamePromptSegment() : string.Empty;

        if (string.IsNullOrWhiteSpace(namesSegment))
        {
            return $"{battleCommandPromptPrefix} {battleCommandKeywordPrompt}";
        }

        return $"{battleCommandPromptPrefix} 현재 전투 이름: {namesSegment}. {battleCommandKeywordPrompt}";
    }

    private bool TryBeginVoiceOrderInputMode()
    {
        EnsureReferences();
        return battleSceneUIManager != null && battleSceneUIManager.TryBeginVoiceOrderInput();
    }

    private void SubmitVoiceOrderText(string recognizedText)
    {
        EnsureReferences();

        if (battleSceneUIManager == null)
        {
            Debug.LogError(
                "[BattleVoiceOrderInputController] Cannot submit voice order. BattleSceneUIManager is not assigned.",
                this
            );
            return;
        }

        battleSceneUIManager.SubmitVoiceOrderInput(recognizedText);
    }

    private void CancelVoiceOrderInputMode()
    {
        EnsureReferences();
        battleSceneUIManager?.CancelVoiceOrderInput();
    }

    private void EnsureReferences()
    {
        if (battleSceneUIManager == null)
        {
            battleSceneUIManager = FindFirstObjectByType<BattleSceneUIManager>();
        }

        if (battleOrdersManager == null)
        {
            battleOrdersManager = FindFirstObjectByType<BattleOrdersManager>();
        }

        if (whisperManager == null)
        {
            whisperManager = FindFirstObjectByType<WhisperManager>();
        }

        if (microphoneRecord == null)
        {
            microphoneRecord = GetComponent<MicrophoneRecord>();
        }

        if (microphoneRecord == null)
        {
            microphoneRecord = FindFirstObjectByType<MicrophoneRecord>();
        }

        RebindBattleOrdersManagerEvents();
    }

    private void ConfigureMicrophoneRecord()
    {
        if (microphoneRecord == null)
            return;

        microphoneRecord.maxLengthSec = maxRecordLengthSec;
        microphoneRecord.loop = false;
        microphoneRecord.frequency = recordFrequency;
        microphoneRecord.echo = false;
        microphoneRecord.useVad = false;
        microphoneRecord.vadStop = false;
        microphoneRecord.OnRecordStop -= HandleRecordStop;
        microphoneRecord.OnRecordStop += HandleRecordStop;
    }

    private void BindButtons()
    {
        if (startRecordingButton != null)
        {
            startRecordingButton.onClick.RemoveListener(BeginRecordingFromButton);
            startRecordingButton.onClick.AddListener(BeginRecordingFromButton);
        }

        if (stopRecordingButton != null)
        {
            stopRecordingButton.onClick.RemoveListener(EndRecordingFromButton);
            stopRecordingButton.onClick.AddListener(EndRecordingFromButton);
        }
    }

    private void RefreshButtonStates()
    {
        bool commandProcessing = IsBattleOrderCommandProcessing();
        bool canStart = _isWhisperReady && !_isRecording && !_isTranscribing && !commandProcessing;
        bool canStop = _isRecording && !_isTranscribing && !commandProcessing;

        if (startRecordingButton != null)
        {
            startRecordingButton.interactable = canStart;
        }

        if (stopRecordingButton != null)
        {
            stopRecordingButton.interactable = canStop;
        }
    }

    private bool IsBattleOrderCommandProcessing()
    {
        EnsureBattleOrdersManagerReferenceOnly();
        return battleOrdersManager != null && battleOrdersManager.CurrentCommandState == BattleOrderCommandState.Processing;
    }

    private void EnsureBattleOrdersManagerReferenceOnly()
    {
        if (battleOrdersManager == null)
        {
            battleOrdersManager = FindFirstObjectByType<BattleOrdersManager>();
        }

        RebindBattleOrdersManagerEvents();
    }

    private void RebindBattleOrdersManagerEvents()
    {
        if (_subscribedBattleOrdersManager == battleOrdersManager)
        {
            return;
        }

        UnbindBattleOrdersManagerEvents();
        _subscribedBattleOrdersManager = battleOrdersManager;

        if (_subscribedBattleOrdersManager == null)
        {
            return;
        }

        _subscribedBattleOrdersManager.OnCommandStateChanged += HandleBattleOrderCommandStateChanged;
    }

    private void UnbindBattleOrdersManagerEvents()
    {
        if (_subscribedBattleOrdersManager == null)
        {
            return;
        }

        _subscribedBattleOrdersManager.OnCommandStateChanged -= HandleBattleOrderCommandStateChanged;
        _subscribedBattleOrdersManager = null;
    }

    private void HandleBattleOrderCommandStateChanged(
        BattleOrderCommandState previousState,
        BattleOrderCommandState nextState
    )
    {
        RefreshButtonStates();
    }

    private IEnumerator RequestMicrophonePermissionIfNeeded()
    {
        if (HasMicrophonePermission())
            yield break;

        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
    }

    private static bool HasMicrophonePermission()
    {
#if UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
        return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
        return true;
#endif
    }

    private bool ConfiguredModelFileExists()
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return false;

        string resolvedPath = isModelPathInStreamingAssets
            ? Path.Combine(Application.streamingAssetsPath, modelPath)
            : modelPath;

        return File.Exists(resolvedPath);
    }
}
