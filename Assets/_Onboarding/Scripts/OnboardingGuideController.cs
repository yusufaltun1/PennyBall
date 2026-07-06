using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public class OnboardingGuideController : MonoBehaviour
{
    enum GuidePhase
    {
        Inactive,
        Stage1_Drag,
        Stage2_ReleaseToShot,
        Stage3_DragAgain,
        Stage4_AlignShot,
        Stage5_Drag,
        Stage6_Power,
        Stage7_ResetCoins,
        Stage8_Drag,
        Stage9_ReleaseToShot,
        Stage10_Drag,
        Stage11_ReleaseToShot,
        Stage12_Drag,
        Stage12_PowerShot,
        Stage13_PreAlignDrag,
        Stage14_GateAlignPower,
        Stage15_Drag,
        Stage16_PowerShot,
        Stage17_Drag,
        Stage17_ReleaseToShot,
        Stage18_PowerShot,
        Completed
    }

    public static OnboardingGuideController Instance { get; private set; }

    public bool IsAimInputFrozen => _isAimInputFrozen;

    public bool IsOnboardingSceneInteractionBlocked =>
        _sceneInteractionBlockedUntilStageEight
        || _awaitingFinalGoalCelebration
        || _phase == GuidePhase.Stage7_ResetCoins
        || (_phase == GuidePhase.Completed && _onboardingCompletePending)
        || IsOnboardingCompletePanelVisible();

    /// <summary>
    /// Tek coin'li erken tutorial aşamalarında gate/InvalidMove kuralları kapalı (Aşama 1–9).
    /// </summary>
    public bool ShouldSuppressInvalidMoveRules =>
        IsSingleCoinTutorialPhase() || IsStageTwelvePracticePhase();

    /// <summary>
    /// Tutorial aşamalarında çizgi uzunluğu ve güç sabit kalır (1–2: stage one güç, 4: açı öğretimi güç).
    /// Aşama 2'de çekme yönü de kilitlenir; oyuncu sadece bırakır.
    /// </summary>
    public bool UsesFixedTutorialAimPower =>
        _phase == GuidePhase.Stage1_Drag
        || _phase == GuidePhase.Stage2_ReleaseToShot
        || _phase == GuidePhase.Stage4_AlignShot
        || _phase == GuidePhase.Stage8_Drag
        || _phase == GuidePhase.Stage9_ReleaseToShot
        || _phase == GuidePhase.Stage10_Drag
        || _phase == GuidePhase.Stage11_ReleaseToShot
        || _phase == GuidePhase.Stage17_ReleaseToShot;

    public float FixedTutorialAimPower01 =>
        _phase switch
        {
            GuidePhase.Stage4_AlignShot => _stageFourShotPower01,
            GuidePhase.Stage11_ReleaseToShot => _stageElevenResolvedPower01,
            GuidePhase.Stage17_ReleaseToShot => _stageSeventeenResolvedPower01,
            _ => _stageOneShotPower01
        };

    public float StageOneShotPower01 => _stageOneShotPower01;

    public bool ShouldUseExtendedInvalidMoveHideDelay =>
        _phase == GuidePhase.Stage18_PowerShot;

    public bool ShouldDeferGoalRoundReset =>
        _guideStarted && _phase == GuidePhase.Stage6_Power;

    public bool ShouldDeferRoundResetForOnboardingCompletion =>
        _guideStarted
        && (_phase == GuidePhase.Stage18_PowerShot
            || (_phase == GuidePhase.Completed && _onboardingCompletePending)
            || _onboardingCompletePending);

    public void NotifyPlayerGoalCelebrationStarting()
    {
        if (_phase == GuidePhase.Stage6_Power)
        {
            _sceneInteractionBlockedUntilStageEight = true;
            return;
        }

        if (_phase == GuidePhase.Stage18_PowerShot)
        {
            CompleteOnboardingAfterFinalGoal();
        }
    }

    public void OnFinalGoalCelebrationFinished()
    {
        if (_phase != GuidePhase.Stage6_Power)
        {
            return;
        }

        _awaitingFinalGoalCelebration = false;
        EnterPostTutorialReset();
    }

    public bool CanReleaseAim(CoinDragController coin)
    {
        Transform guidedCoin = GetGuidedCoinTransform();
        if (coin == null || guidedCoin == null || coin.transform != guidedCoin)
        {
            return true;
        }

        return _phase switch
        {
            GuidePhase.Stage4_AlignShot => _alignedForCurrentAim,
            GuidePhase.Stage6_Power => _powerOkForCurrentAim,
            GuidePhase.Stage12_PowerShot => _powerOkForCurrentAim,
            GuidePhase.Stage14_GateAlignPower => _powerOkForCurrentAim,
            GuidePhase.Stage16_PowerShot => _powerOkForCurrentAim,
            GuidePhase.Stage18_PowerShot => _powerOkForCurrentAim,
            _ => true
        };
    }

    static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    static readonly int HoleSoftnessId = Shader.PropertyToID("_HoleSoftness");
    static readonly int HoleAspectId = Shader.PropertyToID("_HoleAspect");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] RectTransform _guideElement;
    [SerializeField] RectTransform _pullGuideElement;
    [SerializeField] RectTransform _arrow;
    [SerializeField] RectTransform _pullArrow;
    [SerializeField] RectTransform _guideRoot;
    [SerializeField] TextMeshProUGUI _guideText;
    [SerializeField] TextMeshProUGUI _pullGuideText;
    [SerializeField] Transform _openingCoin;
    [SerializeField] Transform _sideCoinLeft;
    [SerializeField] Transform _sideCoinRight;
    [SerializeField] Transform _enemyGoal;
    [SerializeField] Transform _stageTwoShotTarget;
    [SerializeField] Transform _stageNineShotTarget;
    [SerializeField] Transform _stageElevenShotTarget;
    [SerializeField] Transform _stageTwelveShotTarget;
    [SerializeField] Transform _stageFourteenShotTarget;
    [SerializeField] Transform _stageSixteenShotTarget;
    [SerializeField] Transform _stageSeventeenShotTarget;
    [SerializeField] Transform _stageSixGoalTarget;
    [SerializeField] GameObject _onboardingCompletePanel;
    [SerializeField] Camera _worldCamera;

    [Header("Copy")]
    [SerializeField] string _dragMessage = "Drag";
    [SerializeField] string _tryAgainMessage = "Try again";
    [SerializeField] string _releaseToShotMessage = "Release to shot";
    [SerializeField] string _passBetweenCoinsMessage = "Pass between coins";
    [SerializeField] string _dragAgainMessage = "Drag again";
    [SerializeField] string _alignShotMessage = "Align the shot line between here";
    [SerializeField] string _nowReleaseMessage = "Now release";
    [SerializeField] string _releaseNowMessage = "Release now";
    [SerializeField] string _increasePowerMessage = "Increase power";

    [Header("Dev")]
    [Tooltip("Geçici: tutorial mesajlarının sonuna (Aşama no) ekler. Onboarding bitince kapatılacak.")]
    [SerializeField] bool _appendStageNumberToGuideMessages = false;

    [Header("Tuning")]
    [SerializeField] float _minPullPower01 = 0.02f;
    [SerializeField] float _stageOneShotPower01 = 0.5f;
    [SerializeField] float _stageFourShotPower01 = 0.5f;
    [SerializeField] Vector3 _stageTwoShotTargetPosition = new(1.02f, 0.1393f, 2.12f);
    [Tooltip("Aşama 9: Coin_P2 tekrar atış hedefi (sahne boşsa fallback kullanılır).")]
    [SerializeField] Vector3 _stageNineShotTargetPosition = new(1.33f, 0.1393f, 1.975f);
    [Tooltip("Aşama 12: Coin_P3 sabit açı hedefi (sahne boşsa fallback kullanılır).")]
    [SerializeField] Vector3 _stageTwelveShotTargetPosition = new(1.28f, 0.1485f, 2.55f);
    [Tooltip("Aşama 14: Coin_P3 gate atışı hayalet hedefi (sahne boşsa fallback kullanılır).")]
    [SerializeField] Vector3 _stageFourteenShotTargetPosition = new(1.408f, 0.129f, 2.344f);
    [Tooltip("Aşama 16: Sahnedeki Stage16GoalTarget (boşsa bulunur, yoksa fallback Vector3).")]
    [SerializeField] Vector3 _stageSixteenShotTargetPosition = new(1.452f, 0.1393f, 2.639f);
    [Tooltip("Aşama 17: Sahnedeki Stage17GoalTarget (boşsa bulunur, yoksa fallback Vector3).")]
    [SerializeField] Vector3 _stageSeventeenShotTargetPosition = new(1.62f, 0.1393f, 2.15f);
    [Tooltip("Aşama 11: Sahnedeki Stage11GoalTarget (boşsa bulunur, yoksa fallback Vector3).")]
    [SerializeField] Vector3 _stageElevenShotTargetPosition = new(1.682f, 0.139f, 2.026f);
    [SerializeField] float _pullTargetGap;
    [SerializeField] float _alignHalfAngleDegrees = 14f;
    [SerializeField] float _angleArrowDistanceFromCoin = 0.5f;
    [Tooltip("Aşama 6: Oyuncu çizgi ucu Stage Six Goal Target'a bu yarıçap içinde gelince Now release.")]
    [SerializeField] float _stageSixTargetMatchRadius = 0.1f;
    [SerializeField] float _coinReturnDuration = 1.2f;
    [SerializeField] float _sideCoinEnterDelay = 0.25f;
    [SerializeField] float _sideCoinEnterDuration = 0.85f;
    [Tooltip("Ekran dışından kaydırma için ekran kenarından taşma (0-1).")]
    [SerializeField] float _sideCoinSlideScreenPadding = 0.08f;
    [SerializeField] float _sideCoinSlideFallbackOffset = 1.2f;
    [SerializeField] float _ghostTargetRingRadius = 0.028f;
    [SerializeField] float _stageSixGoalTargetInset = 0f;

    [Header("Arrow")]
    [Tooltip("Okun yukarı-aşağı zıplama mesafesi (piksel). Büyütürsen ok paraya daha çok iner; küçültürsen kapatmaz.")]
    [SerializeField] float _arrowBounceHeight = 50f;
    [Tooltip("Ok animasyonunun bir yönü için süre (saniye).")]
    [SerializeField] float _arrowMoveDuration = 0.85f;
    [Tooltip("Aşama 1/3/5: Ok ucunun para merkezinin ne kadar üstünde duracağı (piksel). Parayı kapatıyorsa artır (ör. 48–72).")]
    [SerializeField] float _arrowGapAboveCoin = 48f;
    [Tooltip("Aşama 2/4/6: Ok ucunun hedef noktanın ne kadar üstünde duracağı (piksel).")]
    [SerializeField] float _pullGuideScreenOffset = 8f;
    [Tooltip("Coin Pull modunda mesaj kutusu ile ok arasındaki boşluk (piksel).")]
    [SerializeField] float _coinGuideExplanationGap = 10f;
    [Tooltip("Coin Pull modunda ok ölçeği (parayı kapatmaması için küçültülür).")]
    [SerializeField] float _coinGuideArrowScale = 0.42f;
    [Tooltip("Pull/hedef modunda ok ölçeği.")]
    [SerializeField] float _pullGuideArrowScale = 0.68f;
    [Tooltip("Explanation kutusu ile ok arasındaki sabit boşluk (piksel, pull modu).")]
    [SerializeField] float _explanationArrowGap = 24f;
    [SerializeField] float _guideElementMinWidth = 120f;

    [Header("Explanation Colors")]
    [SerializeField] Color _positiveExplanationColor = new(0.08f, 0.42f, 0.82f, 1f);
    [SerializeField] Color _misalignExplanationColor = new(0.75f, 0.05f, 0.05f, 1f);
    [SerializeField] Color _alignedAngleGuideColor = new(0.15f, 0.88f, 0.28f, 0.92f);
    [SerializeField] Color _powerLowExplanationColor = new(1f, 0.45f, 0f, 1f);

    [Header("Spotlight")]
    [SerializeField] Shader _overlayShader;
    [SerializeField] Color _overlayColor = new(0f, 0f, 0f, 0.72f);
    [SerializeField] float _holeRadiusUv = 0.14f;
    [SerializeField] float _holeSoftnessUv = 0.035f;

    enum GuideAnchorMode
    {
        Coin,
        PullTarget
    }

    RectTransform _canvasRect;
    RectTransform _overlayRect;
    Image _overlayImage;
    Material _overlayMaterial;
    OnboardingAimTutorialOverlay _tutorialOverlay;
    Image _activeExplanationImage;

    RectTransform _activeGuideElement;
    RectTransform _activeArrow;
    TextMeshProUGUI _activeGuideText;
    Vector2 _arrowBaseLocalPosition;
    Vector3 _spotlightWorldAnchor;
    Vector3 _guidePointerWorldAnchor;
    GuideAnchorMode _guideAnchorMode;
    Coroutine _arrowRoutine;
    Coroutine _flowRoutine;

    GuidePhase _phase = GuidePhase.Inactive;
    bool _guideStarted;
    bool _isAimInputFrozen;
    bool _waitingForCoinStop;
    bool _wasAimingLastFrame;
    bool _alignedForCurrentAim;
    bool _powerOkForCurrentAim;
    float _stageElevenResolvedPower01;
    float _stageSeventeenResolvedPower01;
    bool _stage18AwaitingP3Drag;
    bool _onboardingCompletePending;
    bool _awaitingFinalGoalCelebration;
    bool _sceneInteractionBlockedUntilStageEight;
    float _activeGuideVerticalOffset;

    Vector3 _centerCoinSpawnPosition;
    Quaternion _centerCoinSpawnRotation = Quaternion.identity;
    Vector3 _sideCoinLeftSpawnPosition;
    Quaternion _sideCoinLeftSpawnRotation = Quaternion.identity;
    Vector3 _sideCoinRightSpawnPosition;
    Quaternion _sideCoinRightSpawnRotation = Quaternion.identity;

    void Awake()
    {
        Instance = this;

        if (_guideRoot == null)
        {
            _guideRoot = transform as RectTransform;
        }

        _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        ResolveReferences();
        OnboardingSceneBootstrap.ApplyInitialSceneSetup();
        HideSideCoinsForEarlyTutorial();
        EnsureTutorialOverlay();
        EnsureOverlay();
        PrepareGuideElement();
        CachePositiveExplanationColorFromScene();
        HideGuideVisuals();
        SubscribeIntroFinished();
    }

    void SubscribeIntroFinished()
    {
        if (FindFirstObjectByType<MatchIntroCameraFlythrough>(FindObjectsInactive.Include) == null)
        {
            BeginGuide();
            return;
        }

        MatchIntroCameraFlythrough.Finished += OnIntroFlythroughFinished;
        StartCoroutine(WaitForIntroCompletion());
    }

    IEnumerator WaitForIntroCompletion()
    {
        while (MatchIntroCameraFlythrough.IsActive)
        {
            yield return null;
        }

        if (!_guideStarted)
        {
            BeginGuide();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_isAimInputFrozen)
        {
            Time.timeScale = 1f;
        }

        MatchIntroCameraFlythrough.Finished -= OnIntroFlythroughFinished;
    }

    void OnDisable()
    {
        if (_arrowRoutine != null)
        {
            StopCoroutine(_arrowRoutine);
            _arrowRoutine = null;
        }
    }

    void Update()
    {
        if (!IsGuideRunning())
        {
            return;
        }

        UpdateSpotlightHole();
        UpdateActiveGuideElementPosition();
        UpdatePhasePresentation();
    }

    void LateUpdate()
    {
        if (!IsGuideRunning())
        {
            return;
        }

        CoinDragController dragController = GetGuidedCoinDragController();
        bool isAiming = dragController != null && dragController.IsAiming;

        if (_waitingForCoinStop || _awaitingFinalGoalCelebration)
        {
            if (_waitingForCoinStop
                && dragController != null
                && !dragController.IsAiming
                && !dragController.IsSliding)
            {
                OnCoinStopped();
            }

            _wasAimingLastFrame = isAiming;
            return;
        }

        switch (_phase)
        {
            case GuidePhase.Stage1_Drag:
                HandleStage1(isAiming, dragController);
                break;
            case GuidePhase.Stage2_ReleaseToShot:
                HandleStage2(isAiming, dragController);
                break;
            case GuidePhase.Stage3_DragAgain:
                HandleStage3(isAiming);
                break;
            case GuidePhase.Stage4_AlignShot:
                HandleStage4(isAiming, dragController);
                break;
            case GuidePhase.Stage5_Drag:
                HandleStage5(isAiming);
                break;
            case GuidePhase.Stage6_Power:
                HandleStage6(isAiming, dragController);
                break;
            case GuidePhase.Stage8_Drag:
                HandleStage8(isAiming, dragController);
                break;
            case GuidePhase.Stage9_ReleaseToShot:
                HandleStage9(isAiming, dragController);
                break;
            case GuidePhase.Stage10_Drag:
                HandleStage10(isAiming, dragController);
                break;
            case GuidePhase.Stage11_ReleaseToShot:
                HandleStage11(isAiming, dragController);
                break;
            case GuidePhase.Stage12_Drag:
                HandleStage12(isAiming, dragController);
                break;
            case GuidePhase.Stage12_PowerShot:
                HandleStage12PowerShot(isAiming, dragController);
                break;
            case GuidePhase.Stage13_PreAlignDrag:
                HandleStage13PreAlignDrag(isAiming, dragController);
                break;
            case GuidePhase.Stage14_GateAlignPower:
                HandleStage14GateAlignPower(isAiming, dragController);
                break;
            case GuidePhase.Stage15_Drag:
                HandleStage15(isAiming, dragController);
                break;
            case GuidePhase.Stage16_PowerShot:
                HandleStage16PowerShot(isAiming, dragController);
                break;
            case GuidePhase.Stage17_Drag:
                HandleStage17(isAiming, dragController);
                break;
            case GuidePhase.Stage17_ReleaseToShot:
                HandleStage17ReleaseToShot(isAiming, dragController);
                break;
            case GuidePhase.Stage18_PowerShot:
                HandleStage18PowerShot(isAiming, dragController);
                break;
        }

        if (_wasAimingLastFrame && !isAiming)
        {
            OnAimReleased(dragController);
        }

        UpdateStageIdlePresentation(isAiming, dragController);
        _wasAimingLastFrame = isAiming;

        UpdateSpotlightHole();
        UpdateActiveGuideElementPosition();
        EnsureArrowAnimationRunning();
    }

    void UpdateStageIdlePresentation(bool isAiming, CoinDragController dragController)
    {
        if (_waitingForCoinStop || _awaitingFinalGoalCelebration || isAiming)
        {
            return;
        }

        if (dragController != null && dragController.IsSliding)
        {
            return;
        }

        if (_phase != GuidePhase.Stage4_AlignShot && _phase != GuidePhase.Stage6_Power)
        {
            return;
        }

        SetCoinGuideAnchors();
        HideTutorialOverlay();
        HidePullGuideVisuals();

        if (_guideElement != null && !_guideElement.gameObject.activeSelf)
        {
            ShowCoinGuideVisuals();
            SetActiveGuideText(_dragAgainMessage);
            SetExplanationBackground(_positiveExplanationColor);
        }
    }

    void HandleStage1(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage2);
    }

    void HandleStage8(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage9);
    }

    void TryEnterReleaseToShotFromDrag(CoinDragController dragController, Action<CoinDragController> enterReleaseStage)
    {
        if (!dragController.TryGetActiveAimTarget(out _, out float power01) || power01 < _minPullPower01)
        {
            return;
        }

        enterReleaseStage(dragController);
    }

    void HandleStage2(bool isAiming, CoinDragController dragController)
    {
        UpdateReleaseToShotPresentation(isAiming, dragController);
    }

    void HandleStage9(bool isAiming, CoinDragController dragController)
    {
        UpdateReleaseToShotPresentation(isAiming, dragController);
    }

    void HandleStage10(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage11);
    }

    void HandleStage11(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryUpdateStageElevenAimLock(dragController);
        UpdateReleaseToShotPresentation(isAiming, dragController);
    }

    void HandleStage12(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage12PowerShot);
    }

    void HandleStage12PowerShot(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        HideCoinGuideElementOnly();
        EnsurePullGuidePresentation();
        EnsureTutorialOverlay();

        Vector3 anchor = dragController.transform.position;
        Vector3 shotTarget = GetStageTwelveShotTarget();
        Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(anchor, shotTarget);

        var ghostPath = new CoinAimIndicator.PathVisual(anchor, shotTarget, false, shotTarget);
        _tutorialOverlay.ShowPowerGuide(ghostPath, _ghostTargetRingRadius);
        SetPullGuideAnchors(shotTarget);

        if (!dragController.TryGetActiveAimTarget(out Vector3 playerEnd, out _))
        {
            _powerOkForCurrentAim = false;
            SetActiveGuideText(_increasePowerMessage);
            SetExplanationBackground(_misalignExplanationColor);
            return;
        }

        float powerDelta = OnboardingAimTutorialOverlay.ComparePowerAlongDirection(
            anchor,
            direction,
            playerEnd,
            shotTarget);

        if (OnboardingAimTutorialOverlay.IsEndpointNearTarget(
                playerEnd,
                shotTarget,
                _stageSixTargetMatchRadius)
            || powerDelta >= 0f)
        {
            _powerOkForCurrentAim = true;
            SetActiveGuideText(_releaseNowMessage);
            SetExplanationBackground(_positiveExplanationColor);
            return;
        }

        _powerOkForCurrentAim = false;
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void HandleStage13PreAlignDrag(bool isAiming, CoinDragController dragController)
    {
        if (isAiming && dragController != null)
        {
            EnterStage14GateAlignPower(dragController);
        }
    }

    void HandleStage14GateAlignPower(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        UpdateFixedDirectionPowerShotPresentation(dragController, GetStageFourteenShotTarget());
    }

    void HandleStage15(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage16PowerShot);
    }

    void HandleStage16PowerShot(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        UpdateFixedDirectionPowerShotPresentation(dragController, GetStageSixteenShotTarget());
    }

    void HandleStage17(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        Transform guidedCoin = GetGuidedCoinTransform();
        if (guidedCoin == null || dragController.transform != guidedCoin)
        {
            return;
        }

        TryEnterReleaseToShotFromDrag(dragController, EnterStage17ReleaseToShot);
    }

    void HandleStage17ReleaseToShot(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryUpdateStageSeventeenAimLock(dragController);
        UpdateReleaseToShotPresentation(isAiming, dragController);
    }

    void HandleStage18PowerShot(bool isAiming, CoinDragController dragController)
    {
        if (_stage18AwaitingP3Drag)
        {
            if (!isAiming || dragController == null || dragController.transform != _sideCoinRight)
            {
                return;
            }

            TryEnterReleaseToShotFromDrag(dragController, EnterStage18PowerShot);
            return;
        }

        if (!isAiming || dragController == null)
        {
            return;
        }

        UpdateFixedDirectionPowerShotPresentation(dragController, GetStageSixGoalTarget());
    }

    void UpdateFixedDirectionPowerShotPresentation(CoinDragController dragController, Vector3 shotTarget)
    {
        HideCoinGuideElementOnly();
        EnsurePullGuidePresentation();
        EnsureTutorialOverlay();

        Vector3 anchor = dragController.transform.position;
        Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(anchor, shotTarget);

        var ghostPath = new CoinAimIndicator.PathVisual(anchor, shotTarget, false, shotTarget);
        _tutorialOverlay.ShowPowerGuide(ghostPath, _ghostTargetRingRadius);
        SetPullGuideAnchors(shotTarget);

        if (!dragController.TryGetActiveAimTarget(out Vector3 playerEnd, out _))
        {
            _powerOkForCurrentAim = false;
            SetActiveGuideText(_increasePowerMessage);
            SetExplanationBackground(_misalignExplanationColor);
            return;
        }

        float powerDelta = OnboardingAimTutorialOverlay.ComparePowerAlongDirection(
            anchor,
            direction,
            playerEnd,
            shotTarget);

        if (OnboardingAimTutorialOverlay.IsEndpointNearTarget(
                playerEnd,
                shotTarget,
                _stageSixTargetMatchRadius)
            || powerDelta >= 0f)
        {
            _powerOkForCurrentAim = true;
            SetActiveGuideText(_releaseNowMessage);
            SetExplanationBackground(_positiveExplanationColor);
            return;
        }

        _powerOkForCurrentAim = false;
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void UpdateReleaseToShotPresentation(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        _tutorialOverlay.HideAngleGuides();

        HideCoinGuideElementOnly();
        EnsurePullGuidePresentation();

        if (_phase == GuidePhase.Stage11_ReleaseToShot)
        {
            SetPullGuideAnchors(ResolveStageElevenShotTarget());
        }
        else if (_phase == GuidePhase.Stage17_ReleaseToShot)
        {
            SetPullGuideAnchors(ResolveStageSeventeenShotTarget());
        }
        else if (TryGetFixedPowerAimLineEnd(dragController, out Vector3 aimEnd))
        {
            SetPullGuideAnchors(aimEnd);
        }

        SetActiveGuideText(_phase switch
        {
            GuidePhase.Stage11_ReleaseToShot => _passBetweenCoinsMessage,
            GuidePhase.Stage17_ReleaseToShot => _releaseToShotMessage,
            _ => _releaseToShotMessage
        });
        SetExplanationBackground(_positiveExplanationColor);
    }

    void HandleStage3(bool isAiming)
    {
        if (isAiming)
        {
            EnterStage4();
        }
    }

    void HandleStage4(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        UpdateAngleAlignPresentation(dragController, _nowReleaseMessage, anchorAtAlignedShotLanding: true);
    }

    void UpdateAngleAlignPresentation(
        CoinDragController dragController,
        string alignedMessage,
        bool anchorAtAlignedShotLanding)
    {
        if (dragController == null || _enemyGoal == null)
        {
            return;
        }

        HideCoinGuideElementOnly();
        EnsurePullGuidePresentation();

        Vector3 coinPosition = dragController.transform.position;
        Vector3 goalCenter = GetEnemyGoalCenter();

        if (anchorAtAlignedShotLanding)
        {
            SetPullGuideAnchors(GetStageFourLandingPosition(dragController));
        }

        if (!dragController.TryGetActiveLaunchDirection(out Vector3 aimDirection))
        {
            _alignedForCurrentAim = false;
            _tutorialOverlay.ShowAngleGuides(coinPosition, goalCenter, _alignHalfAngleDegrees);
            SetActiveGuideText(_alignShotMessage);
            SetExplanationBackground(_misalignExplanationColor);
            return;
        }

        Vector3 centerDirection = OnboardingAimTutorialOverlay.GetMidAngleDirection(
            coinPosition,
            goalCenter);
        _alignedForCurrentAim = OnboardingAimTutorialOverlay.IsDirectionWithinAngleRange(
            aimDirection,
            centerDirection,
            _alignHalfAngleDegrees);

        Color angleGuideColor = _alignedForCurrentAim ? _alignedAngleGuideColor : Color.white;
        _tutorialOverlay.ShowAngleGuides(
            coinPosition,
            goalCenter,
            _alignHalfAngleDegrees,
            angleGuideColor);

        if (!anchorAtAlignedShotLanding)
        {
            SetPullGuideAnchors(OnboardingAimTutorialOverlay.GetAngleGuideArrowAnchor(
                coinPosition,
                goalCenter,
                _alignHalfAngleDegrees,
                _angleArrowDistanceFromCoin));
        }

        SetActiveGuideText(_alignedForCurrentAim ? alignedMessage : _alignShotMessage);
        SetExplanationBackground(
            _alignedForCurrentAim ? _positiveExplanationColor : _misalignExplanationColor);
    }

    Vector3 GetStageFourLandingPosition(CoinDragController dragController)
    {
        Vector3 coinPosition = dragController != null
            ? dragController.transform.position
            : GetCenterCoinPosition();

        if (dragController == null || _enemyGoal == null)
        {
            return coinPosition;
        }

        Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
            coinPosition,
            GetEnemyGoalCenter());
        return dragController.GetPathEndForDirectionAndPower(
            coinPosition,
            direction,
            _stageFourShotPower01);
    }

    bool TryGetFixedPowerAimLineEnd(CoinDragController dragController, out Vector3 aimEnd)
    {
        aimEnd = default;
        if (dragController == null)
        {
            return false;
        }

        if (_phase == GuidePhase.Stage16_PowerShot || _phase == GuidePhase.Stage18_PowerShot)
        {
            return false;
        }

        if (_phase == GuidePhase.Stage2_ReleaseToShot
            || _phase == GuidePhase.Stage9_ReleaseToShot
            || _phase == GuidePhase.Stage11_ReleaseToShot
            || _phase == GuidePhase.Stage17_ReleaseToShot)
        {
            return TryGetReleaseToShotAimEnd(dragController, out aimEnd);
        }

        if (dragController.TryGetActiveAimTarget(out aimEnd, out _))
        {
            return true;
        }

        if (_phase != GuidePhase.Stage2_ReleaseToShot
            && _phase != GuidePhase.Stage9_ReleaseToShot
            && _phase != GuidePhase.Stage11_ReleaseToShot
            && _phase != GuidePhase.Stage17_ReleaseToShot
            && _enemyGoal == null)
        {
            return false;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(coinPosition, GetEnemyGoalCenter());
        aimEnd = dragController.GetPathEndForDirectionAndPower(
            coinPosition,
            direction,
            FixedTutorialAimPower01);
        return true;
    }

    bool TryGetReleaseToShotAimEnd(CoinDragController dragController, out Vector3 aimEnd)
    {
        aimEnd = default;
        if (dragController == null)
        {
            return false;
        }

        if (_phase == GuidePhase.Stage11_ReleaseToShot)
        {
            aimEnd = ResolveStageElevenShotTarget();
            return true;
        }

        if (_phase == GuidePhase.Stage17_ReleaseToShot)
        {
            aimEnd = ResolveStageSeventeenShotTarget();
            return true;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetReleaseToShotLaunchDirection(_phase, coinPosition);
        aimEnd = dragController.GetPathEndForDirectionAndPower(
            coinPosition,
            direction,
            FixedTutorialAimPower01);
        return true;
    }

    bool TryUpdateStageElevenAimLock(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return false;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 target = ResolveStageElevenShotTarget();
        Vector3 direction = GetStageElevenLaunchDirection(coinPosition);
        dragController.LockAimDirection(direction);

        if (dragController.TryGetPower01ForWorldTarget(coinPosition, direction, target, out float power01))
        {
            _stageElevenResolvedPower01 = power01;
        }
        else
        {
            _stageElevenResolvedPower01 = _stageOneShotPower01;
        }

        dragController.SetAimPullForPower01(_stageElevenResolvedPower01);
        return true;
    }

    bool TryUpdateStageSeventeenAimLock(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return false;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 target = ResolveStageSeventeenShotTarget();
        Vector3 direction = GetStageSeventeenLaunchDirection(coinPosition);
        dragController.LockAimDirection(direction);

        if (dragController.TryGetPower01ForWorldTarget(coinPosition, direction, target, out float power01))
        {
            _stageSeventeenResolvedPower01 = power01;
        }
        else
        {
            _stageSeventeenResolvedPower01 = _stageOneShotPower01;
        }

        dragController.SetAimPullForPower01(_stageSeventeenResolvedPower01);
        return true;
    }

    void LockReleaseToShotAim(CoinDragController dragController, GuidePhase phase)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        if (phase == GuidePhase.Stage11_ReleaseToShot)
        {
            TryUpdateStageElevenAimLock(dragController);
            return;
        }

        if (phase == GuidePhase.Stage17_ReleaseToShot)
        {
            TryUpdateStageSeventeenAimLock(dragController);
            return;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetReleaseToShotLaunchDirection(phase, coinPosition);
        dragController.LockAimDirection(direction);
    }

    Vector3 ResolveStageTwoShotTarget()
    {
        if (_stageTwoShotTarget != null)
        {
            return _stageTwoShotTarget.position;
        }

        return _stageTwoShotTargetPosition;
    }

    Vector3 ResolveStageNineShotTarget()
    {
        if (_stageNineShotTarget != null)
        {
            return _stageNineShotTarget.position;
        }

        return _stageNineShotTargetPosition;
    }

    Vector3 ResolveReleaseToShotTarget(GuidePhase phase)
    {
        return phase switch
        {
            GuidePhase.Stage11_ReleaseToShot => ResolveStageElevenShotTarget(),
            GuidePhase.Stage17_ReleaseToShot => ResolveStageSeventeenShotTarget(),
            GuidePhase.Stage9_ReleaseToShot => ResolveStageNineShotTarget(),
            _ => ResolveStageTwoShotTarget()
        };
    }

    Vector3 GetReleaseToShotLaunchDirection(GuidePhase phase, Vector3 coinPosition)
    {
        return phase switch
        {
            GuidePhase.Stage11_ReleaseToShot => GetStageElevenLaunchDirection(coinPosition),
            GuidePhase.Stage17_ReleaseToShot => GetStageSeventeenLaunchDirection(coinPosition),
            GuidePhase.Stage9_ReleaseToShot => GetStageNineLaunchDirection(coinPosition),
            _ => GetStageTwoLaunchDirection(coinPosition)
        };
    }

    Vector3 ResolveStageElevenShotTarget()
    {
        Transform target = ResolveStageElevenShotTargetTransform();
        return target != null ? target.position : _stageElevenShotTargetPosition;
    }

    Transform ResolveStageElevenShotTargetTransform()
    {
        if (_stageElevenShotTarget != null)
        {
            return _stageElevenShotTarget;
        }

        GameObject targetObject = GameObject.Find("Stage11GoalTarget");
        return targetObject != null ? targetObject.transform : null;
    }

    bool TryGetStageElevenGateMidpoint(out Vector3 gateMidpoint)
    {
        gateMidpoint = default;
        if (_openingCoin == null || _sideCoinRight == null)
        {
            return false;
        }

        gateMidpoint = (_openingCoin.position + _sideCoinRight.position) * 0.5f;
        return true;
    }

    Vector3 GetStageTwoLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageTwoShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.forward : toTarget.normalized;
    }

    Vector3 GetStageNineLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageNineShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.forward : toTarget.normalized;
    }

    Vector3 GetStageElevenLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageElevenShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.right : toTarget.normalized;
    }

    Vector3 ResolveStageSeventeenShotTarget()
    {
        Transform target = ResolveStageSeventeenShotTargetTransform();
        return target != null ? target.position : _stageSeventeenShotTargetPosition;
    }

    Transform ResolveStageSeventeenShotTargetTransform()
    {
        if (_stageSeventeenShotTarget != null)
        {
            return _stageSeventeenShotTarget;
        }

        GameObject targetObject = GameObject.Find("Stage17GoalTarget");
        return targetObject != null ? targetObject.transform : null;
    }

    Vector3 GetStageSeventeenLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageSeventeenShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.right : toTarget.normalized;
    }

    bool TryGetSideCoinRightGateMidpoint(out Vector3 gateMidpoint)
    {
        gateMidpoint = default;
        if (_sideCoinLeft == null || _openingCoin == null)
        {
            return false;
        }

        gateMidpoint = (_sideCoinLeft.position + _openingCoin.position) * 0.5f;
        return true;
    }

    Vector3 GetStageFourteenShotTarget()
    {
        if (_stageFourteenShotTarget != null)
        {
            return _stageFourteenShotTarget.position;
        }

        return _stageFourteenShotTargetPosition;
    }

    Vector3 GetStageSixteenShotTarget()
    {
        Transform target = ResolveStageSixteenShotTargetTransform();
        return target != null ? target.position : _stageSixteenShotTargetPosition;
    }

    Transform ResolveStageSixteenShotTargetTransform()
    {
        if (_stageSixteenShotTarget != null)
        {
            return _stageSixteenShotTarget;
        }

        GameObject targetObject = GameObject.Find("Stage16GoalTarget");
        return targetObject != null ? targetObject.transform : null;
    }

    void HandleStage5(bool isAiming)
    {
        if (isAiming)
        {
            EnterStage6();
        }
    }

    void HandleStage6(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null || _enemyGoal == null)
        {
            return;
        }

        HideCoinGuideElementOnly();
        EnsurePullGuidePresentation();

        Vector3 anchor = dragController.transform.position;
        Vector3 goalTarget = GetStageSixGoalTarget();
        Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(anchor, goalTarget);

        var ghostPath = new CoinAimIndicator.PathVisual(anchor, goalTarget, false, goalTarget);
        _tutorialOverlay.ShowPowerGuide(ghostPath, _ghostTargetRingRadius);
        SetPullGuideAnchors(goalTarget);

        if (!dragController.TryGetActiveAimTarget(out Vector3 playerEnd, out _))
        {
            _powerOkForCurrentAim = false;
            SetActiveGuideText(_increasePowerMessage);
            SetExplanationBackground(_powerLowExplanationColor);
            return;
        }

        float powerDelta = OnboardingAimTutorialOverlay.ComparePowerAlongDirection(
            anchor,
            direction,
            playerEnd,
            goalTarget);

        if (OnboardingAimTutorialOverlay.IsEndpointNearTarget(
                playerEnd,
                goalTarget,
                _stageSixTargetMatchRadius)
            || powerDelta >= 0f)
        {
            _powerOkForCurrentAim = true;
            SetActiveGuideText(_nowReleaseMessage);
            SetExplanationBackground(_positiveExplanationColor);
            return;
        }

        _powerOkForCurrentAim = false;
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_powerLowExplanationColor);
    }

    void OnAimReleased(CoinDragController dragController)
    {
        switch (_phase)
        {
            case GuidePhase.Stage2_ReleaseToShot:
            case GuidePhase.Stage9_ReleaseToShot:
            case GuidePhase.Stage11_ReleaseToShot:
            case GuidePhase.Stage17_ReleaseToShot:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage4_AlignShot when _alignedForCurrentAim:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage6_Power when _powerOkForCurrentAim:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage12_PowerShot when _powerOkForCurrentAim:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage14_GateAlignPower when _powerOkForCurrentAim:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage16_PowerShot when _powerOkForCurrentAim:
                BeginWaitingForCoinStop();
                break;
            case GuidePhase.Stage18_PowerShot when _powerOkForCurrentAim:
                BeginWaitingForCoinStop();
                break;
        }
    }

    void OnCoinStopped()
    {
        HidePullGuideVisuals();
        HideTutorialOverlay();
        SetExplanationBackground(_positiveExplanationColor);

        if (_phase == GuidePhase.Completed)
        {
            _waitingForCoinStop = false;
            return;
        }

        if (_phase == GuidePhase.Stage6_Power)
        {
            _waitingForCoinStop = false;
            _awaitingFinalGoalCelebration = true;
            HideGuideVisuals();
            return;
        }

        _waitingForCoinStop = false;

        switch (_phase)
        {
            case GuidePhase.Stage2_ReleaseToShot:
                EnterStage3();
                break;
            case GuidePhase.Stage4_AlignShot:
                EnterStage5();
                break;
            case GuidePhase.Stage9_ReleaseToShot:
                EnterStage10();
                break;
            case GuidePhase.Stage11_ReleaseToShot:
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(AdvanceAfterNormalRulesShotResolved(() => EnterStage12(), EnterStage10));
                break;
            case GuidePhase.Stage17_ReleaseToShot:
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(AdvanceAfterNormalRulesShotResolved(
                    BeginStage18OnP3,
                    EnterStage17AfterInvalidShot));
                break;
            case GuidePhase.Stage12_PowerShot:
                EnterStage13PreAlignDrag();
                break;
            case GuidePhase.Stage14_GateAlignPower:
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(AdvanceAfterNormalRulesShotResolved(
                    () => EnterStage15(),
                    () => EnterStage13PreAlignDrag()));
                break;
            case GuidePhase.Stage16_PowerShot:
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(AdvanceAfterNormalRulesShotResolved(() => EnterStage17(), EnterStage15AfterInvalidShot));
                break;
            case GuidePhase.Stage18_PowerShot:
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(AdvanceAfterNormalRulesShotResolved(() => EnterCompleted(), EnterStage17AfterInvalidShot));
                break;
        }
    }

    IEnumerator AdvanceAfterNormalRulesShotResolved(Action onValidShot, Action onInvalidShot)
    {
        GameRulesManager rules = GameRulesManager.Instance;
        bool? shotValid = null;

        void OnResolved(CoinIdentity _, bool valid)
        {
            shotValid = valid;
        }

        if (rules != null)
        {
            rules.PlayerShotResolved += OnResolved;
        }

        try
        {
            while (rules != null && rules.IsMatchLockedForInput)
            {
                yield return null;
            }

            const float resolveTimeoutSeconds = 2f;
            float elapsed = 0f;
            while (shotValid == null && elapsed < resolveTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (rules != null)
            {
                rules.PlayerShotResolved -= OnResolved;
            }
        }

        _flowRoutine = null;

        if (shotValid == true)
        {
            onValidShot?.Invoke();
        }
        else
        {
            onInvalidShot?.Invoke();
        }
    }

    void BeginWaitingForCoinStop()
    {
        _waitingForCoinStop = true;
        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        HideTutorialOverlay();
    }

    void EnterStage2(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage2_ReleaseToShot);
    }

    void EnterStage8()
    {
        _sceneInteractionBlockedUntilStageEight = false;
        GameRulesManager.Instance?.PrepareForPostTutorialOpeningShot();

        SetPhase(GuidePhase.Stage8_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage9(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage9_ReleaseToShot);
    }

    void EnterStage10()
    {
        GameRulesManager.Instance?.PrepareForStageTenElevenGuidedShot(GetSideCoinLeftIdentity());

        SetPhase(GuidePhase.Stage10_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage11(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage11_ReleaseToShot);
    }

    void EnterStage17ReleaseToShot(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage17_ReleaseToShot);
    }

    void EnterStage12(bool afterInvalidShot = false)
    {
        GameRulesManager.Instance?.PrepareForGuidedCoinShot(GetSideCoinRightIdentity());

        HideTutorialOverlay();

        SetPhase(GuidePhase.Stage12_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(afterInvalidShot ? _tryAgainMessage : _dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage12PowerShot(CoinDragController dragController)
    {
        SetPhase(GuidePhase.Stage12_PowerShot);
        _powerOkForCurrentAim = false;
        HideTutorialOverlay();
        _tutorialOverlay.HideAngleGuides();

        Vector3 shotTarget = GetStageTwelveShotTarget();
        if (dragController != null)
        {
            Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
                dragController.transform.position,
                shotTarget);
            dragController.LockAimDirection(direction);
        }

        SetPullGuideAnchors(shotTarget);
        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void EnterStage13PreAlignDrag()
    {
        SetPhase(GuidePhase.Stage13_PreAlignDrag);
        HideTutorialOverlay();
        HidePullGuideVisuals();
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_tryAgainMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage14GateAlignPower(CoinDragController dragController)
    {
        SetPhase(GuidePhase.Stage14_GateAlignPower);
        _powerOkForCurrentAim = false;
        HideTutorialOverlay();
        _tutorialOverlay.HideAngleGuides();

        Vector3 shotTarget = GetStageFourteenShotTarget();
        if (dragController != null)
        {
            Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
                dragController.transform.position,
                shotTarget);
            dragController.LockAimDirection(direction);
        }

        SetPullGuideAnchors(shotTarget);
        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void EnterStage15(bool afterInvalidShot = false)
    {
        GameRulesManager.Instance?.PrepareForGuidedCoinShot(GetCenterCoinIdentity());

        SetPhase(GuidePhase.Stage15_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(afterInvalidShot ? _tryAgainMessage : _dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage15AfterInvalidShot()
    {
        EnterStage15(afterInvalidShot: true);
    }

    void EnterStage16PowerShot(CoinDragController dragController)
    {
        SetPhase(GuidePhase.Stage16_PowerShot);
        _powerOkForCurrentAim = false;
        HideTutorialOverlay();
        _tutorialOverlay.HideAngleGuides();

        Vector3 shotTarget = GetStageSixteenShotTarget();
        if (dragController != null)
        {
            Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
                dragController.transform.position,
                shotTarget);
            dragController.LockAimDirection(direction);
        }

        SetPullGuideAnchors(shotTarget);
        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void EnterStage17(bool afterInvalidShot = false)
    {
        _stage18AwaitingP3Drag = false;
        EnsureSideCoinLeftActive();
        GameRulesManager.Instance?.PrepareForGuidedCoinShot(GetSideCoinLeftIdentity());

        HideTutorialOverlay();

        SetPhase(GuidePhase.Stage17_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(afterInvalidShot ? _tryAgainMessage : _dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnsureSideCoinLeftActive()
    {
        if (_sideCoinLeft != null)
        {
            _sideCoinLeft.gameObject.SetActive(true);
        }
    }

    void BeginStage18OnP3()
    {
        _stage18AwaitingP3Drag = true;
        GameRulesManager.Instance?.PrepareForGuidedCoinShot(GetSideCoinRightIdentity());

        if (_sideCoinRight != null)
        {
            _sideCoinRight.gameObject.SetActive(true);
        }

        HideTutorialOverlay();
        SetPhase(GuidePhase.Stage18_PowerShot);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage17AfterInvalidShot()
    {
        EnterStage17(afterInvalidShot: true);
    }

    void EnterStage18PowerShot(CoinDragController dragController)
    {
        if (dragController != null
            && _sideCoinLeft != null
            && dragController.transform == _sideCoinLeft)
        {
            dragController.CancelAim();
            BeginStage18OnP3();
            return;
        }

        if (dragController != null
            && _sideCoinRight != null
            && dragController.transform != _sideCoinRight)
        {
            return;
        }

        _stage18AwaitingP3Drag = false;
        SetPhase(GuidePhase.Stage18_PowerShot);
        _powerOkForCurrentAim = false;
        HideTutorialOverlay();
        _tutorialOverlay.HideAngleGuides();

        Vector3 shotTarget = GetStageSixGoalTarget();
        if (dragController != null)
        {
            Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
                dragController.transform.position,
                shotTarget);
            dragController.LockAimDirection(direction);
        }

        SetPullGuideAnchors(shotTarget);
        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void EnterReleaseToShotStage(CoinDragController dragController, GuidePhase phase)
    {
        SetPhase(phase);

        _tutorialOverlay.HideAngleGuides();

        bool usesExactShotTargetAnchor = phase == GuidePhase.Stage11_ReleaseToShot
            || phase == GuidePhase.Stage17_ReleaseToShot;
        Vector3 shotTarget = ResolveReleaseToShotTarget(phase);
        LockReleaseToShotAim(dragController, phase);

        if (usesExactShotTargetAnchor)
        {
            SetPullGuideAnchors(shotTarget);
        }
        else if (TryGetFixedPowerAimLineEnd(dragController, out Vector3 aimEnd))
        {
            SetPullGuideAnchors(aimEnd);
        }
        else if (dragController != null)
        {
            Vector3 coinPosition = dragController.transform.position;
            Vector3 direction = GetReleaseToShotLaunchDirection(phase, coinPosition);
            aimEnd = dragController.GetPathEndForDirectionAndPower(
                coinPosition,
                direction,
                _stageOneShotPower01);
            SetPullGuideAnchors(aimEnd);
        }

        HideCoinGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(phase switch
        {
            GuidePhase.Stage11_ReleaseToShot => _passBetweenCoinsMessage,
            GuidePhase.Stage17_ReleaseToShot => _releaseToShotMessage,
            _ => _releaseToShotMessage
        });
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterCompleted()
    {
        CompleteOnboardingAfterFinalGoal();
        TryPresentOnboardingComplete();
    }

    void CompleteOnboardingAfterFinalGoal()
    {
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
            _flowRoutine = null;
        }

        _waitingForCoinStop = false;
        GameRulesManager.Instance?.ClearGuidedPlayableCoin();
        SetPhase(GuidePhase.Completed);
        HideGuideVisuals();
        OnboardingProgress.MarkCompleted();
        _onboardingCompletePending = true;
    }

    public void NotifyGoalEffectFinishedForOnboarding()
    {
        TryPresentOnboardingComplete();
    }

    void TryPresentOnboardingComplete()
    {
        if (!_onboardingCompletePending)
        {
            return;
        }

        PlayerGoalEffectController effect = PlayerGoalEffectController.EnsureInstance();
        if (effect != null && effect.gameObject.activeSelf)
        {
            return;
        }

        _onboardingCompletePending = false;
        ShowOnboardingCompletePanel();
    }

    void ShowOnboardingCompletePanel()
    {
        if (_onboardingCompletePanel == null)
        {
            GameObject panelObject = GameObject.Find("OnboardingComplete");
            if (panelObject != null)
            {
                _onboardingCompletePanel = panelObject;
            }
        }

        if (_onboardingCompletePanel != null)
        {
            EnsureOnboardingCompleteController(_onboardingCompletePanel);
            _onboardingCompletePanel.SetActive(true);
        }
    }

    static void EnsureOnboardingCompleteController(GameObject panel)
    {
        if (panel.GetComponent<OnboardingCompleteController>() == null)
        {
            panel.AddComponent<OnboardingCompleteController>();
        }
    }

    bool IsOnboardingCompletePanelVisible()
    {
        return _onboardingCompletePanel != null && _onboardingCompletePanel.activeSelf;
    }

    void EnterStage3()
    {
        SetPhase(GuidePhase.Stage3_DragAgain);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_dragAgainMessage);
    }

    void EnterStage4()
    {
        SetPhase(GuidePhase.Stage4_AlignShot);
        _alignedForCurrentAim = false;

        Vector3 coinPosition = GetCenterCoinPosition();
        Vector3 goalCenter = GetEnemyGoalCenter();
        _tutorialOverlay.ShowAngleGuides(coinPosition, goalCenter, _alignHalfAngleDegrees);

        CoinDragController dragController = GetCenterCoinDragController();
        SetPullGuideAnchors(GetStageFourLandingPosition(dragController));

        HideCoinGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_alignShotMessage);
        SetExplanationBackground(_misalignExplanationColor);
    }

    void EnterStage5()
    {
        SetPhase(GuidePhase.Stage5_Drag);
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_dragMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void EnterStage6()
    {
        SetPhase(GuidePhase.Stage6_Power);
        _powerOkForCurrentAim = false;

        _tutorialOverlay.HideAngleGuides();

        CoinDragController dragController = GetCenterCoinDragController();
        Vector3 goalTarget = GetStageSixGoalTarget();
        if (dragController != null && _enemyGoal != null)
        {
            Vector3 direction = OnboardingAimTutorialOverlay.GetMidAngleDirection(
                dragController.transform.position,
                goalTarget);
            dragController.LockAimDirection(direction);
        }

        SetPullGuideAnchors(goalTarget);

        HideCoinGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(_increasePowerMessage);
        SetExplanationBackground(_powerLowExplanationColor);
    }

    void EnterPostTutorialReset()
    {
        SetPhase(GuidePhase.Stage7_ResetCoins);
        HideGuideVisuals();

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        _flowRoutine = StartCoroutine(ResetCoinsThenComplete());
    }

    IEnumerator ResetCoinsThenComplete()
    {
        yield return ReturnCenterCoinHome();
        yield return new WaitForSeconds(_sideCoinEnterDelay);
        yield return SlideSideCoinsIntoPlace();

        EnterStage8();
        _flowRoutine = null;
    }

    IEnumerator ReturnCenterCoinHome()
    {
        if (_openingCoin == null)
        {
            yield break;
        }

        CoinDragController dragController = _openingCoin.GetComponent<CoinDragController>();
        Rigidbody rigidbody = _openingCoin.GetComponent<Rigidbody>();
        if (dragController != null)
        {
            dragController.CancelAim();
        }

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
        }

        Vector3 startPosition = _openingCoin.position;
        Quaternion startRotation = _openingCoin.rotation;
        float elapsed = 0f;

        while (elapsed < _coinReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _coinReturnDuration));
            Vector3 nextPosition = Vector3.Lerp(startPosition, _centerCoinSpawnPosition, t);
            Quaternion nextRotation = Quaternion.Slerp(startRotation, _centerCoinSpawnRotation, t);

            _openingCoin.SetPositionAndRotation(nextPosition, nextRotation);
            if (rigidbody != null)
            {
                rigidbody.position = nextPosition;
                rigidbody.rotation = nextRotation;
            }

            yield return null;
        }

        ApplyCoinPose(_openingCoin, rigidbody, dragController, _centerCoinSpawnPosition, _centerCoinSpawnRotation);
        dragController?.UnfreezeAfterGoal();
    }

    IEnumerator SlideSideCoinsIntoPlace()
    {
        int pendingSlides = 0;
        if (_sideCoinLeft != null)
        {
            pendingSlides++;
        }

        if (_sideCoinRight != null)
        {
            pendingSlides++;
        }

        if (pendingSlides == 0)
        {
            yield break;
        }

        int completedSlides = 0;
        if (_sideCoinLeft != null)
        {
            StartCoroutine(SlideSideCoinIntoPlace(
                _sideCoinLeft,
                _sideCoinLeftSpawnPosition,
                _sideCoinLeftSpawnRotation,
                fromLeft: true,
                () => completedSlides++));
        }

        if (_sideCoinRight != null)
        {
            StartCoroutine(SlideSideCoinIntoPlace(
                _sideCoinRight,
                _sideCoinRightSpawnPosition,
                _sideCoinRightSpawnRotation,
                fromLeft: false,
                () => completedSlides++));
        }

        while (completedSlides < pendingSlides)
        {
            yield return null;
        }
    }

    IEnumerator SlideSideCoinIntoPlace(
        Transform coinTransform,
        Vector3 targetPosition,
        Quaternion targetRotation,
        bool fromLeft,
        Action onComplete)
    {
        if (coinTransform == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        CoinDragController dragController = coinTransform.GetComponent<CoinDragController>();
        Rigidbody rigidbody = coinTransform.GetComponent<Rigidbody>();

        coinTransform.gameObject.SetActive(true);
        dragController?.CancelAim();
        dragController?.ForceStopSliding();
        dragController?.ResetVisualRotation();

        if (rigidbody != null)
        {
            rigidbody.isKinematic = true;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        Vector3 startPosition = ResolveSideCoinSlideStart(targetPosition, fromLeft);
        ApplyCoinPose(coinTransform, rigidbody, dragController, startPosition, targetRotation);

        float elapsed = 0f;
        while (elapsed < _sideCoinEnterDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _sideCoinEnterDuration));
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, t);
            ApplyCoinPose(coinTransform, rigidbody, dragController, nextPosition, targetRotation);
            yield return null;
        }

        ApplyCoinPose(coinTransform, rigidbody, dragController, targetPosition, targetRotation);
        dragController?.UnfreezeAfterGoal();
        onComplete?.Invoke();
    }

    Vector3 ResolveSideCoinSlideStart(Vector3 targetPosition, bool fromLeft)
    {
        Camera worldCamera = ResolveWorldCamera();
        if (worldCamera != null)
        {
            float screenX = fromLeft
                ? -_sideCoinSlideScreenPadding
                : 1f + _sideCoinSlideScreenPadding;
            Ray ray = worldCamera.ScreenPointToRay(
                new Vector3(screenX * Screen.width, Screen.height * 0.5f, 0f));
            var tablePlane = new Plane(Vector3.up, new Vector3(0f, targetPosition.y, 0f));
            if (tablePlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            Vector3 cameraRight = worldCamera.transform.right;
            cameraRight.y = 0f;
            if (cameraRight.sqrMagnitude > 0.0001f)
            {
                cameraRight.Normalize();
                float direction = fromLeft ? -1f : 1f;
                return targetPosition + cameraRight * (_sideCoinSlideFallbackOffset * direction);
            }
        }

        float fallbackX = fromLeft ? -_sideCoinSlideFallbackOffset : _sideCoinSlideFallbackOffset;
        return targetPosition + new Vector3(fallbackX, 0f, 0f);
    }

    static void ApplyCoinPose(
        Transform coinTransform,
        Rigidbody rigidbody,
        CoinDragController dragController,
        Vector3 position,
        Quaternion rotation)
    {
        if (coinTransform == null)
        {
            return;
        }

        coinTransform.SetPositionAndRotation(position, rotation);
        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = position;
            rigidbody.rotation = rotation;
            rigidbody.isKinematic = false;
        }

        dragController?.ResetVisualRotation();
    }

    void UpdatePhasePresentation()
    {
        if (_phase == GuidePhase.Stage1_Drag
            || _phase == GuidePhase.Stage3_DragAgain
            || _phase == GuidePhase.Stage5_Drag
            || _phase == GuidePhase.Stage8_Drag
            || _phase == GuidePhase.Stage10_Drag
            || _phase == GuidePhase.Stage12_Drag
            || _phase == GuidePhase.Stage13_PreAlignDrag
            || _phase == GuidePhase.Stage15_Drag
            || _phase == GuidePhase.Stage17_Drag
            || (_phase == GuidePhase.Stage18_PowerShot && _stage18AwaitingP3Drag))
        {
            SetCoinGuideAnchors();
            if (_guideElement != null && _guideElement.gameObject.activeSelf)
            {
                EnsureArrowAnimationRunning();
            }
        }
        else if (_phase == GuidePhase.Stage2_ReleaseToShot
                 || _phase == GuidePhase.Stage4_AlignShot
                 || _phase == GuidePhase.Stage6_Power
                 || _phase == GuidePhase.Stage9_ReleaseToShot
                 || _phase == GuidePhase.Stage11_ReleaseToShot
                 || _phase == GuidePhase.Stage17_ReleaseToShot
                 || _phase == GuidePhase.Stage12_PowerShot
                 || _phase == GuidePhase.Stage14_GateAlignPower
                 || _phase == GuidePhase.Stage16_PowerShot
                 || (_phase == GuidePhase.Stage18_PowerShot && !_stage18AwaitingP3Drag))
        {
            if (_pullGuideElement != null && _pullGuideElement.gameObject.activeSelf)
            {
                EnsureArrowAnimationRunning();
            }
        }

        if ((_phase == GuidePhase.Stage1_Drag
             || _phase == GuidePhase.Stage8_Drag
             || _phase == GuidePhase.Stage10_Drag
             || _phase == GuidePhase.Stage12_Drag
             || _phase == GuidePhase.Stage13_PreAlignDrag
             || _phase == GuidePhase.Stage15_Drag
            || _phase == GuidePhase.Stage17_Drag
            || (_phase == GuidePhase.Stage18_PowerShot && _stage18AwaitingP3Drag))
            && IsCenterCoinAimLineVisible())
        {
            HideCoinGuideElementOnly();
        }
        else if (_phase == GuidePhase.Stage1_Drag
                 || _phase == GuidePhase.Stage8_Drag
                 || _phase == GuidePhase.Stage10_Drag
                 || _phase == GuidePhase.Stage12_Drag
                 || _phase == GuidePhase.Stage13_PreAlignDrag
                 || _phase == GuidePhase.Stage15_Drag
                 || _phase == GuidePhase.Stage17_Drag
                 || (_phase == GuidePhase.Stage18_PowerShot && _stage18AwaitingP3Drag))
        {
            RestoreCoinGuideElementIfNeeded();
        }
    }

    bool IsGuideRunning()
    {
        return _guideStarted
               && _phase != GuidePhase.Inactive
               && _phase != GuidePhase.Completed;
    }

    /// <summary>
    /// Gate/InvalidMove kuralları Aşama 10'dan önce kapalı; sonrasında normal oyun kuralları geçerli.
    /// </summary>
    bool IsSingleCoinTutorialPhase()
    {
        if (!_guideStarted)
        {
            return false;
        }

        return _phase is GuidePhase.Stage1_Drag
            or GuidePhase.Stage2_ReleaseToShot
            or GuidePhase.Stage3_DragAgain
            or GuidePhase.Stage4_AlignShot
            or GuidePhase.Stage5_Drag
            or GuidePhase.Stage6_Power
            or GuidePhase.Stage7_ResetCoins
            or GuidePhase.Stage8_Drag
            or GuidePhase.Stage9_ReleaseToShot;
    }

    bool IsStageTwelvePracticePhase()
    {
        return _phase == GuidePhase.Stage12_Drag || _phase == GuidePhase.Stage12_PowerShot;
    }

    void SetPhase(GuidePhase phase)
    {
        _phase = phase;
        SyncInvalidMovePresenterObject();
    }

    void SyncInvalidMovePresenterObject()
    {
        GameObject invalidMove = GameObject.Find("InvalidMove");
        if (invalidMove == null)
        {
            return;
        }

        invalidMove.SetActive(!ShouldSuppressInvalidMoveRules);
    }

    void OnIntroFlythroughFinished()
    {
        BeginGuide();
    }

    void CacheCoinSpawnPoses()
    {
        _centerCoinSpawnPosition = OnboardingSceneBootstrap.CenterCoinSpawnPosition;
        _centerCoinSpawnRotation = OnboardingSceneBootstrap.CenterCoinSpawnRotation;
        _sideCoinLeftSpawnPosition = OnboardingSceneBootstrap.SideCoinLeftSpawnPosition;
        _sideCoinLeftSpawnRotation = OnboardingSceneBootstrap.SideCoinLeftSpawnRotation;
        _sideCoinRightSpawnPosition = OnboardingSceneBootstrap.SideCoinRightSpawnPosition;
        _sideCoinRightSpawnRotation = OnboardingSceneBootstrap.SideCoinRightSpawnRotation;

        if (_openingCoin != null)
        {
            if (_centerCoinSpawnPosition == Vector3.zero)
            {
                _centerCoinSpawnPosition = _openingCoin.position;
            }

            if (_centerCoinSpawnRotation == Quaternion.identity)
            {
                _centerCoinSpawnRotation = _openingCoin.rotation;
            }
        }

        if (_sideCoinLeft != null)
        {
            if (_sideCoinLeftSpawnPosition == Vector3.zero)
            {
                _sideCoinLeftSpawnPosition = _sideCoinLeft.position;
            }

            if (_sideCoinLeftSpawnRotation == Quaternion.identity)
            {
                _sideCoinLeftSpawnRotation = _sideCoinLeft.rotation;
            }
        }

        if (_sideCoinRight != null)
        {
            if (_sideCoinRightSpawnPosition == Vector3.zero)
            {
                _sideCoinRightSpawnPosition = _sideCoinRight.position;
            }

            if (_sideCoinRightSpawnRotation == Quaternion.identity)
            {
                _sideCoinRightSpawnRotation = _sideCoinRight.rotation;
            }
        }
    }

    void HideSideCoinsForEarlyTutorial()
    {
        if (_sideCoinLeft != null)
        {
            _sideCoinLeft.gameObject.SetActive(false);
        }

        if (_sideCoinRight != null)
        {
            _sideCoinRight.gameObject.SetActive(false);
        }
    }

    void BeginGuide()
    {
        if (_guideStarted || _phase != GuidePhase.Inactive)
        {
            return;
        }

        MatchIntroCameraFlythrough.Finished -= OnIntroFlythroughFinished;
        HideSideCoinsForEarlyTutorial();
        CacheCoinSpawnPoses();

        _guideStarted = true;
        SetPhase(GuidePhase.Stage1_Drag);
        SetCoinGuideAnchors();
        SetGuideText(_dragMessage);
        ShowCoinGuideVisuals();
    }

    CoinDragController GetCenterCoinDragController()
    {
        return _openingCoin != null ? _openingCoin.GetComponent<CoinDragController>() : null;
    }

    CoinDragController GetSideCoinLeftDragController()
    {
        return _sideCoinLeft != null ? _sideCoinLeft.GetComponent<CoinDragController>() : null;
    }

    CoinIdentity GetCenterCoinIdentity()
    {
        return _openingCoin != null ? _openingCoin.GetComponent<CoinIdentity>() : null;
    }

    CoinIdentity GetSideCoinLeftIdentity()
    {
        return _sideCoinLeft != null ? _sideCoinLeft.GetComponent<CoinIdentity>() : null;
    }

    CoinDragController GetSideCoinRightDragController()
    {
        return _sideCoinRight != null ? _sideCoinRight.GetComponent<CoinDragController>() : null;
    }

    CoinIdentity GetSideCoinRightIdentity()
    {
        return _sideCoinRight != null ? _sideCoinRight.GetComponent<CoinIdentity>() : null;
    }

    bool UsesSideCoinLeftGuidedCoin()
    {
        return _phase == GuidePhase.Stage10_Drag
               || _phase == GuidePhase.Stage11_ReleaseToShot
               || _phase == GuidePhase.Stage17_Drag
               || _phase == GuidePhase.Stage17_ReleaseToShot;
    }

    bool UsesCenterCoinGuidedCoin()
    {
        return _phase == GuidePhase.Stage15_Drag || _phase == GuidePhase.Stage16_PowerShot;
    }

    bool UsesSideCoinRightGuidedCoin()
    {
        return _phase == GuidePhase.Stage12_Drag
               || _phase == GuidePhase.Stage12_PowerShot
               || _phase == GuidePhase.Stage13_PreAlignDrag
               || _phase == GuidePhase.Stage14_GateAlignPower
               || _phase == GuidePhase.Stage18_PowerShot;
    }

    Transform GetGuidedCoinTransform()
    {
        if (UsesSideCoinLeftGuidedCoin())
        {
            return _sideCoinLeft;
        }

        if (UsesSideCoinRightGuidedCoin())
        {
            return _sideCoinRight;
        }

        if (UsesCenterCoinGuidedCoin())
        {
            return _openingCoin;
        }

        return _openingCoin;
    }

    CoinDragController GetGuidedCoinDragController()
    {
        if (UsesSideCoinLeftGuidedCoin())
        {
            return GetSideCoinLeftDragController();
        }

        if (UsesSideCoinRightGuidedCoin())
        {
            return GetSideCoinRightDragController();
        }

        if (UsesCenterCoinGuidedCoin())
        {
            return GetCenterCoinDragController();
        }

        return GetCenterCoinDragController();
    }

    Vector3 GetSideCoinLeftPosition()
    {
        return _sideCoinLeft != null ? _sideCoinLeft.position : Vector3.zero;
    }

    Vector3 GetSideCoinRightPosition()
    {
        return _sideCoinRight != null ? _sideCoinRight.position : Vector3.zero;
    }

    bool IsCenterCoinAimLineVisible()
    {
        CoinDragController dragController = GetGuidedCoinDragController();
        return dragController != null
               && dragController.IsAiming
               && dragController.TryGetActiveAimTarget(out _, out _);
    }

    Vector3 GetEnemyGoalCenter()
    {
        return OnboardingGoalCenter.ResolveEnemyGoalCenter(_enemyGoal);
    }

    Vector3 GetCenterCoinPosition()
    {
        return _openingCoin != null ? _openingCoin.position : Vector3.zero;
    }

    void SetCoinGuideAnchors()
    {
        Vector3 coinPosition;
        if (UsesSideCoinLeftGuidedCoin())
        {
            coinPosition = GetSideCoinLeftPosition();
        }
        else if (UsesSideCoinRightGuidedCoin())
        {
            coinPosition = GetSideCoinRightPosition();
        }
        else if (UsesCenterCoinGuidedCoin())
        {
            coinPosition = GetCenterCoinPosition();
        }
        else
        {
            coinPosition = GetCenterCoinPosition();
        }

        _spotlightWorldAnchor = coinPosition;
        _guidePointerWorldAnchor = coinPosition;
        _guideAnchorMode = GuideAnchorMode.Coin;
    }

    void SetPullGuideAnchors(Vector3 worldTarget)
    {
        _spotlightWorldAnchor = worldTarget;
        _guidePointerWorldAnchor = worldTarget;
        _guideAnchorMode = GuideAnchorMode.PullTarget;
    }

    Vector3 GetStageSixGoalTarget()
    {
        if (_stageSixGoalTarget != null)
        {
            return _stageSixGoalTarget.position;
        }

        Vector3 coinPosition = GetCenterCoinPosition();
        if (OnboardingGoalCenter.TryGetEnemyGoalInteriorPoint(
                _enemyGoal,
                coinPosition,
                _stageSixGoalTargetInset,
                out Vector3 interiorPoint))
        {
            return interiorPoint;
        }

        Vector3 goalCenter = GetEnemyGoalCenter();
        Vector3 towardGoal = OnboardingAimTutorialOverlay.GetMidAngleDirection(coinPosition, goalCenter);
        return goalCenter + towardGoal * _stageSixGoalTargetInset;
    }

    Vector3 GetStageTwelveShotTarget()
    {
        if (_stageTwelveShotTarget != null)
        {
            return _stageTwelveShotTarget.position;
        }

        return _stageTwelveShotTargetPosition;
    }

    float ResolveGuideVerticalOffset()
    {
        if (_guideAnchorMode == GuideAnchorMode.Coin)
        {
            return _arrowGapAboveCoin;
        }

        return _pullGuideScreenOffset;
    }

    bool UsesCompactCoinGuideLayout(RectTransform guideElement)
    {
        return _guideAnchorMode == GuideAnchorMode.Coin
               && guideElement == _guideElement;
    }

    void ApplyCompactCoinArrowLayout(RectTransform arrow, float arrowHeight)
    {
        arrow.anchorMin = new Vector2(0.5f, 0f);
        arrow.anchorMax = new Vector2(0.5f, 0f);
        arrow.pivot = new Vector2(0.5f, 1f);
        arrow.anchoredPosition = new Vector2(0f, arrowHeight);
    }

    void ApplyCompactCoinExplanationLayout(RectTransform explanation, float arrowHeight)
    {
        explanation.anchorMin = new Vector2(0.5f, 0f);
        explanation.anchorMax = new Vector2(0.5f, 0f);
        explanation.pivot = new Vector2(0.5f, 0f);
        explanation.anchoredPosition = new Vector2(0f, arrowHeight + _coinGuideExplanationGap);
    }

    bool UsesGateLineArrowAnimation()
    {
        return _phase == GuidePhase.Stage11_ReleaseToShot;
    }

    void StartArrowAnimation()
    {
        if (_arrowRoutine != null)
        {
            return;
        }

        _arrowBaseLocalPosition = _activeArrow != null ? _activeArrow.anchoredPosition : Vector2.zero;
        _arrowRoutine = StartCoroutine(
            UsesGateLineArrowAnimation() ? AnimateArrowAlongGateLoop() : AnimateArrowLoop());
    }

    void EnsureArrowAnimationRunning()
    {
        if (_activeArrow == null
            || _activeGuideElement == null
            || !_activeGuideElement.gameObject.activeSelf)
        {
            return;
        }

        if (_arrowRoutine != null)
        {
            return;
        }

        StartArrowAnimation();
    }

    void EnsurePullGuidePresentation()
    {
        HideCoinGuideElementOnly();
        if (_pullGuideElement == null)
        {
            return;
        }

        if (!_pullGuideElement.gameObject.activeSelf || _activeGuideElement != _pullGuideElement)
        {
            ShowPullGuideVisuals();
            return;
        }

        EnsureArrowAnimationRunning();
    }

    void ResolveReferences()
    {
        if (_canvasRect == null)
        {
            _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        }

        if (_guideElement == null && _canvasRect != null)
        {
            Transform guideElement = _canvasRect.Find("GuideElement");
            if (guideElement != null)
            {
                _guideElement = guideElement as RectTransform;
            }
        }

        if (_pullGuideElement == null && _canvasRect != null)
        {
            Transform pullGuideElement = _canvasRect.Find("GuideElement_Pull");
            if (pullGuideElement != null)
            {
                _pullGuideElement = pullGuideElement as RectTransform;
            }
        }

        if (_arrow == null && _guideElement != null)
        {
            Transform arrow = _guideElement.Find("Arrow");
            if (arrow != null)
            {
                _arrow = arrow as RectTransform;
            }
        }

        if (_pullArrow == null && _pullGuideElement != null)
        {
            Transform pullArrow = _pullGuideElement.Find("Arrow");
            if (pullArrow != null)
            {
                _pullArrow = pullArrow as RectTransform;
            }
        }

        if (_guideText == null && _guideElement != null)
        {
            _guideText = _guideElement.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_pullGuideText == null && _pullGuideElement != null)
        {
            _pullGuideText = _pullGuideElement.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_openingCoin == null)
        {
            GameObject openingCoinObject = GameObject.Find("Coin_P2");
            if (openingCoinObject != null)
            {
                _openingCoin = openingCoinObject.transform;
            }
        }

        if (_sideCoinLeft == null)
        {
            GameObject leftCoin = OnboardingSceneBootstrap.FindSceneObject("Coin_P1");
            if (leftCoin != null)
            {
                _sideCoinLeft = leftCoin.transform;
            }
        }

        if (_sideCoinRight == null)
        {
            GameObject rightCoin = OnboardingSceneBootstrap.FindSceneObject("Coin_P3");
            if (rightCoin != null)
            {
                _sideCoinRight = rightCoin.transform;
            }
        }

        if (_enemyGoal == null)
        {
            GameObject goalObject = GameObject.Find("Kale_E");
            if (goalObject != null)
            {
                _enemyGoal = goalObject.transform;
            }
        }

        if (_stageElevenShotTarget == null)
        {
            GameObject stageElevenTarget = GameObject.Find("Stage11GoalTarget");
            if (stageElevenTarget != null)
            {
                _stageElevenShotTarget = stageElevenTarget.transform;
            }
        }

        if (_stageSixteenShotTarget == null)
        {
            GameObject stageSixteenTarget = GameObject.Find("Stage16GoalTarget");
            if (stageSixteenTarget != null)
            {
                _stageSixteenShotTarget = stageSixteenTarget.transform;
            }
        }

        if (_stageSeventeenShotTarget == null)
        {
            GameObject stageSeventeenTarget = GameObject.Find("Stage17GoalTarget");
            if (stageSeventeenTarget != null)
            {
                _stageSeventeenShotTarget = stageSeventeenTarget.transform;
            }
        }

        if (_worldCamera == null)
        {
            _worldCamera = ResolveWorldCamera();
        }

        EnsurePullGuideElement();
    }

    void EnsureTutorialOverlay()
    {
        if (_tutorialOverlay == null)
        {
            _tutorialOverlay = GetComponent<OnboardingAimTutorialOverlay>();
            if (_tutorialOverlay == null)
            {
                _tutorialOverlay = gameObject.AddComponent<OnboardingAimTutorialOverlay>();
            }
        }

        _tutorialOverlay.EnsureInitialized();
    }

    void HideTutorialOverlay()
    {
        EnsureTutorialOverlay();
        _tutorialOverlay?.HideAll();
    }

    void EnsurePullGuideElement()
    {
        if (_pullGuideElement != null || _guideElement == null)
        {
            return;
        }

        GameObject clone = Instantiate(_guideElement.gameObject, _guideElement.parent);
        clone.name = "GuideElement_Pull";
        clone.SetActive(false);

        _pullGuideElement = clone.GetComponent<RectTransform>();
        Transform pullArrowTransform = _pullGuideElement.Find("Arrow");
        if (pullArrowTransform != null)
        {
            _pullArrow = pullArrowTransform as RectTransform;
        }

        _pullGuideText = _pullGuideElement.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    Camera ResolveWorldCamera()
    {
        if (_worldCamera != null)
        {
            return _worldCamera;
        }

        CoinInputHandler inputHandler = FindFirstObjectByType<CoinInputHandler>();
        if (inputHandler != null)
        {
            Camera inputCamera = inputHandler.GetComponent<Camera>();
            if (inputCamera != null)
            {
                _worldCamera = inputCamera;
                return _worldCamera;
            }
        }

        GameObject cameraObject = GameObject.Find("Camera");
        if (cameraObject != null)
        {
            Camera sceneCamera = cameraObject.GetComponent<Camera>();
            if (sceneCamera != null)
            {
                _worldCamera = sceneCamera;
                return _worldCamera;
            }
        }

        _worldCamera = Camera.main;
        return _worldCamera;
    }

    void EnsureOverlay()
    {
        if (_canvasRect == null || _overlayRect != null)
        {
            return;
        }

        if (_overlayShader == null)
        {
            _overlayShader = Shader.Find("PennyBall/UI/SpotlightOverlay");
        }

        var overlayObject = new GameObject(
            "SpotlightOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.transform.SetParent(_canvasRect, false);

        _overlayRect = overlayObject.GetComponent<RectTransform>();
        _overlayRect.anchorMin = Vector2.zero;
        _overlayRect.anchorMax = Vector2.one;
        _overlayRect.offsetMin = Vector2.zero;
        _overlayRect.offsetMax = Vector2.zero;

        int guideIndex = _guideRoot != null ? _guideRoot.GetSiblingIndex() : _canvasRect.childCount - 1;
        _overlayRect.SetSiblingIndex(guideIndex);

        _overlayImage = overlayObject.GetComponent<Image>();
        _overlayImage.raycastTarget = false;
        _overlayImage.sprite = GetWhiteSprite();
        _overlayImage.color = Color.white;

        if (_overlayShader != null)
        {
            _overlayMaterial = new Material(_overlayShader);
            _overlayMaterial.SetColor(ColorId, _overlayColor);
            _overlayMaterial.SetFloat(HoleRadiusId, _holeRadiusUv);
            _overlayMaterial.SetFloat(HoleSoftnessId, _holeSoftnessUv);
            _overlayImage.material = _overlayMaterial;
        }
        else
        {
            _overlayImage.color = _overlayColor;
        }
    }

    void PrepareGuideElement()
    {
        EnsurePullGuideElement();
        PrepareGuideElementBinding(_guideElement, ref _arrow);
        PrepareGuideElementBinding(_pullGuideElement, ref _pullArrow);
        SyncGuideElementLayout(_guideElement);
        SyncGuideElementLayout(_pullGuideElement);
        DisableGuideRaycasts(_guideElement);
        DisableGuideRaycasts(_pullGuideElement);

        if (_pullGuideElement != null)
        {
            _pullGuideElement.gameObject.SetActive(false);
        }
    }

    static void DisableGuideRaycasts(RectTransform guideElement)
    {
        if (guideElement == null)
        {
            return;
        }

        Image[] images = guideElement.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            images[i].raycastTarget = false;
        }

        TextMeshProUGUI[] texts = guideElement.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].raycastTarget = false;
        }
    }

    void PrepareGuideElementBinding(RectTransform guideElement, ref RectTransform arrow)
    {
        if (guideElement != null)
        {
            guideElement.anchorMin = new Vector2(0.5f, 0.5f);
            guideElement.anchorMax = new Vector2(0.5f, 0.5f);
            guideElement.pivot = new Vector2(0.5f, 0f);
            ConfigureExplanationLayout(guideElement);
        }

        if (arrow != null)
        {
            ConfigureArrowLayout(arrow, resetAnchoredPosition: true);

            Image arrowImage = arrow.GetComponent<Image>();
        if (arrowImage != null)
        {
            arrowImage.raycastTarget = false;
            }
        }
    }

    static void ConfigureArrowLayout(RectTransform arrow, bool resetAnchoredPosition)
    {
        if (arrow == null)
        {
            return;
        }

        arrow.anchorMin = new Vector2(0.5f, 0f);
        arrow.anchorMax = new Vector2(0.5f, 0f);
        arrow.pivot = new Vector2(0.5f, 0f);

        if (resetAnchoredPosition)
        {
            arrow.anchoredPosition = Vector2.zero;
        }

        Image arrowImage = arrow.GetComponent<Image>();
        if (arrowImage != null && arrowImage.sprite != null)
        {
            Rect spriteRect = arrowImage.sprite.rect;
            arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, spriteRect.width);
            arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, spriteRect.height);
        }
    }

    static void ConfigureExplanationLayout(RectTransform guideElement)
    {
        Transform explanation = guideElement.Find("Explanation");
        if (explanation == null)
        {
            return;
        }

        RectTransform explanationRect = explanation as RectTransform;
        if (explanationRect != null)
        {
            explanationRect.anchorMin = new Vector2(0.5f, 1f);
            explanationRect.anchorMax = new Vector2(0.5f, 1f);
            explanationRect.pivot = new Vector2(0.5f, 1f);
            explanationRect.anchoredPosition = Vector2.zero;
        }

        if (explanation.GetComponent<GuideExplanationAutoWidth>() == null)
        {
            explanation.gameObject.AddComponent<GuideExplanationAutoWidth>();
        }
    }

    void SyncGuideElementLayout(RectTransform guideElement)
    {
        if (guideElement == null)
        {
            return;
        }

        Transform arrowTransform = guideElement.Find("Arrow");
        Transform explanationTransform = guideElement.Find("Explanation");
        if (arrowTransform is not RectTransform arrow || explanationTransform is not RectTransform explanation)
        {
            return;
        }

        bool compactCoinLayout = UsesCompactCoinGuideLayout(guideElement);
        arrow.localScale = Vector3.one * (compactCoinLayout ? _coinGuideArrowScale : _pullGuideArrowScale);
        ConfigureArrowLayout(arrow, resetAnchoredPosition: compactCoinLayout);

        GuideExplanationAutoWidth autoWidth = explanation.GetComponent<GuideExplanationAutoWidth>();
        autoWidth?.Refresh();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(explanation);
        LayoutRebuilder.ForceRebuildLayoutImmediate(arrow);

        float arrowHeight = MeasureGuideChildHeight(arrow);
        float explanationHeight = MeasureGuideChildHeight(explanation);
        float arrowWidth = MeasureGuideChildWidth(arrow);
        float explanationWidth = MeasureGuideChildWidth(explanation);

        float explanationGap = compactCoinLayout ? _coinGuideExplanationGap : _explanationArrowGap;
        if (compactCoinLayout)
        {
            ApplyCompactCoinArrowLayout(arrow, arrowHeight);
            ApplyCompactCoinExplanationLayout(explanation, arrowHeight);
        }

        float width = Mathf.Max(arrowWidth, explanationWidth, _guideElementMinWidth);
        float height = arrowHeight + explanationGap + explanationHeight;
        guideElement.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        guideElement.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (compactCoinLayout)
        {
            ApplyCompactCoinArrowLayout(arrow, arrowHeight);
            ApplyCompactCoinExplanationLayout(explanation, arrowHeight);
        }
    }

    static float MeasureGuideChildHeight(RectTransform rect)
    {
        if (rect == null)
        {
            return 0f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        float height = LayoutUtility.GetPreferredHeight(rect);
        if (height <= 0.01f)
        {
            height = rect.rect.height;
        }

        return height * Mathf.Abs(rect.localScale.y);
    }

    static float MeasureGuideChildWidth(RectTransform rect)
    {
        if (rect == null)
        {
            return 0f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        float width = LayoutUtility.GetPreferredWidth(rect);
        if (width <= 0.01f)
        {
            width = rect.rect.width;
        }

        return width * Mathf.Abs(rect.localScale.x);
    }

    void RefreshExplanationLayout(RectTransform guideElement)
    {
        if (guideElement == null)
        {
            return;
        }

        Transform explanation = guideElement.Find("Explanation");
        if (explanation == null)
        {
            return;
        }

        GuideExplanationAutoWidth layout = explanation.GetComponent<GuideExplanationAutoWidth>();
        layout?.Refresh();

        SyncGuideElementLayout(guideElement);
    }

    void CachePositiveExplanationColorFromScene()
    {
        Image explanationImage = GetExplanationImage(_guideElement);
        if (explanationImage == null)
        {
            return;
        }

        Color sceneColor = explanationImage.color;
        if (sceneColor.a > 0.01f && sceneColor != Color.black)
        {
            _positiveExplanationColor = sceneColor;
        }
    }

    void HideGuideVisuals()
    {
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        HideCoinGuideVisuals();
        HidePullGuideVisuals();
        _tutorialOverlay?.HideAll();
        SetExplanationBackground(_positiveExplanationColor);
    }

    void HideCoinGuideVisuals()
    {
        if (_activeArrow == _arrow)
        {
            StopArrowAnimation();
        }

        if (_guideElement != null)
        {
            _guideElement.gameObject.SetActive(false);
        }
    }

    void HidePullGuideVisuals()
    {
        if (_activeArrow == _pullArrow)
        {
            StopArrowAnimation();
        }

        if (_pullGuideElement != null)
        {
            _pullGuideElement.gameObject.SetActive(false);
        }
    }

    void HideCoinGuideElementOnly()
    {
        if (_guideElement == null || !_guideElement.gameObject.activeSelf)
        {
            return;
        }

        if (_activeArrow == _arrow)
        {
            StopArrowAnimation();
        }

        _guideElement.gameObject.SetActive(false);
    }

    void RestoreCoinGuideElementIfNeeded()
    {
        if (_guideElement == null
            || _guideElement.gameObject.activeSelf
            || _overlayRect == null
            || !_overlayRect.gameObject.activeSelf)
        {
            return;
        }

        _activeGuideElement = _guideElement;
        _activeArrow = _arrow;
        _activeGuideText = _guideText;
        _arrowBaseLocalPosition = _arrow != null ? _arrow.anchoredPosition : Vector2.zero;
        _guideElement.gameObject.SetActive(true);

        if (_arrowRoutine == null && _arrow != null)
        {
            UpdateActiveGuideElementPosition();
            StartArrowAnimation();
        }
        else
        {
            EnsureArrowAnimationRunning();
        }
    }

    void ShowCoinGuideVisuals()
    {
        SetCoinGuideAnchors();
        _activeGuideElement = _guideElement;
        _activeArrow = _arrow;
        _activeGuideText = _guideText;
        _activeExplanationImage = GetExplanationImage(_guideElement);
        PrepareCoinGuideArrow();
        ShowActiveGuideVisuals();
    }

    void PrepareCoinGuideArrow()
    {
        if (_arrow == null)
        {
            _arrowBaseLocalPosition = Vector2.zero;
            return;
        }

        ConfigureArrowLayout(_arrow, resetAnchoredPosition: true);
        if (_guideElement != null)
        {
            SyncGuideElementLayout(_guideElement);
        }

        _arrowBaseLocalPosition = _arrow.anchoredPosition;
    }

    void ShowPullGuideVisuals()
    {
        _activeGuideElement = _pullGuideElement;
        _activeArrow = _pullArrow;
        _activeGuideText = _pullGuideText;
        _activeExplanationImage = GetExplanationImage(_pullGuideElement);
        PreparePullArrowForCurrentPhase();
        ShowActiveGuideVisuals();
    }

    void PreparePullArrowForCurrentPhase()
    {
        if (_pullArrow == null)
        {
            _arrowBaseLocalPosition = Vector2.zero;
            return;
        }

        if (UsesGateLineArrowAnimation())
        {
            _arrowBaseLocalPosition = _pullArrow.anchoredPosition;
            return;
        }

        ConfigureArrowLayout(_pullArrow, resetAnchoredPosition: true);
        if (_pullGuideElement != null)
        {
            SyncGuideElementLayout(_pullGuideElement);
        }

        _arrowBaseLocalPosition = _pullArrow.anchoredPosition;
    }

    void ShowActiveGuideVisuals()
    {
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(true);
        }

        if (_activeGuideElement != null)
        {
            _activeGuideElement.gameObject.SetActive(true);
            SyncGuideElementLayout(_activeGuideElement);
        }

        if (_activeArrow != null)
        {
            _arrowBaseLocalPosition = _activeArrow.anchoredPosition;
            _activeArrow.anchoredPosition = _arrowBaseLocalPosition;
            StopArrowAnimation();
            UpdateActiveGuideElementPosition();
            StartArrowAnimation();
        }

        UpdateSpotlightHole();
        UpdateActiveGuideElementPosition();
    }

    static Image GetExplanationImage(RectTransform guideElement)
    {
        if (guideElement == null)
        {
            return null;
        }

        Transform explanation = guideElement.Find("Explanation");
        return explanation != null ? explanation.GetComponent<Image>() : null;
    }

    void SetExplanationBackground(Color color)
    {
        if (_activeExplanationImage != null)
        {
            _activeExplanationImage.color = color;
        }
    }

    void StopArrowAnimation()
    {
        if (_arrowRoutine != null)
        {
            StopCoroutine(_arrowRoutine);
            _arrowRoutine = null;
        }
    }

    void SetActiveGuideText(string message)
    {
        if (_activeGuideText == null)
        {
            return;
        }

        string displayMessage = FormatGuideMessage(message);
        if (_activeGuideText.text == displayMessage)
        {
            return;
        }

        _activeGuideText.text = displayMessage;
        RefreshExplanationLayout(_activeGuideElement);
    }

    void SetGuideText(string message)
    {
        string displayMessage = FormatGuideMessage(message);

        if (_guideText != null)
        {
            _guideText.text = displayMessage;
        }

        if (_pullGuideText != null)
        {
            _pullGuideText.text = displayMessage;
        }

        RefreshExplanationLayout(_guideElement);
        RefreshExplanationLayout(_pullGuideElement);
    }

    string FormatGuideMessage(string message)
    {
        if (!_appendStageNumberToGuideMessages
            || string.IsNullOrEmpty(message)
            || !TryGetDisplayStageNumber(out int stageNumber))
        {
            return message;
        }

        return $"{message} ({stageNumber})";
    }

    bool TryGetDisplayStageNumber(out int stageNumber)
    {
        stageNumber = _phase switch
        {
            GuidePhase.Stage1_Drag => 1,
            GuidePhase.Stage2_ReleaseToShot => 2,
            GuidePhase.Stage3_DragAgain => 3,
            GuidePhase.Stage4_AlignShot => 4,
            GuidePhase.Stage5_Drag => 5,
            GuidePhase.Stage6_Power => 6,
            GuidePhase.Stage7_ResetCoins => 7,
            GuidePhase.Stage8_Drag => 8,
            GuidePhase.Stage9_ReleaseToShot => 9,
            GuidePhase.Stage10_Drag => 10,
            GuidePhase.Stage11_ReleaseToShot => 11,
            GuidePhase.Stage12_Drag => 12,
            GuidePhase.Stage12_PowerShot => 12,
            GuidePhase.Stage13_PreAlignDrag => 13,
            GuidePhase.Stage14_GateAlignPower => 14,
            GuidePhase.Stage15_Drag => 15,
            GuidePhase.Stage16_PowerShot => 16,
            GuidePhase.Stage17_Drag => 17,
            GuidePhase.Stage17_ReleaseToShot => 17,
            GuidePhase.Stage18_PowerShot => 18,
            _ => 0
        };

        return stageNumber > 0;
    }

    bool TryGetSpotlightWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = _spotlightWorldAnchor;
        return _guideAnchorMode == GuideAnchorMode.PullTarget || _openingCoin != null;
    }

    bool TryGetGuidePointerWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = _guidePointerWorldAnchor;
        return _guideAnchorMode == GuideAnchorMode.PullTarget || GetGuidedCoinTransform() != null;
    }

    void UpdateSpotlightHole()
    {
        Camera worldCamera = ResolveWorldCamera();
        if (_overlayMaterial == null || _overlayRect == null || worldCamera == null)
        {
            return;
        }

        if (!TryGetSpotlightWorldPosition(out Vector3 worldPosition))
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_overlayRect);

        Rect overlayRect = _overlayRect.rect;
        if (overlayRect.width <= 1f || overlayRect.height <= 1f)
        {
            return;
        }

        float aspect = overlayRect.width / overlayRect.height;
        _overlayMaterial.SetFloat(HoleAspectId, aspect);

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRect,
                screenPoint,
                GetUiCamera(),
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 holeCenter = new Vector2(
            Mathf.InverseLerp(overlayRect.xMin, overlayRect.xMax, localPoint.x),
            Mathf.InverseLerp(overlayRect.yMin, overlayRect.yMax, localPoint.y));

        _overlayMaterial.SetVector(HoleCenterId, new Vector4(holeCenter.x, holeCenter.y, 0f, 0f));
    }

    void UpdateActiveGuideElementPosition()
    {
        if (_activeGuideElement == null || !_activeGuideElement.gameObject.activeSelf)
        {
            return;
        }

        if (!TryGetGuidePointerWorldPosition(out Vector3 worldPosition))
        {
            return;
        }

        _activeGuideVerticalOffset = ResolveGuideVerticalOffset();

        if (!TryWorldToAnchoredPosition(
                _canvasRect,
                _activeGuideElement,
                worldPosition,
                _activeGuideVerticalOffset,
                out Vector2 anchoredPosition))
        {
            return;
        }

        Vector2 desiredAnchoredPosition = anchoredPosition;
        anchoredPosition = ClampGuideAnchoredPosition(anchoredPosition);
        if (_guideAnchorMode == GuideAnchorMode.Coin)
        {
            anchoredPosition.y = Mathf.Max(anchoredPosition.y, desiredAnchoredPosition.y);
        }

        _activeGuideElement.anchoredPosition = anchoredPosition;
    }

    Vector2 ClampGuideAnchoredPosition(Vector2 anchoredPosition)
    {
        if (_canvasRect == null || _activeGuideElement == null)
        {
            return anchoredPosition;
        }

        Rect parentRect = _canvasRect.rect;
        Vector2 anchorCenter = (_activeGuideElement.anchorMin + _activeGuideElement.anchorMax) * 0.5f;
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, anchorCenter.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, anchorCenter.y));

        Vector2 parentLocal = anchorReference + anchoredPosition;
        Vector2 elementSize = _activeGuideElement.rect.size;
        Vector2 pivot = _activeGuideElement.pivot;
        float margin = 12f;
        float leftExtent = elementSize.x * pivot.x;
        float rightExtent = elementSize.x * (1f - pivot.x);
        float bottomExtent = elementSize.y * pivot.y;
        float topExtent = elementSize.y * (1f - pivot.y);

        parentLocal.x = Mathf.Clamp(
            parentLocal.x,
            parentRect.xMin + leftExtent + margin,
            parentRect.xMax - rightExtent - margin);
        parentLocal.y = Mathf.Clamp(
            parentLocal.y,
            parentRect.yMin + bottomExtent + margin,
            parentRect.yMax - topExtent - margin);

        return parentLocal - anchorReference;
    }

    void ApplyArrowBounce(float bounceT)
    {
        if (_activeArrow == null)
        {
            return;
        }

        float bounceDirection = _guideAnchorMode == GuideAnchorMode.Coin ? 1f : -1f;
        float y = _arrowBaseLocalPosition.y + bounceDirection * bounceT * _arrowBounceHeight;
        _activeArrow.anchoredPosition = new Vector2(_arrowBaseLocalPosition.x, y);
    }

    bool TryWorldToAnchoredPosition(
        RectTransform parent,
        RectTransform child,
        Vector3 worldPosition,
        float extraYOffset,
        out Vector2 anchoredPosition)
    {
        anchoredPosition = default;

        Camera worldCamera = ResolveWorldCamera();
        if (parent == null || child == null || worldCamera == null)
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
            screenPoint,
            GetUiCamera(),
                out Vector2 parentLocalPoint))
        {
            return false;
        }

        anchoredPosition = ConvertParentLocalToAnchoredPosition(parent, child, parentLocalPoint);
        anchoredPosition.y += extraYOffset;
        return true;
    }

    static Vector2 ConvertParentLocalToAnchoredPosition(
        RectTransform parent,
        RectTransform child,
        Vector2 parentLocalPoint)
    {
        Rect parentRect = parent.rect;
        Vector2 anchorCenter = (child.anchorMin + child.anchorMax) * 0.5f;
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, anchorCenter.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, anchorCenter.y));

        return parentLocalPoint - anchorReference;
    }

    Camera GetUiCamera()
    {
        Canvas canvas = _canvasRect != null ? _canvasRect.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return canvas.worldCamera;
        }

        return null;
    }

    bool TryGetGateArrowEndpoints(out Vector3 start, out Vector3 end)
    {
        GateIndicator gateIndicator = GateIndicator.Instance;
        if (gateIndicator != null && gateIndicator.TryGetGateWorldEndpoints(out start, out end))
        {
            return true;
        }

        if (_openingCoin == null || _sideCoinRight == null)
        {
            start = default;
            end = default;
            return false;
        }

        const float gateLineHeightOffset = 0.004f;
        start = _openingCoin.position;
        end = _sideCoinRight.position;
        start.y += gateLineHeightOffset;
        end.y += gateLineHeightOffset;
        return true;
    }

    bool TryWorldToArrowAnchoredPosition(Vector3 worldPosition, out Vector2 arrowAnchored)
    {
        arrowAnchored = default;
        if (_activeArrow == null)
        {
            return false;
        }

        RectTransform guideElement = _activeArrow.parent as RectTransform;
        Camera worldCamera = ResolveWorldCamera();
        if (guideElement == null || worldCamera == null)
        {
            return false;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
        {
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            guideElement,
            screenPoint,
            GetUiCamera(),
            out arrowAnchored);
    }

    IEnumerator AnimateArrowAlongGateLoop()
    {
        float halfDuration = Mathf.Max(0.01f, _arrowMoveDuration * 0.5f);

        while (true)
        {
            yield return AnimateArrowAlongGateSegment(0f, 1f, halfDuration);
            yield return AnimateArrowAlongGateSegment(1f, 0f, halfDuration);
        }
    }

    IEnumerator AnimateArrowAlongGateSegment(float fromT, float toT, float duration)
    {
        if (_activeArrow == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!TryGetGateArrowEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
            {
                yield break;
            }

            UpdateActiveGuideElementPosition();

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float alongT = Mathf.LerpUnclamped(fromT, toT, t);
            Vector3 worldPosition = Vector3.Lerp(gateStart, gateEnd, alongT);
            if (TryWorldToArrowAnchoredPosition(worldPosition, out Vector2 arrowAnchored))
            {
                _activeArrow.anchoredPosition = arrowAnchored;
            }

            yield return null;
        }

        if (TryGetGateArrowEndpoints(out Vector3 finalStart, out Vector3 finalEnd))
        {
            Vector3 worldPosition = Vector3.Lerp(finalStart, finalEnd, toT);
            if (TryWorldToArrowAnchoredPosition(worldPosition, out Vector2 arrowAnchored))
            {
                _activeArrow.anchoredPosition = arrowAnchored;
            }
        }
    }

    IEnumerator AnimateArrowLoop()
    {
        float halfDuration = Mathf.Max(0.01f, _arrowMoveDuration * 0.5f);

        while (true)
        {
            yield return AnimateArrowBounce(0f, 1f, halfDuration);
            yield return AnimateArrowBounce(1f, 0f, halfDuration);
        }
    }

    IEnumerator AnimateArrowBounce(float fromT, float toT, float duration)
    {
        if (_activeArrow == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            UpdateActiveGuideElementPosition();

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float bounceT = Mathf.LerpUnclamped(fromT, toT, t);
            ApplyArrowBounce(bounceT);
            yield return null;
        }

        UpdateActiveGuideElementPosition();
        ApplyArrowBounce(toT);
    }

    static Sprite _whiteSprite;

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        _whiteSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (_whiteSprite == null)
        {
            _whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        return _whiteSprite;
    }
}
