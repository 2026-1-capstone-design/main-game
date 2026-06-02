// 자동 카메라 포커싱 주기와 전환을 관리한다.
// CameraView의 기존 orbit/look/FOV 제약 안에서만 카메라 상태를 적용한다.
// 명령 입력 시작 상태를 캐싱하고, 명령 처리 결과에 따라 포커싱 또는 복귀를 수행한다.
// 모든 카메라 연출 시간은 real time 기준으로 계산한다.

using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleAutoCameraDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CameraView cameraView;

    [SerializeField]
    private BattleSimulationManager battleSimulationManager;

    [SerializeField]
    private BattleOrdersManager battleOrdersManager;

    [Header("Auto Camera")]
    [SerializeField]
    private bool initialAutoCameraEnabled;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Default 상태에서 다음 자동 포커싱까지 기다리는 real time 초.")]
    private float defaultFocusCooldownSeconds = 5f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("UserInput/Processing 상태에서 다음 자동 포커싱까지 기다리는 real time 초.")]
    private float commandFocusCooldownSeconds = 2f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("일반 자동 포커싱 이동에 쓰는 SmoothStep 전환 시간.")]
    private float autoTransitionDurationSeconds = 1f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("명령 성공 포커싱과 실패 복귀에 쓰는 SmoothStep 전환 시간.")]
    private float commandResultTransitionSeconds = 0.5f;

    [Header("Focus Projection")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("유닛 기준 선호 카메라 거리. 100을 기본 거리로 보고, 그보다 작으면 CameraView에서 FOV 줌인을 적용한다.")]
    private float preferredFocusDistance = 100f;

    [SerializeField]
    [Range(-80f, 80f)]
    [Tooltip("유닛 기준 선호 elevation 각도. 실제 위치는 CameraView valid ring으로 투영된다.")]
    private float preferredFocusElevationDegrees = 35f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("자동 포커싱 후 대상 주변을 도는 선호 원 각도 변화량. real time 초당 degree.")]
    private float focusOrbitDegreesPerSecond = 20f;

    [SerializeField]
    [Min(8)]
    [Tooltip("유닛 기준 선호 원에서 valid 카메라 후보를 찾기 위한 샘플 수.")]
    private int focusCandidateSampleCount = 72;

    [Header("Focus Stabilization")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("포커싱 대상 위치를 보정하는 시간. 0이면 보정하지 않는다.")]
    private float focusTargetPositionSmoothingSeconds = 0.25f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("대상 위치 변화가 이 값보다 작으면 카메라 목표점을 갱신하지 않는다.")]
    private float focusTargetPositionDeadZone = 0.2f;

    private CameraView _subscribedCameraView;
    private BattleOrdersManager _subscribedBattleOrdersManager;

    private bool _autoCameraEnabled;
    private float _cooldownRemainingSeconds;

    private bool _hasCachedCommandInputCameraState;
    private CameraViewState _cachedCommandInputCameraState;

    private bool _isTransitioning;
    private float _transitionElapsedSeconds;
    private float _transitionDurationSeconds;
    private CameraViewState _transitionStartState;
    private CameraViewState _transitionTargetState;

    private BattleRuntimeUnit _focusedUnit;
    private float _focusCircleAngleDegrees;
    private int _focusOrbitDirection = 1;

    private bool _hasSmoothedFocusTargetPosition;
    private Vector3 _smoothedFocusTargetPosition;
    private Vector3 _focusTargetSmoothVelocity;

    public bool IsAutoCameraEnabled => _autoCameraEnabled;

    private void Awake()
    {
        _autoCameraEnabled = initialAutoCameraEnabled;
        EnsureReferences();

        _cooldownRemainingSeconds = Mathf.Max(0.01f, defaultFocusCooldownSeconds);
    }

    private void OnEnable()
    {
        EnsureReferences();
        RebindEvents();
    }

    private void Start()
    {
        EnsureReferences();
        RebindEvents();
        SetAutoCameraEnabled(initialAutoCameraEnabled);
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void Update()
    {
        if (!_autoCameraEnabled)
        {
            return;
        }

        EnsureReferences();

        if (cameraView == null || !cameraView.IsInitialized)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (_focusedUnit != null && !IsUsableFocusedUnit(_focusedUnit))
        {
            HandleFocusedUnitLost();
            return;
        }

        if (_isTransitioning)
        {
            TickTransition(deltaTime);
            return;
        }

        TickFocusedOrbit(deltaTime);
        TickAutoFocusCooldown(deltaTime);
    }

    public void SetAutoCameraEnabled(bool enabled)
    {
        EnsureReferences();
        _autoCameraEnabled = enabled;

        if (!_autoCameraEnabled)
        {
            CancelAutomaticMotion();
            return;
        }

        // 자동 카메라가 켜진 직후에는 전투 시작/토글 시점과 무관하게 첫 자동 포커싱을 지연함. 그냥 보기 이쁘라고
        _cooldownRemainingSeconds = Mathf.Max(0.01f, defaultFocusCooldownSeconds);
    }

    private void TickTransition(float deltaTime)
    {
        _transitionElapsedSeconds += Mathf.Max(0f, deltaTime);
        float t = _transitionDurationSeconds <= 0.0001f ? 1f : _transitionElapsedSeconds / _transitionDurationSeconds;
        float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        cameraView.ApplyStateInterpolated(_transitionStartState, _transitionTargetState, smoothT);

        if (t >= 1f)
        {
            cameraView.ApplyStateImmediate(_transitionTargetState);
            _isTransitioning = false;
        }
    }

    private void TickFocusedOrbit(float deltaTime)
    {
        if (!IsUsableFocusedUnit(_focusedUnit))
        {
            HandleFocusedUnitLost();
            return;
        }

        Vector3 focusTargetPosition = UpdateSmoothedFocusTargetPosition(_focusedUnit.Position, deltaTime);

        if (focusOrbitDegreesPerSecond <= 0f)
        {
            if (TryBuildFocusState(focusTargetPosition, _focusCircleAngleDegrees, out CameraViewState fixedState))
            {
                cameraView.ApplyStateImmediate(fixedState);
            }

            return;
        }

        float stepAngle = focusOrbitDegreesPerSecond * Mathf.Max(0f, deltaTime);
        float nextAngle = _focusCircleAngleDegrees + _focusOrbitDirection * stepAngle;

        if (TryBuildFocusState(focusTargetPosition, nextAngle, out CameraViewState nextState))
        {
            _focusCircleAngleDegrees = nextAngle;
            cameraView.ApplyStateImmediate(nextState);
            return;
        }

        // 한쪽 회전 방향이 invalid하면 즉시 반대 방향으로 시도한다.
        _focusOrbitDirection *= -1;
        nextAngle = _focusCircleAngleDegrees + _focusOrbitDirection * stepAngle;
        if (TryBuildFocusState(focusTargetPosition, nextAngle, out nextState))
        {
            _focusCircleAngleDegrees = nextAngle;
            cameraView.ApplyStateImmediate(nextState);
        }
    }

    private void TickAutoFocusCooldown(float deltaTime)
    {
        _cooldownRemainingSeconds -= Mathf.Max(0f, deltaTime);
        if (_cooldownRemainingSeconds > 0f)
        {
            return;
        }

        BattleRuntimeUnit target = FindClusterCenterPlayerAlly();
        if (target != null)
        {
            StartFocusTransition(target, autoTransitionDurationSeconds);
        }

        ResetCooldownForCurrentState();
    }

    private void StartFocusTransition(BattleRuntimeUnit targetUnit, float durationSeconds)
    {
        if (!IsUsableFocusedUnit(targetUnit))
        {
            return;
        }

        _focusedUnit = targetUnit;
        ResetFocusTargetSmoothing(targetUnit.Position);

        if (!TryFindNearestFocusState(targetUnit, out CameraViewState targetState, out float targetCircleAngle))
        {
            _focusedUnit = null;
            ResetFocusTargetSmoothing();
            return;
        }

        _focusCircleAngleDegrees = targetCircleAngle;
        _focusOrbitDirection = Random.value < 0.5f ? -1 : 1;
        StartStateTransition(targetState, durationSeconds);
    }

    private void StartStateTransition(CameraViewState targetState, float durationSeconds)
    {
        _transitionStartState = cameraView.CaptureState();
        _transitionTargetState = targetState;
        _transitionElapsedSeconds = 0f;
        _transitionDurationSeconds = Mathf.Max(0.0001f, durationSeconds);
        _isTransitioning = true;
    }

    private void CancelAutomaticMotion()
    {
        _isTransitioning = false;
        _focusedUnit = null;
        _cooldownRemainingSeconds = 0f;
        ResetFocusTargetSmoothing();
    }

    private void HandleManualCameraInput()
    {
        if (!_autoCameraEnabled)
        {
            return;
        }

        CancelAutomaticMotion();
        ResetCooldownForCurrentState();
    }

    private void HandleCommandStateChanged(BattleOrderCommandState previousState, BattleOrderCommandState nextState)
    {
        if (nextState == BattleOrderCommandState.UserInput)
        {
            if (cameraView != null && cameraView.IsInitialized)
            {
                _cachedCommandInputCameraState = cameraView.CaptureState();
                _hasCachedCommandInputCameraState = true;
            }

            ResetCooldownForCurrentState();
            return;
        }

        if (nextState == BattleOrderCommandState.Processing)
        {
            ResetCooldownForCurrentState();
            return;
        }

        if (nextState == BattleOrderCommandState.Default && previousState != BattleOrderCommandState.Processing)
        {
            _hasCachedCommandInputCameraState = false;
            ResetCooldownForCurrentState();
        }
    }

    private void HandleCommandProcessingFinished(BattleOrderProcessingResult result)
    {
        if (!_autoCameraEnabled)
        {
            _hasCachedCommandInputCameraState = false;
            return;
        }

        if (result != null && result.Succeeded && IsUsableFocusedUnit(result.FirstIssuedActor))
        {
            StartFocusTransition(result.FirstIssuedActor, commandResultTransitionSeconds);
        }
        else if (_hasCachedCommandInputCameraState && cameraView != null && cameraView.IsInitialized)
        {
            _focusedUnit = null;
            ResetFocusTargetSmoothing();
            StartStateTransition(_cachedCommandInputCameraState, commandResultTransitionSeconds);
        }

        _hasCachedCommandInputCameraState = false;
        ResetCooldownForCurrentState();
    }

    private BattleRuntimeUnit FindClusterCenterPlayerAlly()
    {
        if (battleSimulationManager == null)
        {
            return null;
        }

        BattleFieldSnapshot snapshot = battleSimulationManager.CurrentSnapshot;
        if (
            snapshot != null
            && snapshot.TryFindPlayerAllyNearestLargestClusterCenter(
                _focusedUnit,
                out BattleRuntimeUnit ally
            )
        )
        {
            return ally;
        }

        return FindFirstLivingPlayerUnitExcept(_focusedUnit);
    }

    private BattleRuntimeUnit FindFirstLivingPlayerUnitExcept(BattleRuntimeUnit excludedUnit)
    {
        if (battleSimulationManager == null || battleSimulationManager.RuntimeUnits == null)
        {
            return null;
        }

        for (int i = 0; i < battleSimulationManager.RuntimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = battleSimulationManager.RuntimeUnits[i];
            if (ReferenceEquals(unit, excludedUnit))
            {
                continue;
            }

            if (IsUsableFocusedUnit(unit))
            {
                return unit;
            }
        }

        return null;
    }

    private bool TryFindNearestFocusState(
        BattleRuntimeUnit targetUnit,
        out CameraViewState bestState,
        out float bestTargetCircleAngle
    )
    {
        bestState = default;
        bestTargetCircleAngle = 0f;

        if (cameraView == null || !cameraView.IsInitialized || targetUnit == null)
        {
            return false;
        }

        int sampleCount = Mathf.Max(8, focusCandidateSampleCount);
        Vector3 currentCameraPosition = cameraView.transform.position;
        Vector3 focusTargetPosition = _hasSmoothedFocusTargetPosition
            ? _smoothedFocusTargetPosition
            : targetUnit.Position;
        float bestSqrDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < sampleCount; i++)
        {
            float targetCircleAngle = i * (360f / sampleCount);
            if (!TryBuildFocusState(focusTargetPosition, targetCircleAngle, out CameraViewState candidateState))
            {
                continue;
            }

            Vector3 candidatePosition = cameraView.EvaluateCameraPosition(candidateState);
            float sqrDistance = (candidatePosition - currentCameraPosition).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
            {
                continue;
            }

            bestSqrDistance = sqrDistance;
            bestState = candidateState;
            bestTargetCircleAngle = targetCircleAngle;
            found = true;
        }

        return found;
    }

    private bool TryBuildFocusState(Vector3 focusTargetPosition, float targetCircleAngle, out CameraViewState state)
    {
        state = default;

        if (cameraView == null)
        {
            return false;
        }

        return cameraView.TryBuildFocusStateForTargetCircleAngle(
            focusTargetPosition,
            targetCircleAngle,
            preferredFocusDistance,
            preferredFocusElevationDegrees,
            out state
        );
    }

    // 유닛의 짧은 미세 이동을 직접 카메라 목표점에 반영하지 않도록 보정한다.
    private Vector3 UpdateSmoothedFocusTargetPosition(Vector3 rawTargetPosition, float deltaTime)
    {
        if (!_hasSmoothedFocusTargetPosition)
        {
            ResetFocusTargetSmoothing(rawTargetPosition);
            return _smoothedFocusTargetPosition;
        }

        float deadZone = Mathf.Max(0f, focusTargetPositionDeadZone);
        if (deadZone > 0f && (_smoothedFocusTargetPosition - rawTargetPosition).sqrMagnitude <= deadZone * deadZone)
        {
            return _smoothedFocusTargetPosition;
        }

        float smoothTime = Mathf.Max(0f, focusTargetPositionSmoothingSeconds);
        if (smoothTime <= 0.0001f)
        {
            _smoothedFocusTargetPosition = rawTargetPosition;
            _focusTargetSmoothVelocity = Vector3.zero;
            return _smoothedFocusTargetPosition;
        }

        _smoothedFocusTargetPosition = Vector3.SmoothDamp(
            _smoothedFocusTargetPosition,
            rawTargetPosition,
            ref _focusTargetSmoothVelocity,
            smoothTime,
            Mathf.Infinity,
            Mathf.Max(0f, deltaTime)
        );

        return _smoothedFocusTargetPosition;
    }

    private void ResetFocusTargetSmoothing(Vector3 targetPosition)
    {
        _smoothedFocusTargetPosition = targetPosition;
        _focusTargetSmoothVelocity = Vector3.zero;
        _hasSmoothedFocusTargetPosition = true;
    }

    private void ResetFocusTargetSmoothing()
    {
        _smoothedFocusTargetPosition = Vector3.zero;
        _focusTargetSmoothVelocity = Vector3.zero;
        _hasSmoothedFocusTargetPosition = false;
    }

    private bool IsUsableFocusedUnit(BattleRuntimeUnit unit)
    {
        return unit != null && unit.IsPlayerOwned && !unit.IsCombatDisabled;
    }

    // 현재 포커싱 대상이 사망하면 cooldown 없이 즉시 다른 자동 포커싱 후보로 전환한다.
    private void HandleFocusedUnitLost()
    {
        _isTransitioning = false;

        BattleRuntimeUnit lostUnit = _focusedUnit;
        _focusedUnit = null;
        ResetFocusTargetSmoothing();

        BattleRuntimeUnit nextTarget = FindClusterCenterPlayerAlly();
        if (nextTarget != null && !ReferenceEquals(nextTarget, lostUnit))
        {
            StartFocusTransition(nextTarget, commandResultTransitionSeconds);
            ResetCooldownForCurrentState();
            return;
        }

        ResetCooldownForCurrentState();
    }

    private void ResetCooldownForCurrentState()
    {
        _cooldownRemainingSeconds = GetCurrentCooldownSeconds();
    }

    private float GetCurrentCooldownSeconds()
    {
        if (battleOrdersManager != null && battleOrdersManager.CurrentCommandState != BattleOrderCommandState.Default)
        {
            return commandFocusCooldownSeconds;
        }

        return defaultFocusCooldownSeconds;
    }

    private void EnsureReferences()
    {
        if (cameraView == null)
        {
            cameraView = FindFirstObjectByType<CameraView>();
        }

        if (battleSimulationManager == null)
        {
            battleSimulationManager = FindFirstObjectByType<BattleSimulationManager>();
        }

        if (battleOrdersManager == null)
        {
            battleOrdersManager = FindFirstObjectByType<BattleOrdersManager>();
        }

        RebindEvents();
    }

    private void RebindEvents()
    {
        if (_subscribedCameraView != cameraView)
        {
            if (_subscribedCameraView != null)
            {
                _subscribedCameraView.OnManualCameraInput -= HandleManualCameraInput;
            }

            _subscribedCameraView = cameraView;
            if (_subscribedCameraView != null)
            {
                _subscribedCameraView.OnManualCameraInput += HandleManualCameraInput;
            }
        }

        if (_subscribedBattleOrdersManager != battleOrdersManager)
        {
            if (_subscribedBattleOrdersManager != null)
            {
                _subscribedBattleOrdersManager.OnCommandStateChanged -= HandleCommandStateChanged;
                _subscribedBattleOrdersManager.OnCommandProcessingFinished -= HandleCommandProcessingFinished;
            }

            _subscribedBattleOrdersManager = battleOrdersManager;
            if (_subscribedBattleOrdersManager != null)
            {
                _subscribedBattleOrdersManager.OnCommandStateChanged += HandleCommandStateChanged;
                _subscribedBattleOrdersManager.OnCommandProcessingFinished += HandleCommandProcessingFinished;
            }
        }
    }

    private void UnbindEvents()
    {
        if (_subscribedCameraView != null)
        {
            _subscribedCameraView.OnManualCameraInput -= HandleManualCameraInput;
            _subscribedCameraView = null;
        }

        if (_subscribedBattleOrdersManager != null)
        {
            _subscribedBattleOrdersManager.OnCommandStateChanged -= HandleCommandStateChanged;
            _subscribedBattleOrdersManager.OnCommandProcessingFinished -= HandleCommandProcessingFinished;
            _subscribedBattleOrdersManager = null;
        }
    }
}
