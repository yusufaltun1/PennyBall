using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
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
        Stage6_PowerAim,
        Stage7_Drag,
        Stage8_Drag,
        Stage9_PassBetween,
        Stage10_PullAndGoal,
        Stage11_PassAndGoal,
        Completed
    }

    public static OnboardingGuideController Instance { get; private set; }

    public bool IsAimInputFrozen => _isAimInputFrozen;

    public bool IsOnboardingSceneInteractionBlocked =>
        _stageFourPostShotSequenceActive
        || _stage6PostShotSequenceActive
        || _stage7PostShotSequenceActive
        || _stage9PostShotSequenceActive
        || _stage11PostShotSequenceActive
        || _stage9AlertPending
        || IsStageSixAlertVisible()
        || IsStageSevenAlertVisible()
        || IsStageNineAlertVisible()
        || IsStageTenAlertVisible()
        || IsSuccessPanelVisible()
        || (_phase == GuidePhase.Completed && _onboardingCompletePending)
        || IsOnboardingCompletePanelVisible();

    /// <summary>
    /// Guide çalışırken (Aşama 1–8) gate/InvalidMove kuralları kapalı.
    /// Aşama 9+: gate doğrulaması açık; InvalidMove UI Aşama 9'da ayrıca bastırılır.
    /// </summary>
    public bool ShouldSuppressInvalidMoveRules =>
        IsSingleCoinTutorialPhase();

    /// <summary>
    /// Aşama 9/10: kapı kaçırılınca coin(ler) geri çekilir ama InvalidMove UI gösterilmez.
    /// </summary>
    public bool ShouldSuppressInvalidMoveFeedback =>
        _guideStarted
        && (_phase == GuidePhase.Stage9_PassBetween
            || _phase == GuidePhase.Stage11_PassAndGoal);

    /// <summary>
    /// Aşama 2 ve 4: sabit güç, kilitli açı. Aşama 1/3 serbest çekiş.
    /// </summary>
    public bool UsesFixedTutorialAimPower =>
        _phase == GuidePhase.Stage2_ReleaseToShot
        || _phase == GuidePhase.Stage4_AlignShot;

    public float FixedTutorialAimPower01 =>
        _phase == GuidePhase.Stage4_AlignShot
            ? _stageFourShotPower01
            : _stageOneShotPower01;

    public float StageOneShotPower01 => _stageOneShotPower01;

    public bool ShouldUseExtendedInvalidMoveHideDelay => false;

    public bool ShouldDeferGoalRoundReset => false;

    public bool ShouldDeferRoundResetForOnboardingCompletion =>
        _guideStarted
        && (_phase == GuidePhase.Stage11_PassAndGoal
            || _onboardingCompletePending
            || (_phase == GuidePhase.Completed && _onboardingCompletePending));

    /// <summary>
    /// Aşama 5/7/8: CoinInputHandler idle Hide'ını engelle; GateIndicator açık kalsın.
    /// </summary>
    public bool ShouldKeepOnboardingGateIndicatorVisible =>
        _guideStarted
        && (_phase == GuidePhase.Stage5_Drag
            || _phase == GuidePhase.Stage7_Drag
            || _phase == GuidePhase.Stage8_Drag)
        && !IsStageSixAlertVisible()
        && !IsStageSevenAlertVisible()
        && !IsStageNineAlertVisible()
        && !_stage6PostShotSequenceActive
        && !_stage7PostShotSequenceActive
        && !_stage9PostShotSequenceActive;

    public void NotifyPlayerGoalCelebrationStarting()
    {
        if (_phase == GuidePhase.Stage11_PassAndGoal)
        {
            _stage11ConsecutiveFails = 0;
            _stage11NoGoalFails = 0;
            CompleteOnboardingAfterFinalGoal();
        }
    }

    /// <summary>
    /// Aşama 8 gate denemesi (kodda Stage9_PassBetween): gol olsa bile Success → sonraki aşama.
    /// </summary>
    public bool ShouldTreatGoalAsStageEightGateSuccess =>
        _guideStarted && _phase == GuidePhase.Stage9_PassBetween;

    public void NotifyStageEightGateSuccess()
    {
        if (_phase != GuidePhase.Stage9_PassBetween)
        {
            return;
        }

        _stage9ConsecutiveFails = 0;
        BeginStageNineSuccessToStageTen();
    }

    public void OnFinalGoalCelebrationFinished()
    {
    }

    public bool CanReleaseAim(CoinDragController coin)
    {
        _ = coin;
        return true;
    }

    /// <summary>
    /// CoinInputHandler BeginAim hemen ardından çağırır (TryShowGateIndicator'dan önce).
    /// Aşama 8→9 geçişi burada yapılır ki GateIndicator aynı karede normal kurallarla açılsın.
    /// </summary>
    public void OnPlayerAimBegan(CoinDragController coin)
    {
        if (coin == null || !IsGuideRunning())
        {
            return;
        }

        if (_phase == GuidePhase.Stage8_Drag)
        {
            EnterStage9(coin);
            return;
        }

        if (_phase == GuidePhase.Stage10_PullAndGoal)
        {
            EnterStage11(coin);
        }
    }

    /// <summary>
    /// CoinInputHandler her karede UpdateAim'den önce çağırır.
    /// Aşama 1→2 ve 3→4 geçişleri burada yapılır; AimArrow doğrudan Inspector hedefine kilitlenir.
    /// </summary>
    public void PrepareOnboardingAimLockBeforeAimUpdate(CoinDragController coin)
    {
        if (coin == null || !coin.IsAiming)
        {
            return;
        }

        if (_phase == GuidePhase.Stage8_Drag)
        {
            EnterStage9(coin);
            return;
        }

        if (_phase == GuidePhase.Stage10_PullAndGoal)
        {
            EnterStage11(coin);
            return;
        }

        if (_phase == GuidePhase.Stage6_PowerAim)
        {
            TryUpdateStageSixAimLock(coin);
            return;
        }

        if (_phase == GuidePhase.Stage2_ReleaseToShot)
        {
            TryUpdateStageTwoAimLock(coin);
            return;
        }

        if (_phase == GuidePhase.Stage4_AlignShot)
        {
            TryUpdateStageFourAimLock(coin);
            return;
        }

        if (_phase == GuidePhase.Stage1_Drag)
        {
            if (!coin.TryGetActiveAimTarget(out _, out float power01) || power01 < _stage1MinPullPower01)
            {
                return;
            }

            TryUpdateStageTwoAimLock(coin);
            EnterStage2(coin);
            return;
        }

        if (_phase != GuidePhase.Stage3_DragAgain)
        {
            return;
        }

        if (!coin.TryGetActiveAimTarget(out _, out float stageThreePower01) || stageThreePower01 < _stage3MinPullPower01)
        {
            return;
        }

        TryUpdateStageFourAimLock(coin);
        EnterStage4(coin);
    }

    public void PrepareStageTwoAimLockBeforeAimUpdate(CoinDragController coin)
    {
        PrepareOnboardingAimLockBeforeAimUpdate(coin);
    }

    static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    static readonly int HoleSoftnessId = Shader.PropertyToID("_HoleSoftness");
    static readonly int HoleAspectId = Shader.PropertyToID("_HoleAspect");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int WedgeEnabledId = Shader.PropertyToID("_WedgeEnabled");
    static readonly int WedgeApexId = Shader.PropertyToID("_WedgeApex");
    static readonly int WedgePointLeftId = Shader.PropertyToID("_WedgePointLeft");
    static readonly int WedgePointRightId = Shader.PropertyToID("_WedgePointRight");
    static readonly int WedgeSoftnessId = Shader.PropertyToID("_WedgeSoftness");
    static readonly int GateEnabledId = Shader.PropertyToID("_GateEnabled");
    static readonly int GatePointAId = Shader.PropertyToID("_GatePointA");
    static readonly int GatePointBId = Shader.PropertyToID("_GatePointB");
    static readonly int HalfPlaneEnabledId = Shader.PropertyToID("_HalfPlaneEnabled");
    static readonly int HalfPlaneBrightPointId = Shader.PropertyToID("_HalfPlaneBrightPoint");
    static readonly int HalfPlaneSoftnessId = Shader.PropertyToID("_HalfPlaneSoftness");

    [Header("References")]
    [SerializeField] RectTransform _guideElement;
    [SerializeField] RectTransform _pullGuideElement;
    [SerializeField] RectTransform _arrow;
    [SerializeField] RectTransform _pullArrow;
    [SerializeField] RectTransform _guideRoot;
    [SerializeField] TextMeshProUGUI _guideText;
    [SerializeField] TextMeshProUGUI _pullGuideText;
    [SerializeField] Transform _openingCoin;
    [SerializeField] Transform _enemyGoal;
    [SerializeField] GameObject _onboardingCompletePanel;
    [SerializeField] Camera _worldCamera;

    [Header("Dev")]
    [Tooltip("Geçici: tutorial mesajlarının sonuna (Aşama no) ekler. Onboarding bitince kapatılacak.")]
    [SerializeField] bool _appendStageNumberToGuideMessages = true;

    [Header("Aşama 1 — Drag")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _dragMessage = "Drag";
    [Tooltip("Aşama 1→2 geçişi için minimum çekme gücü (0-1).")]
    [FormerlySerializedAs("_minPullPower01")]
    [InspectorLabel("Min Pull Power")]
    [SerializeField] float _stage1MinPullPower01 = 0.02f;
    [SerializeField] Sprite _handDefaultSprite;
    [SerializeField] Sprite _handPressedSprite;
    [Tooltip("El boyutu (piksel). Aşama 1 ve 3 paylaşır.")]
    [InspectorLabel("Hand Screen Size")]
    [SerializeField] float _stageOneHandScreenSize = 180f;
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [FormerlySerializedAs("_stageOneHandScreenOffset")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage1HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [FormerlySerializedAs("_stageOneExplanationScreenOffset")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage1ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Ok ucunun para merkezinin ne kadar üstünde duracağı (piksel). Aşama 1/3.")]
    [InspectorLabel("Arrow Gap Above Coin")]
    [SerializeField] float _arrowGapAboveCoin = 48f;
    [Tooltip("Paradan çekme mesafesi (world). Aşama 1: atış yönünün tersi.")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stageOneHandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stageOneHandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stageOneHandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stageOneHandPause = 0.15f;

    [Header("Aşama 2 — Release To Shot")]
    [FormerlySerializedAs("_releaseToShotMessage")]
    [InspectorLabel("Release Message")]
    [SerializeField] string _stage2ReleaseMessage = "Release to shot";
    [Tooltip("Inspector hedefi (Transform atanırsa pozisyon yerine bunu kullanır).")]
    [InspectorLabel("Shot Target")]
    [SerializeField] Transform _stageTwoShotTarget;
    [Tooltip("Transform boşsa AimArrow bu world noktasına kilitlenir.")]
    [InspectorLabel("Shot Target Position")]
    [SerializeField] Vector3 _stageTwoShotTargetPosition = new(1.02f, 0.1393f, 2.12f);
    [Tooltip("Aşama 2 sabit atış gücü (0-1).")]
    [InspectorLabel("Shot Power")]
    [SerializeField] float _stageOneShotPower01 = 0.5f;
    [Tooltip("Explanation offset (piksel). Maske/ok kapalıyken metin kutusu.")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage2ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Ok ucunun hedef noktanın ne kadar üstünde duracağı (piksel). Aşama 2/4.")]
    [InspectorLabel("Pull Guide Screen Offset")]
    [SerializeField] float _pullGuideScreenOffset = 8f;

    [Header("Aşama 3 — Drag Again")]
    [FormerlySerializedAs("_dragAgainMessage")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _stage3DragMessage = "Drag again";
    [Tooltip("Aşama 3→4 geçişi için minimum çekme gücü (0-1).")]
    [InspectorLabel("Min Pull Power")]
    [SerializeField] float _stage3MinPullPower01 = 0.02f;
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [FormerlySerializedAs("_stageThreeHandScreenOffset")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage3HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [FormerlySerializedAs("_stageThreeExplanationScreenOffset")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage3ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world). Aşama 3: yukarıdan aşağı (ekran).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage3HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage3HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage3HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage3HandPause = 0.15f;

    [Header("Aşama 4 — Align Shot")]
    [InspectorLabel("Release Message")]
    [SerializeField] string _stage4ReleaseMessage = "Release to shot";
    [Tooltip("Inspector hedefi (Transform atanırsa pozisyon yerine bunu kullanır).")]
    [InspectorLabel("Shot Target")]
    [SerializeField] Transform _stageFourShotTarget;
    [Tooltip("Transform boşsa AimArrow bu world noktasına kilitlenir.")]
    [InspectorLabel("Shot Target Position")]
    [SerializeField] Vector3 _stageFourShotTargetPosition = new(1.02f, 0.1393f, 2.12f);
    [Tooltip("Aşama 4 sabit atış gücü (0-1).")]
    [InspectorLabel("Shot Power")]
    [SerializeField] float _stageFourShotPower01 = 0.5f;
    [Tooltip("Explanation offset (piksel). Maske/ok kapalıyken metin kutusu.")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage4ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Atış sonrası coin'in spawn'a dönüş süresi (saniye).")]
    [SerializeField] float _coinReturnDuration = 1.2f;
    [Tooltip("Coin durduktan sonra spawn'a dönmeden önce bekleme (saniye).")]
    [FormerlySerializedAs("_stageFourToFivePause")]
    [InspectorLabel("Return Home Pause")]
    [SerializeField] float _stageFourToCompletePause = 1.5f;

    [Header("Aşama 5 — Drag")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _stage5DragMessage = "Pull";
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage5HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage5ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage5HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage5HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage5HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage5HandPause = 0.15f;

    [Header("Aşama 6 — Power Aim")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _stage6DragMessage = "Pull";
    [Tooltip("Aim ucu GateIndicator'ü geçmeden önce.")]
    [InspectorLabel("Increase Power Message")]
    [SerializeField] string _stage6IncreasePowerMessage = "Increase Power";
    [Tooltip("Aim ucu GateIndicator'ü geçince.")]
    [InspectorLabel("Release Message")]
    [SerializeField] string _stage6ReleaseMessage = "Release now";
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage6HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel). Aim ucunun hemen üstü.")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage6ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage6HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage6HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage6HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage6HandPause = 0.15f;
    [Tooltip("Explanation (Increase Power / Release) bu gücün altında gösterilmez.")]
    [InspectorLabel("Min Pull Power")]
    [SerializeField] float _stage6MinPullPower01 = 0.05f;
    [Tooltip("GateIndicator uç noktası (Stg6-Coin1).")]
    [InspectorLabel("Gate Coin 1")]
    [SerializeField] Transform _stage6GateCoin1;
    [Tooltip("GateIndicator uç noktası (Stg6-Coin2).")]
    [InspectorLabel("Gate Coin 2")]
    [SerializeField] Transform _stage6GateCoin2;
    [Tooltip("Aim yolu gate geçiş toleransı (world).")]
    [InspectorLabel("Gate Margin")]
    [SerializeField] float _stage6GateMargin = 0.09f;
    [Tooltip("Üst üste 2 başarısız atıştan sonra gösterilir.")]
    [InspectorLabel("Alert Panel")]
    [SerializeField] GameObject _stage6Alert;
    [Tooltip("Başarılı gate geçişinden sonra coin spawn'a dönmeden önce bekleme (saniye).")]
    [InspectorLabel("Success Return Home Pause")]
    [SerializeField] float _stage6SuccessReturnHomePause = 1f;

    [Header("Aşama 7 — Drag")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _stage7DragMessage = "Pull";
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage7HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage7ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage7HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage7HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage7HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage7HandPause = 0.15f;
    [Tooltip("GateIndicator uç noktası (Stg7-Coin1).")]
    [InspectorLabel("Gate Coin 1")]
    [SerializeField] Transform _stage7GateCoin1;
    [Tooltip("GateIndicator uç noktası (Stg7-Coin2).")]
    [InspectorLabel("Gate Coin 2")]
    [SerializeField] Transform _stage7GateCoin2;
    [Tooltip("GateIndicator animasyon hız çarpanı.")]
    [InspectorLabel("Gate Animation Speed Scale")]
    [SerializeField] float _stage7GateAnimationSpeedScale = 0.5f;
    [Tooltip("Coin → gate uçları yeşil rehber çizgi rengi.")]
    [InspectorLabel("Guide Line Color")]
    [SerializeField] Color _stage7GuideLineColor = new(0.15f, 0.88f, 0.28f, 0.92f);
    [Tooltip("Kama maske kenar yumuşaklığı (UV).")]
    [InspectorLabel("Wedge Softness")]
    [SerializeField] float _stage7WedgeSoftnessUv = 0.02f;
    [Tooltip("0 = yalnızca GateIndicator segmentinin içinden geçiş geçerli (uç toleransı yok).")]
    [InspectorLabel("Gate Margin")]
    [SerializeField] float _stage7GateMargin = 0f;
    [Tooltip("Üst üste 2 başarısız atıştan sonra gösterilir.")]
    [InspectorLabel("Alert Panel")]
    [SerializeField] GameObject _stage7Alert;

    [Header("Aşama 8 — Drag")]
    [InspectorLabel("Drag Message")]
    [SerializeField] string _stage8DragMessage = "Drag";
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage8HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage8ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage8HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage8HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage8HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage8HandPause = 0.15f;
    [Tooltip("Coin_P1 hedefi (Stg8-Coin1).")]
    [InspectorLabel("Target Coin 1")]
    [SerializeField] Transform _stage8Coin1Target;
    [Tooltip("Coin_P3 hedefi (Stg8-Coin2).")]
    [InspectorLabel("Target Coin 2")]
    [SerializeField] Transform _stage8Coin2Target;
    [Tooltip("Yan coinlerin hedef pozisyona hareket süresi (saniye).")]
    [InspectorLabel("Side Coin Reveal Duration")]
    [SerializeField] float _sideCoinRevealDuration = 1.0f;

    [Header("Aşama 9 — Pass Between (GameUI Rules)")]
    [Tooltip("Üst üste 2 başarısız gate geçişinden sonra gösterilir.")]
    [InspectorLabel("Alert Panel")]
    [SerializeField] GameObject _stage9Alert;
    [Tooltip("Gate kaçırılınca coinlerin başlangıca dönüş süresi (saniye).")]
    [InspectorLabel("Reset Duration")]
    [SerializeField] float _stage9CoinResetDuration = 0.75f;

    [Header("Aşama 10 — Pull and Goal")]
    [InspectorLabel("Pull Message")]
    [SerializeField] string _stage10DragMessage = "Pull and Goal";
    [Tooltip("El offset (piksel). Pozitif X sağa.")]
    [InspectorLabel("Hand Screen Offset")]
    [SerializeField] Vector2 _stage10HandScreenOffset = new(45f, 0f);
    [Tooltip("Explanation offset (piksel).")]
    [InspectorLabel("Explanation Screen Offset")]
    [SerializeField] Vector2 _stage10ExplanationScreenOffset = new(0f, 55f);
    [Tooltip("Paradan çekme mesafesi (world).")]
    [InspectorLabel("Hand Drag World Distance")]
    [SerializeField] float _stage10HandDragWorldDistance = 0.18f;
    [InspectorLabel("Hand Move Duration")]
    [SerializeField] float _stage10HandMoveDuration = 0.7f;
    [InspectorLabel("Hand Press Hold")]
    [SerializeField] float _stage10HandPressHold = 0.2f;
    [InspectorLabel("Hand Pause")]
    [SerializeField] float _stage10HandPause = 0.15f;
    [Tooltip("Coin_P1 hedefi (Stg10-Coin1).")]
    [InspectorLabel("Target Coin 1")]
    [SerializeField] Transform _stage10Coin1Target;
    [Tooltip("Coin_P3 hedefi (Stg10-Coin2).")]
    [InspectorLabel("Target Coin 2")]
    [SerializeField] Transform _stage10Coin2Target;
    [Tooltip("Coin_P2 hedefi (Stg10-CoinHit).")]
    [InspectorLabel("Target Coin Hit")]
    [SerializeField] Transform _stage10CoinHitTarget;
    [Tooltip("Coinlerin Stg10 hedeflerine hareket süresi (saniye).")]
    [InspectorLabel("Coin Move Duration")]
    [SerializeField] float _stage10CoinMoveDuration = 1.0f;

    [Header("Aşama 11 — Pass and Goal")]
    [Tooltip("Gate geçilip gol kaçırılınca üst üste 2. denemede gösterilir (Alert-10).")]
    [InspectorLabel("Alert Panel")]
    [SerializeField] GameObject _stage10Alert;
    [Tooltip("Alert kapatılınca / aşama resetinde coinlerin dönüş süresi (saniye).")]
    [InspectorLabel("Reset Duration")]
    [SerializeField] float _stage11CoinResetDuration = 0.75f;

    [Header("Shared — Success")]
    [Tooltip("Görev başarıldığında kısa süre gösterilir.")]
    [InspectorLabel("Success Panel")]
    [SerializeField] GameObject _successPanel;
    [Tooltip("Success paneli görünür kalma süresi (saniye).")]
    [InspectorLabel("Success Display Duration")]
    [SerializeField] float _successDisplayDuration = 1f;
    [Tooltip("Success paneli gösterilirken çalınır (_Onboarding/Assets/Success).")]
    [InspectorLabel("Success Sound")]
    [SerializeField] AudioClip _successSound;

    [Header("Shared — Arrow / Layout")]
    [Tooltip("Okun yukarı-aşağı zıplama mesafesi (piksel).")]
    [SerializeField] float _arrowBounceHeight = 50f;
    [Tooltip("Ok animasyonunun bir yönü için süre (saniye).")]
    [SerializeField] float _arrowMoveDuration = 0.85f;
    [Tooltip("Coin Pull modunda mesaj kutusu ile ok arasındaki boşluk (piksel).")]
    [SerializeField] float _coinGuideExplanationGap = 10f;
    [Tooltip("Coin Pull modunda ok ölçeği.")]
    [SerializeField] float _coinGuideArrowScale = 0.42f;
    [Tooltip("Pull/hedef modunda ok ölçeği.")]
    [SerializeField] float _pullGuideArrowScale = 0.68f;
    [Tooltip("Explanation kutusu ile ok arasındaki sabit boşluk (piksel, pull modu).")]
    [SerializeField] float _explanationArrowGap = 24f;
    [SerializeField] float _guideElementMinWidth = 120f;

    [Header("Shared — Colors / Spotlight")]
    [SerializeField] Color _positiveExplanationColor = new(0.08f, 0.42f, 0.82f, 1f);
    [SerializeField] Color _negativeExplanationColor = new(0.82f, 0.16f, 0.16f, 1f);
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
    Canvas _onboardingCanvas;
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
    Sprite _cachedDefaultArrowSprite;
    Vector2 _cachedDefaultArrowSize;
    Vector2 _cachedDefaultArrowPivot;
    Vector2 _cachedDefaultGuidePivot;
    bool _hasCachedDefaultArrowLayout;

    GuidePhase _phase = GuidePhase.Inactive;
    bool _guideStarted;
    bool _isAimInputFrozen;
    bool _waitingForCoinStop;
    bool _coinStopAdvanceTriggered;
    bool _stageFourPostShotSequenceActive;
    bool _stage6PostShotSequenceActive;
    bool _stage7PostShotSequenceActive;
    bool _stage9PostShotSequenceActive;
    bool _stage11PostShotSequenceActive;
    bool _stage9AlertPending;
    bool _wasAimingLastFrame;
    bool _onboardingCompletePending;
    float _activeGuideVerticalOffset;
    int _stage6ConsecutiveFails;
    int _stage7ConsecutiveFails;
    int _stage9ConsecutiveFails;
    int _stage11ConsecutiveFails;
    int _stage11NoGoalFails;
    readonly List<Vector3> _stage6ShotPathSamples = new(64);
    readonly List<Vector3> _stage7ShotPathSamples = new(64);
    Button _stage6AlertButton;
    Button _stage7AlertButton;
    Button _stage9AlertButton;
    Button _stage10AlertButton;
    AudioSource _successAudioSource;

    Vector3 _centerCoinSpawnPosition;
    Quaternion _centerCoinSpawnRotation = Quaternion.identity;
    Vector3 _stage11CenterStartPosition;
    Quaternion _stage11CenterStartRotation = Quaternion.identity;
    Vector3 _stage11LeftStartPosition;
    Quaternion _stage11LeftStartRotation = Quaternion.identity;
    Vector3 _stage11RightStartPosition;
    Quaternion _stage11RightStartRotation = Quaternion.identity;
    bool _hasStage11StartPoses;

    Vector3 _stage8CenterStartPosition;
    Quaternion _stage8CenterStartRotation = Quaternion.identity;
    Vector3 _stage8LeftStartPosition;
    Quaternion _stage8LeftStartRotation = Quaternion.identity;
    Vector3 _stage8RightStartPosition;
    Quaternion _stage8RightStartRotation = Quaternion.identity;
    bool _hasStage8StartPoses;

    void Awake()
    {
        Instance = this;

        if (_guideRoot == null)
        {
            _guideRoot = transform as RectTransform;
        }

        _onboardingCanvas = GetComponentInParent<Canvas>();
        _canvasRect = _onboardingCanvas?.GetComponent<RectTransform>();
        SetOnboardingCanvasVisible(false);
        OnboardingSceneBootstrap.EnsureSceneSetup();
        ResolveReferences();
        EnsureTutorialOverlay();
        EnsureOverlay();
        PrepareGuideElement();
        CachePositiveExplanationColorFromScene();
        HideStageSixAlert();
        HideStageSevenAlert();
        HideStageNineAlert();
        HideStageTenAlert();
        HideSuccessPanel();
        BindStageSixAlertButton();
        BindStageSevenAlertButton();
        BindStageNineAlertButton();
        BindStageTenAlertButton();
        HideGuideVisuals();
        SubscribeIntroFinished();
        SubscribeGameRulesEvents();
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
        // Awake sırası garanti değil: Flythrough kendi Awake'inde IsActive=true yapar.
        // Bir frame beklemeden kontrol edilirse guide kamera hareketinden önce açılabilir.
        yield return null;

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
        UnsubscribeGameRulesEvents();
        UnbindStageSixAlertButton();
        UnbindStageSevenAlertButton();
        UnbindStageNineAlertButton();
        UnbindStageTenAlertButton();
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

        if (_waitingForCoinStop || _stageFourPostShotSequenceActive || _stage6PostShotSequenceActive || _stage7PostShotSequenceActive || _stage9PostShotSequenceActive || _stage11PostShotSequenceActive || _stage9AlertPending || IsStageNineAlertVisible() || IsStageTenAlertVisible() || IsSuccessPanelVisible())
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

        if (_waitingForCoinStop)
        {
            if (_waitingForCoinStop
                && !_coinStopAdvanceTriggered
                && dragController != null
                && !dragController.IsAiming
                && !dragController.IsSliding)
            {
                _coinStopAdvanceTriggered = true;
                OnCoinStopped();
            }

            _wasAimingLastFrame = isAiming;
            return;
        }

        if (_stageFourPostShotSequenceActive
            || _stage6PostShotSequenceActive
            || _stage7PostShotSequenceActive
            || _stage9PostShotSequenceActive
            || _stage11PostShotSequenceActive
            || _stage9AlertPending
            || IsStageSixAlertVisible()
            || IsStageSevenAlertVisible()
            || IsStageNineAlertVisible()
            || IsStageTenAlertVisible()
            || IsSuccessPanelVisible())
        {
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
                HandleStage5(isAiming, dragController);
                break;
            case GuidePhase.Stage6_PowerAim:
                HandleStage6(isAiming, dragController);
                break;
            case GuidePhase.Stage7_Drag:
                HandleStage7(isAiming, dragController);
                break;
            case GuidePhase.Stage8_Drag:
                HandleStage8(isAiming, dragController);
                break;
            case GuidePhase.Stage9_PassBetween:
                HandleStage9(isAiming, dragController);
                break;
            case GuidePhase.Stage10_PullAndGoal:
                HandleStage10(isAiming, dragController);
                break;
            case GuidePhase.Stage11_PassAndGoal:
                HandleStage11(isAiming, dragController);
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
        if (_waitingForCoinStop
            || _stageFourPostShotSequenceActive
            || _stage6PostShotSequenceActive
            || _stage7PostShotSequenceActive
            || _stage9PostShotSequenceActive
            || _stage11PostShotSequenceActive
            || isAiming)
        {
            return;
        }

        if (dragController != null && dragController.IsSliding)
        {
            return;
        }

        if (_phase != GuidePhase.Stage2_ReleaseToShot
            && _phase != GuidePhase.Stage4_AlignShot)
        {
            return;
        }

        SetCoinGuideAnchors();
        HideTutorialOverlay();
        HidePullGuideVisuals();
        ShowGuideTextOnlyPresentation();
        SetActiveGuideText(_phase == GuidePhase.Stage4_AlignShot
            ? _stage4ReleaseMessage
            : _stage2ReleaseMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void HandleStage1(bool isAiming, CoinDragController dragController)
    {
        // Aşama 1 → 2 geçişi PrepareStageTwoAimLockBeforeAimUpdate içinde (Update, UpdateAim'den önce).
    }

    void HandleStage2(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryUpdateStageTwoAimLock(dragController);
        UpdateReleaseToShotPresentation(isAiming, dragController);
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

        if (TryGetFixedPowerAimLineEnd(dragController, out Vector3 aimEnd))
        {
            SetPullGuideAnchors(aimEnd);
        }

        SetActiveGuideText(_phase == GuidePhase.Stage4_AlignShot
            ? _stage4ReleaseMessage
            : _stage2ReleaseMessage);
        SetExplanationBackground(_positiveExplanationColor);
    }

    void HandleStage3(bool isAiming)
    {
        // Aşama 3 → 4 geçişi PrepareOnboardingAimLockBeforeAimUpdate içinde (Update, UpdateAim'den önce).
    }

    void HandleStage4(bool isAiming, CoinDragController dragController)
    {
        if (!isAiming || dragController == null)
        {
            return;
        }

        TryUpdateStageFourAimLock(dragController);
        UpdateReleaseToShotPresentation(isAiming, dragController);
    }

    void HandleStage5(bool isAiming, CoinDragController dragController)
    {
        EnsureStageSixGateVisible();
        // Yuvarlak maske yok; GateIndicator açık. Oyuncu çekmeye başlayınca Aşama 6.
        if (isAiming)
        {
            EnterStage6();
        }
    }

    void HandleStage7(bool isAiming, CoinDragController dragController)
    {
        UpdateStageSevenPresentation(isAiming);
        _ = dragController;
    }

    void HandleStage8(bool isAiming, CoinDragController dragController)
    {
        // Aşama 1 gibi: yuvarlak maske + OK/Pull. Çekmeye başlayınca Aşama 9.
        if (isAiming)
        {
            EnterStage9(dragController);
        }
    }

    void HandleStage9(bool isAiming, CoinDragController dragController)
    {
        if (isAiming)
        {
            EnsureStageNineGateVisible(dragController);
            return;
        }

        GateIndicator.Instance?.Hide();
    }

    void HandleStage10(bool isAiming, CoinDragController dragController)
    {
        // Yuvarlak maske yok; Explanation + OK. Çekmeye başlayınca Aşama 11.
        if (isAiming)
        {
            EnterStage11(dragController);
        }
    }

    void HandleStage11(bool isAiming, CoinDragController dragController)
    {
        if (isAiming)
        {
            EnsureStageElevenGateVisible(dragController);
            HideCoinGuideElementOnly();
            return;
        }

        GateIndicator.Instance?.Hide();
    }

    void UpdateStageSevenPresentation(bool isAiming)
    {
        EnsureStageSevenGateVisible();
        ShowStageSevenGuideLines();
        UpdateStageSevenWedgeSpotlight();

        if (isAiming)
        {
            HideCoinGuideElementOnly();
        }
    }

    void HandleStage6(bool isAiming, CoinDragController dragController)
    {
        if (isAiming && dragController != null)
        {
            UpdateStageSixPowerPresentation(dragController);
            return;
        }

        GateIndicator.Instance?.Hide();
        SetCoinGuideAnchors();

        if (_guideElement != null && !_guideElement.gameObject.activeSelf)
        {
            ShowCoinGuideVisuals();
            SetActiveGuideText(_stage6DragMessage);
            SetExplanationBackground(_positiveExplanationColor);
        }
    }

    void UpdateStageSixPowerPresentation(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        TryUpdateStageSixAimLock(dragController);
        EnsureStageSixGateVisible();
        HideCoinGuideElementOnly();

        if (!dragController.TryGetActiveAimTarget(out Vector3 aimEnd, out float power01)
            || power01 < _stage6MinPullPower01)
        {
            // Dokunuş / çok hafif çekmede explanation gösterme.
            HidePullGuideVisuals();
            return;
        }

        EnsurePullGuidePresentation();
        SetPullGuideAnchors(aimEnd);
        UpdateActiveGuideElementPosition();

        bool passesGate = DoesAimPassStageSixGate(dragController.transform.position, aimEnd);
        if (passesGate)
        {
            SetActiveGuideText(_stage6ReleaseMessage);
            SetExplanationBackground(_positiveExplanationColor);
        }
        else
        {
            SetActiveGuideText(_stage6IncreasePowerMessage);
            SetExplanationBackground(_negativeExplanationColor);
        }
    }

    bool DoesAimPassStageSixGate(Vector3 coinPosition, Vector3 aimTip)
    {
        if (!TryResolveStageSixGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return false;
        }

        return PassBetweenValidator.DidPassBetween(
            coinPosition,
            aimTip,
            gateStart,
            gateEnd,
            _stage6GateMargin);
    }

    bool TryGetFixedPowerAimLineEnd(CoinDragController dragController, out Vector3 aimEnd)
    {
        aimEnd = default;
        if (dragController == null)
        {
            return false;
        }

        if (_phase == GuidePhase.Stage2_ReleaseToShot
            || _phase == GuidePhase.Stage4_AlignShot)
        {
            return TryGetReleaseToShotAimEnd(dragController, out aimEnd);
        }

        if (dragController.TryGetActiveAimTarget(out aimEnd, out _))
        {
            return true;
        }

        if (_enemyGoal == null)
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

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetReleaseToShotLaunchDirection(_phase, coinPosition);
        aimEnd = dragController.GetPathEndForDirectionAndPower(
            coinPosition,
            direction,
            FixedTutorialAimPower01);
        return true;
    }

    void LockReleaseToShotAim(CoinDragController dragController, GuidePhase phase)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        if (phase == GuidePhase.Stage2_ReleaseToShot)
        {
            TryUpdateStageTwoAimLock(dragController);
            return;
        }

        if (phase == GuidePhase.Stage4_AlignShot)
        {
            TryUpdateStageFourAimLock(dragController);
        }
    }

    void TryUpdateStageTwoAimLock(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetStageTwoLaunchDirection(coinPosition);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        dragController.LockAimDirection(direction);
        dragController.SetAimPullForPower01(_stageOneShotPower01);
    }

    void TryUpdateStageFourAimLock(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetStageFourLaunchDirection(coinPosition);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        dragController.LockAimDirection(direction);
        dragController.SetAimPullForPower01(_stageFourShotPower01);
    }

    Vector3 ResolveStageTwoShotTarget()
    {
        if (_stageTwoShotTarget != null)
        {
            return _stageTwoShotTarget.position;
        }

        return _stageTwoShotTargetPosition;
    }

    Vector3 ResolveStageFourShotTarget()
    {
        if (_stageFourShotTarget != null)
        {
            return _stageFourShotTarget.position;
        }

        return _stageFourShotTargetPosition;
    }

    Vector3 GetStageFourLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageFourShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.forward : toTarget.normalized;
    }

    Vector3 GetReleaseToShotLaunchDirection(GuidePhase phase, Vector3 coinPosition)
    {
        return phase == GuidePhase.Stage4_AlignShot
            ? GetStageFourLaunchDirection(coinPosition)
            : GetStageTwoLaunchDirection(coinPosition);
    }

    Vector3 GetStageTwoLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toTarget = ResolveStageTwoShotTarget() - coinPosition;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.0001f ? Vector3.forward : toTarget.normalized;
    }

    void OnAimReleased(CoinDragController dragController)
    {
        switch (_phase)
        {
            case GuidePhase.Stage2_ReleaseToShot:
            case GuidePhase.Stage4_AlignShot:
            case GuidePhase.Stage6_PowerAim:
            case GuidePhase.Stage7_Drag:
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

        _waitingForCoinStop = false;

        switch (_phase)
        {
            case GuidePhase.Stage2_ReleaseToShot:
                EnterStage3();
                break;
            case GuidePhase.Stage4_AlignShot:
                if (_stageFourPostShotSequenceActive)
                {
                    break;
                }

                _stageFourPostShotSequenceActive = true;
                HideGuideVisuals();
                if (_flowRoutine != null)
                {
                    StopCoroutine(_flowRoutine);
                }

                _flowRoutine = StartCoroutine(ReturnCenterCoinHomeThenComplete());
                break;
            case GuidePhase.Stage6_PowerAim:
                HandleStageSixShotResolved();
                break;
            case GuidePhase.Stage7_Drag:
                HandleStageSevenShotResolved();
                break;
        }
    }

    void HandleStageSixShotResolved()
    {
        GateIndicator.Instance?.Hide();

        if (DidStageSixShotPassGate())
        {
            _stage6ConsecutiveFails = 0;
            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
            }

            _flowRoutine = StartCoroutine(StageSixSuccessThenStage7Routine());
            return;
        }

        _stage6ConsecutiveFails++;
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        if (_stage6ConsecutiveFails >= 2)
        {
            _flowRoutine = StartCoroutine(StageSixShowAlertRoutine());
        }
        else
        {
            _flowRoutine = StartCoroutine(StageSixMissRetryRoutine());
        }
    }

    bool DidStageSixShotPassGate()
    {
        if (!TryResolveStageSixGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return false;
        }

        _stage6ShotPathSamples.Clear();
        CoinDragController dragController = GetCenterCoinDragController();
        if (dragController != null)
        {
            dragController.CopySlidePathTo(_stage6ShotPathSamples);
        }

        if (_stage6ShotPathSamples.Count < 2)
        {
            return false;
        }

        return PassBetweenValidator.DidPassBetweenAlongPath(
            _stage6ShotPathSamples,
            gateStart,
            gateEnd,
            _stage6GateMargin);
    }

    IEnumerator StageSixMissRetryRoutine()
    {
        _stage6PostShotSequenceActive = true;
        HideGuideVisuals();
        GateIndicator.Instance?.Hide();
        yield return ReturnCenterCoinHome();
        _stage6PostShotSequenceActive = false;
        EnterStage6();
        _flowRoutine = null;
    }

    IEnumerator StageSixSuccessThenStage7Routine()
    {
        _stage6PostShotSequenceActive = true;
        HideGuideVisuals();
        GateIndicator.Instance?.Hide();
        yield return ShowSuccessBriefly();
        yield return ReturnCenterCoinHome();
        _stage6PostShotSequenceActive = false;
        EnterStage7();
        _flowRoutine = null;
    }

    IEnumerator StageSixShowAlertRoutine()
    {
        _stage6PostShotSequenceActive = true;
        HideGuideVisuals();
        GateIndicator.Instance?.Hide();
        ShowStageSixAlert();
        _flowRoutine = null;
        yield break;
    }

    IEnumerator StageSixAlertDismissRoutine()
    {
        HideStageSixAlert();
        _stage6PostShotSequenceActive = true;
        yield return ReturnCenterCoinHome();
        _stage6ConsecutiveFails = 0;
        _stage6PostShotSequenceActive = false;
        EnterStage6();
        _flowRoutine = null;
    }

    void HandleStageSevenShotResolved()
    {
        GateIndicator.Instance?.Hide();
        HideTutorialOverlay();

        if (DidStageSevenShotPassGate())
        {
            _stage7ConsecutiveFails = 0;
            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
            }

            _flowRoutine = StartCoroutine(StageSevenSuccessThenStage8Routine());
            return;
        }

        _stage7ConsecutiveFails++;
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        if (_stage7ConsecutiveFails >= 2)
        {
            _flowRoutine = StartCoroutine(StageSevenShowAlertRoutine());
        }
        else
        {
            _flowRoutine = StartCoroutine(StageSevenMissRetryRoutine());
        }
    }

    bool DidStageSevenShotPassGate()
    {
        if (!TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return false;
        }

        _stage7ShotPathSamples.Clear();
        CoinDragController dragController = GetCenterCoinDragController();
        if (dragController != null)
        {
            dragController.CopySlidePathTo(_stage7ShotPathSamples);
        }

        if (_stage7ShotPathSamples.Count < 2)
        {
            return false;
        }

        return PassBetweenValidator.DidPassBetweenAlongPath(
            _stage7ShotPathSamples,
            gateStart,
            gateEnd,
            Mathf.Max(0f, _stage7GateMargin));
    }

    IEnumerator StageSevenMissRetryRoutine()
    {
        _stage7PostShotSequenceActive = true;
        HideGuideVisuals();
        HideTutorialOverlay();
        GateIndicator.Instance?.Hide();
        yield return ReturnCenterCoinHome();
        _stage7PostShotSequenceActive = false;
        EnterStage7();
        _flowRoutine = null;
    }

    IEnumerator StageSevenSuccessThenStage8Routine()
    {
        _stage7PostShotSequenceActive = true;
        HideGuideVisuals();
        HideTutorialOverlay();
        GateIndicator.Instance?.Hide();
        yield return ShowSuccessBriefly();
        yield return ReturnCenterCoinHome();
        yield return RevealSideCoinsToGameUiPositions();
        _stage7PostShotSequenceActive = false;
        EnterStage8(resetRelatedFailCounts: true);
        _flowRoutine = null;
    }

    IEnumerator RevealSideCoinsToGameUiPositions()
    {
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);
        if (leftCoin == null && rightCoin == null)
        {
            Debug.LogWarning("Onboarding: Coin_P1 / Coin_P3 bulunamadı; Aşama 8 yan coin reveal atlandı.");
            yield break;
        }

        OnboardingSceneBootstrap.CacheSideCoinReferences(leftCoin, rightCoin);

        if (leftCoin != null)
        {
            leftCoin.gameObject.SetActive(true);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            rightCoin.gameObject.SetActive(true);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        Vector3 leftStart = leftCoin != null ? leftCoin.position : default;
        Quaternion leftRot = leftCoin != null ? leftCoin.rotation : Quaternion.identity;
        Vector3 rightStart = rightCoin != null ? rightCoin.position : default;
        Quaternion rightRot = rightCoin != null ? rightCoin.rotation : Quaternion.identity;

        if (!TryResolveStageEightSideCoinTargetPositions(out Vector3 leftTarget, out Vector3 rightTarget))
        {
            Debug.LogWarning("Onboarding: Stg8-Coin1 / Stg8-Coin2 hedefi eksik; yan coin reveal atlandı.");
            yield break;
        }

        Rigidbody leftBody = leftCoin != null ? leftCoin.GetComponent<Rigidbody>() : null;
        Rigidbody rightBody = rightCoin != null ? rightCoin.GetComponent<Rigidbody>() : null;
        CoinDragController leftDrag = leftCoin != null ? leftCoin.GetComponent<CoinDragController>() : null;
        CoinDragController rightDrag = rightCoin != null ? rightCoin.GetComponent<CoinDragController>() : null;

        leftDrag?.CancelAim();
        rightDrag?.CancelAim();
        if (leftBody != null)
        {
            leftBody.linearVelocity = Vector3.zero;
            leftBody.angularVelocity = Vector3.zero;
            leftBody.isKinematic = true;
        }

        if (rightBody != null)
        {
            rightBody.linearVelocity = Vector3.zero;
            rightBody.angularVelocity = Vector3.zero;
            rightBody.isKinematic = true;
        }

        float duration = _sideCoinRevealDuration > 0.01f ? _sideCoinRevealDuration : 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (leftCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(leftStart, leftTarget, t);
                leftCoin.SetPositionAndRotation(nextPos, leftRot);
                if (leftBody != null)
                {
                    leftBody.position = nextPos;
                    leftBody.rotation = leftRot;
                }
            }

            if (rightCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(rightStart, rightTarget, t);
                rightCoin.SetPositionAndRotation(nextPos, rightRot);
                if (rightBody != null)
                {
                    rightBody.position = nextPos;
                    rightBody.rotation = rightRot;
                }
            }

            yield return null;
        }

        if (leftCoin != null)
        {
            ApplyCoinPose(leftCoin, leftBody, leftDrag, leftTarget, leftRot);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            ApplyCoinPose(rightCoin, rightBody, rightDrag, rightTarget, rightRot);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (GameRulesManager.Instance != null)
        {
            EnsureOpeningCoinResolved();
            CoinIdentity centerIdentity = _openingCoin != null
                ? _openingCoin.GetComponent<CoinIdentity>()
                : null;
            GameRulesManager.Instance.PrepareForPostTutorialOpeningShot(centerIdentity);
            leftCoin?.GetComponent<CoinIdentity>()?.SetPassive(true);
            rightCoin?.GetComponent<CoinIdentity>()?.SetPassive(true);
            centerIdentity?.SetPassive(false);
        }
    }

    static Transform ResolveSideCoinTransform(string objectName, Transform cached)
    {
        if (cached != null)
        {
            return cached;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    bool TryResolveStageEightSideCoinTargetPositions(out Vector3 leftTarget, out Vector3 rightTarget)
    {
        leftTarget = default;
        rightTarget = default;

        Transform coin1 = _stage8Coin1Target;
        Transform coin2 = _stage8Coin2Target;
        if (coin1 == null || coin2 == null)
        {
            ResolveStageEightTargetMarkersIfNeeded(ref coin1, ref coin2);
        }

        if (coin1 == null || coin2 == null)
        {
            return false;
        }

        _stage8Coin1Target = coin1;
        _stage8Coin2Target = coin2;
        leftTarget = coin1.position;
        rightTarget = coin2.position;
        return true;
    }

    static void ResolveStageEightTargetMarkersIfNeeded(ref Transform coin1, ref Transform coin2)
    {
        if (coin1 != null && coin2 != null)
        {
            return;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (coin1 == null && candidate.name == "Stg8-Coin1")
            {
                coin1 = candidate;
            }
            else if (coin2 == null && candidate.name == "Stg8-Coin2")
            {
                coin2 = candidate;
            }

            if (coin1 != null && coin2 != null)
            {
                return;
            }
        }
    }

    IEnumerator StageSevenShowAlertRoutine()
    {
        _stage7PostShotSequenceActive = true;
        HideGuideVisuals();
        HideTutorialOverlay();
        GateIndicator.Instance?.Hide();
        ShowStageSevenAlert();
        _flowRoutine = null;
        yield break;
    }

    IEnumerator StageSevenAlertDismissRoutine()
    {
        HideStageSevenAlert();
        _stage7PostShotSequenceActive = true;
        yield return ReturnCenterCoinHome();
        _stage7ConsecutiveFails = 0;
        _stage7PostShotSequenceActive = false;
        EnterStage7();
        _flowRoutine = null;
    }

    void BindStageSixAlertButton()
    {
        if (_stage6Alert == null)
        {
            return;
        }

        _stage6AlertButton = _stage6Alert.GetComponentInChildren<Button>(true);
        if (_stage6AlertButton == null)
        {
            return;
        }

        _stage6AlertButton.onClick.RemoveListener(OnStageSixAlertButtonClicked);
        _stage6AlertButton.onClick.AddListener(OnStageSixAlertButtonClicked);
    }

    void UnbindStageSixAlertButton()
    {
        if (_stage6AlertButton == null)
        {
            return;
        }

        _stage6AlertButton.onClick.RemoveListener(OnStageSixAlertButtonClicked);
        _stage6AlertButton = null;
    }

    void OnStageSixAlertButtonClicked()
    {
        if (!IsStageSixAlertVisible())
        {
            return;
        }

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        _flowRoutine = StartCoroutine(StageSixAlertDismissRoutine());
    }

    void ShowStageSixAlert()
    {
        if (_stage6Alert == null)
        {
            return;
        }

        _stage6Alert.SetActive(true);
    }

    void HideStageSixAlert()
    {
        if (_stage6Alert != null)
        {
            _stage6Alert.SetActive(false);
        }
    }

    bool IsStageSixAlertVisible()
    {
        return _stage6Alert != null && _stage6Alert.activeSelf;
    }

    void BindStageSevenAlertButton()
    {
        if (_stage7Alert == null)
        {
            return;
        }

        _stage7AlertButton = _stage7Alert.GetComponentInChildren<Button>(true);
        if (_stage7AlertButton == null)
        {
            return;
        }

        _stage7AlertButton.onClick.RemoveListener(OnStageSevenAlertButtonClicked);
        _stage7AlertButton.onClick.AddListener(OnStageSevenAlertButtonClicked);
    }

    void UnbindStageSevenAlertButton()
    {
        if (_stage7AlertButton == null)
        {
            return;
        }

        _stage7AlertButton.onClick.RemoveListener(OnStageSevenAlertButtonClicked);
        _stage7AlertButton = null;
    }

    void OnStageSevenAlertButtonClicked()
    {
        if (!IsStageSevenAlertVisible())
        {
            return;
        }

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        _flowRoutine = StartCoroutine(StageSevenAlertDismissRoutine());
    }

    void ShowStageSevenAlert()
    {
        if (_stage7Alert == null)
        {
            return;
        }

        _stage7Alert.SetActive(true);
    }

    void HideStageSevenAlert()
    {
        if (_stage7Alert != null)
        {
            _stage7Alert.SetActive(false);
        }
    }

    bool IsStageSevenAlertVisible()
    {
        return _stage7Alert != null && _stage7Alert.activeSelf;
    }

    void BindStageNineAlertButton()
    {
        if (_stage9Alert == null)
        {
            return;
        }

        _stage9AlertButton = _stage9Alert.GetComponentInChildren<Button>(true);
        if (_stage9AlertButton == null)
        {
            return;
        }

        _stage9AlertButton.onClick.RemoveListener(OnStageNineAlertButtonClicked);
        _stage9AlertButton.onClick.AddListener(OnStageNineAlertButtonClicked);
    }

    void UnbindStageNineAlertButton()
    {
        if (_stage9AlertButton == null)
        {
            return;
        }

        _stage9AlertButton.onClick.RemoveListener(OnStageNineAlertButtonClicked);
        _stage9AlertButton = null;
    }

    void OnStageNineAlertButtonClicked()
    {
        if (!IsStageNineAlertVisible())
        {
            return;
        }

        HideStageNineAlert();
        _stage9AlertPending = false;

        if (_phase == GuidePhase.Stage11_PassAndGoal)
        {
            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
            }

            _flowRoutine = StartCoroutine(StageElevenAlertDismissRoutine());
            return;
        }

        if (_phase == GuidePhase.Stage9_PassBetween)
        {
            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
            }

            _flowRoutine = StartCoroutine(StageNineAlertDismissRoutine());
            return;
        }

        _stage9ConsecutiveFails = 0;
    }

    void ShowStageNineAlert()
    {
        if (_stage9Alert == null)
        {
            return;
        }

        _stage9Alert.SetActive(true);
    }

    void HideStageNineAlert()
    {
        if (_stage9Alert != null)
        {
            _stage9Alert.SetActive(false);
        }
    }

    bool IsStageNineAlertVisible()
    {
        return _stage9Alert != null && _stage9Alert.activeSelf;
    }

    void BindStageTenAlertButton()
    {
        if (_stage10Alert == null)
        {
            return;
        }

        _stage10AlertButton = _stage10Alert.GetComponentInChildren<Button>(true);
        if (_stage10AlertButton == null)
        {
            return;
        }

        _stage10AlertButton.onClick.RemoveListener(OnStageTenAlertButtonClicked);
        _stage10AlertButton.onClick.AddListener(OnStageTenAlertButtonClicked);
    }

    void UnbindStageTenAlertButton()
    {
        if (_stage10AlertButton == null)
        {
            return;
        }

        _stage10AlertButton.onClick.RemoveListener(OnStageTenAlertButtonClicked);
        _stage10AlertButton = null;
    }

    void OnStageTenAlertButtonClicked()
    {
        if (!IsStageTenAlertVisible())
        {
            return;
        }

        HideStageTenAlert();
        if (_phase != GuidePhase.Stage11_PassAndGoal)
        {
            return;
        }

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        _flowRoutine = StartCoroutine(StageElevenAlertDismissRoutine());
    }

    void ShowStageTenAlert()
    {
        if (_stage10Alert == null)
        {
            return;
        }

        _stage10Alert.SetActive(true);
    }

    void HideStageTenAlert()
    {
        if (_stage10Alert != null)
        {
            _stage10Alert.SetActive(false);
        }
    }

    bool IsStageTenAlertVisible()
    {
        return _stage10Alert != null && _stage10Alert.activeSelf;
    }

    IEnumerator ShowSuccessBriefly()
    {
        ShowSuccessPanel();
        float duration = _successDisplayDuration > 0.01f ? _successDisplayDuration : 1f;
        yield return new WaitForSecondsRealtime(duration);
        HideSuccessPanel();
    }

    void ShowSuccessPanel()
    {
        EnsureSuccessPanelResolved();
        if (_successPanel != null)
        {
            _successPanel.SetActive(true);
        }

        PlaySuccessSound();
    }

    void PlaySuccessSound()
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        if (_successSound == null)
        {
            return;
        }

        EnsureSuccessAudioSource();
        if (_successAudioSource == null)
        {
            return;
        }

        _successAudioSource.PlayOneShot(_successSound);
    }

    void EnsureSuccessAudioSource()
    {
        if (_successAudioSource != null)
        {
            return;
        }

        _successAudioSource = gameObject.GetComponent<AudioSource>();
        if (_successAudioSource == null)
        {
            _successAudioSource = gameObject.AddComponent<AudioSource>();
        }

        _successAudioSource.playOnAwake = false;
        _successAudioSource.spatialBlend = 0f;
    }

    void HideSuccessPanel()
    {
        EnsureSuccessPanelResolved();
        if (_successPanel != null)
        {
            _successPanel.SetActive(false);
        }
    }

    bool IsSuccessPanelVisible()
    {
        return _successPanel != null && _successPanel.activeSelf;
    }

    void EnsureSuccessPanelResolved()
    {
        if (_successPanel != null || _canvasRect == null)
        {
            return;
        }

        Transform success = _canvasRect.Find("Success");
        if (success != null)
        {
            _successPanel = success.gameObject;
        }
    }

    void SubscribeGameRulesEvents()
    {
        StartCoroutine(SubscribeGameRulesEventsWhenReady());
    }

    IEnumerator SubscribeGameRulesEventsWhenReady()
    {
        while (GameRulesManager.Instance == null)
        {
            yield return null;
        }

        UnsubscribeGameRulesEvents();
        GameRulesManager.Instance.InvalidMoveRollbackFinished += OnInvalidMoveRollbackFinished;
        GameRulesManager.Instance.ValidShotCommitted += OnValidShotCommitted;
        GameRulesManager.Instance.PlayerShotResolved += OnPlayerShotResolved;
    }

    void UnsubscribeGameRulesEvents()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.InvalidMoveRollbackFinished -= OnInvalidMoveRollbackFinished;
        GameRulesManager.Instance.ValidShotCommitted -= OnValidShotCommitted;
        GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
    }

    void OnValidShotCommitted(CoinTeam team)
    {
        if (team != CoinTeam.Player)
        {
            return;
        }

        if (_phase == GuidePhase.Stage9_PassBetween)
        {
            _stage9ConsecutiveFails = 0;
            BeginStageNineSuccessToStageTen();
            return;
        }

        if (_phase == GuidePhase.Stage11_PassAndGoal)
        {
            HandleStageElevenGatePassNoGoal();
        }
    }

    void HandleStageElevenGatePassNoGoal()
    {
        if (_stage11PostShotSequenceActive || IsStageTenAlertVisible())
        {
            return;
        }

        _stage11ConsecutiveFails = 0;
        _stage11NoGoalFails++;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        if (_stage11NoGoalFails >= 2)
        {
            ShowStageTenAlert();
            return;
        }

        _flowRoutine = StartCoroutine(StageElevenNoGoalResetRoutine());
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool shotValid)
    {
        _ = coin;
        if (_phase != GuidePhase.Stage9_PassBetween || !shotValid)
        {
            return;
        }

        _stage9ConsecutiveFails = 0;
        BeginStageNineSuccessToStageTen();
    }

    void BeginStageNineSuccessToStageTen()
    {
        if (_stage9PostShotSequenceActive || _phase != GuidePhase.Stage9_PassBetween)
        {
            return;
        }

        _stage9PostShotSequenceActive = true;
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

        _flowRoutine = StartCoroutine(StageNineSuccessThenStage10Routine());
    }

    IEnumerator StageNineSuccessThenStage10Routine()
    {
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageNineAlert();
        yield return ShowSuccessBriefly();
        yield return AnimateCoinsToStageTenPositions();
        _stage9PostShotSequenceActive = false;
        EnterStage10(resetRelatedFailCounts: true);
        _flowRoutine = null;
    }

    IEnumerator AnimateCoinsToStageTenPositions()
    {
        EnsureOpeningCoinResolved();
        Transform centerCoin = _openingCoin;
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);

        if (!TryResolveStageTenTargetPositions(out Vector3 leftTarget, out Vector3 rightTarget, out Vector3 hitTarget))
        {
            Debug.LogWarning("Onboarding: Stg10-Coin1 / Stg10-Coin2 / Stg10-CoinHit hedefi eksik; Aşama 10 coin animasyonu atlandı.");
            yield break;
        }

        if (leftCoin != null)
        {
            leftCoin.gameObject.SetActive(true);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            rightCoin.gameObject.SetActive(true);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        centerCoin?.GetComponent<CoinIdentity>()?.SetPassive(false);

        Vector3 leftStart = leftCoin != null ? leftCoin.position : default;
        Quaternion leftRot = leftCoin != null ? leftCoin.rotation : Quaternion.identity;
        Vector3 rightStart = rightCoin != null ? rightCoin.position : default;
        Quaternion rightRot = rightCoin != null ? rightCoin.rotation : Quaternion.identity;
        Vector3 centerStart = centerCoin != null ? centerCoin.position : default;
        Quaternion centerRot = centerCoin != null ? centerCoin.rotation : Quaternion.identity;

        Rigidbody leftBody = leftCoin != null ? leftCoin.GetComponent<Rigidbody>() : null;
        Rigidbody rightBody = rightCoin != null ? rightCoin.GetComponent<Rigidbody>() : null;
        Rigidbody centerBody = centerCoin != null ? centerCoin.GetComponent<Rigidbody>() : null;
        CoinDragController leftDrag = leftCoin != null ? leftCoin.GetComponent<CoinDragController>() : null;
        CoinDragController rightDrag = rightCoin != null ? rightCoin.GetComponent<CoinDragController>() : null;
        CoinDragController centerDrag = centerCoin != null ? centerCoin.GetComponent<CoinDragController>() : null;

        leftDrag?.CancelAim();
        rightDrag?.CancelAim();
        centerDrag?.CancelAim();

        if (leftBody != null)
        {
            leftBody.linearVelocity = Vector3.zero;
            leftBody.angularVelocity = Vector3.zero;
            leftBody.isKinematic = true;
        }

        if (rightBody != null)
        {
            rightBody.linearVelocity = Vector3.zero;
            rightBody.angularVelocity = Vector3.zero;
            rightBody.isKinematic = true;
        }

        if (centerBody != null)
        {
            centerBody.linearVelocity = Vector3.zero;
            centerBody.angularVelocity = Vector3.zero;
            centerBody.isKinematic = true;
        }

        float duration = _stage10CoinMoveDuration > 0.01f ? _stage10CoinMoveDuration : 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (leftCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(leftStart, leftTarget, t);
                leftCoin.SetPositionAndRotation(nextPos, leftRot);
                if (leftBody != null)
                {
                    leftBody.position = nextPos;
                    leftBody.rotation = leftRot;
                }
            }

            if (rightCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(rightStart, rightTarget, t);
                rightCoin.SetPositionAndRotation(nextPos, rightRot);
                if (rightBody != null)
                {
                    rightBody.position = nextPos;
                    rightBody.rotation = rightRot;
                }
            }

            if (centerCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(centerStart, hitTarget, t);
                centerCoin.SetPositionAndRotation(nextPos, centerRot);
                if (centerBody != null)
                {
                    centerBody.position = nextPos;
                    centerBody.rotation = centerRot;
                }
            }

            yield return null;
        }

        if (leftCoin != null)
        {
            ApplyCoinPose(leftCoin, leftBody, leftDrag, leftTarget, leftRot);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            ApplyCoinPose(rightCoin, rightBody, rightDrag, rightTarget, rightRot);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (centerCoin != null)
        {
            ApplyCoinPose(centerCoin, centerBody, centerDrag, hitTarget, centerRot);
            centerCoin.GetComponent<CoinIdentity>()?.SetPassive(false);
            centerDrag?.UnfreezeAfterGoal();
        }
    }

    bool TryResolveStageTenTargetPositions(out Vector3 leftTarget, out Vector3 rightTarget, out Vector3 hitTarget)
    {
        leftTarget = default;
        rightTarget = default;
        hitTarget = default;

        Transform coin1 = _stage10Coin1Target;
        Transform coin2 = _stage10Coin2Target;
        Transform coinHit = _stage10CoinHitTarget;
        if (coin1 == null || coin2 == null || coinHit == null)
        {
            ResolveStageTenTargetMarkersIfNeeded(ref coin1, ref coin2, ref coinHit);
        }

        if (coin1 == null || coin2 == null || coinHit == null)
        {
            return false;
        }

        _stage10Coin1Target = coin1;
        _stage10Coin2Target = coin2;
        _stage10CoinHitTarget = coinHit;
        leftTarget = coin1.position;
        rightTarget = coin2.position;
        hitTarget = coinHit.position;
        return true;
    }

    static void ResolveStageTenTargetMarkersIfNeeded(ref Transform coin1, ref Transform coin2, ref Transform coinHit)
    {
        if (coin1 != null && coin2 != null && coinHit != null)
        {
            return;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (coin1 == null && candidate.name == "Stg10-Coin1")
            {
                coin1 = candidate;
            }
            else if (coin2 == null && candidate.name == "Stg10-Coin2")
            {
                coin2 = candidate;
            }
            else if (coinHit == null && candidate.name == "Stg10-CoinHit")
            {
                coinHit = candidate;
            }

            if (coin1 != null && coin2 != null && coinHit != null)
            {
                return;
            }
        }
    }

    void OnInvalidMoveRollbackFinished(CoinTeam team)
    {
        if (team != CoinTeam.Player)
        {
            return;
        }

        if (_phase == GuidePhase.Stage9_PassBetween)
        {
            _stage9ConsecutiveFails++;
            GateIndicator.Instance?.Hide();
            HideGuideVisuals();

            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
            }

            _flowRoutine = StartCoroutine(StageNineInvalidMoveResetRoutine());
            return;
        }

        if (_phase != GuidePhase.Stage11_PassAndGoal)
        {
            return;
        }

        _stage11NoGoalFails = 0;
        _stage11ConsecutiveFails++;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();

        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
        }

            _flowRoutine = StartCoroutine(StageElevenInvalidMoveResetRoutine());
    }

    IEnumerator StageNineInvalidMoveResetRoutine()
    {
        _stage9PostShotSequenceActive = true;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageNineAlert();
        yield return RestoreStageEightCoinPositions();
        _stage9PostShotSequenceActive = false;

        if (_stage9ConsecutiveFails >= 2)
        {
            _stage9AlertPending = true;
            ShowStageNineAlert();
            _stage9AlertPending = false;
        }
        else
        {
            // Atış denemesi bitti; el + explanation için Aşama 8 sunumuna dön.
            EnterStage8();
        }

        _flowRoutine = null;
    }

    IEnumerator StageNineAlertDismissRoutine()
    {
        _stage9PostShotSequenceActive = true;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageNineAlert();
        yield return RestoreStageEightCoinPositions();
        _stage9ConsecutiveFails = 0;
        _stage9PostShotSequenceActive = false;
        EnterStage8();
        _flowRoutine = null;
    }

    IEnumerator StageElevenInvalidMoveResetRoutine()
    {
        _stage11PostShotSequenceActive = true;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageNineAlert();
        HideStageTenAlert();
        yield return RestoreStageElevenCoinPositions();
        _stage11PostShotSequenceActive = false;

        if (_stage11ConsecutiveFails >= 2)
        {
            _stage9AlertPending = true;
            ShowStageNineAlert();
            _stage9AlertPending = false;
        }
        else
        {
            EnterStage10();
        }

        _flowRoutine = null;
    }

    IEnumerator StageElevenNoGoalResetRoutine()
    {
        _stage11PostShotSequenceActive = true;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageTenAlert();
        yield return RestoreStageElevenCoinPositions();
        _stage11PostShotSequenceActive = false;
        EnterStage10();
        _flowRoutine = null;
    }

    IEnumerator StageElevenAlertDismissRoutine()
    {
        _stage11PostShotSequenceActive = true;
        GateIndicator.Instance?.Hide();
        HideGuideVisuals();
        HideStageNineAlert();
        HideStageTenAlert();
        yield return RestoreStageElevenCoinPositions();
        _stage11ConsecutiveFails = 0;
        _stage11NoGoalFails = 0;
        _stage11PostShotSequenceActive = false;
        EnterStage10();
        _flowRoutine = null;
    }

    IEnumerator RestoreStageElevenCoinPositions()
    {
        if (!_hasStage11StartPoses)
        {
            CacheStageElevenCoinPositions();
        }

        EnsureOpeningCoinResolved();
        Transform centerCoin = _openingCoin;
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);

        Rigidbody centerBody = centerCoin != null ? centerCoin.GetComponent<Rigidbody>() : null;
        Rigidbody leftBody = leftCoin != null ? leftCoin.GetComponent<Rigidbody>() : null;
        Rigidbody rightBody = rightCoin != null ? rightCoin.GetComponent<Rigidbody>() : null;
        CoinDragController centerDrag = centerCoin != null ? centerCoin.GetComponent<CoinDragController>() : null;
        CoinDragController leftDrag = leftCoin != null ? leftCoin.GetComponent<CoinDragController>() : null;
        CoinDragController rightDrag = rightCoin != null ? rightCoin.GetComponent<CoinDragController>() : null;

        centerDrag?.CancelAim();
        leftDrag?.CancelAim();
        rightDrag?.CancelAim();

        Vector3 centerStart = centerCoin != null ? centerCoin.position : default;
        Quaternion centerRotStart = centerCoin != null ? centerCoin.rotation : Quaternion.identity;
        Vector3 leftStart = leftCoin != null ? leftCoin.position : default;
        Quaternion leftRotStart = leftCoin != null ? leftCoin.rotation : Quaternion.identity;
        Vector3 rightStart = rightCoin != null ? rightCoin.position : default;
        Quaternion rightRotStart = rightCoin != null ? rightCoin.rotation : Quaternion.identity;

        void PrepBody(Rigidbody body)
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        PrepBody(centerBody);
        PrepBody(leftBody);
        PrepBody(rightBody);

        float duration = _stage11CoinResetDuration > 0.01f ? _stage11CoinResetDuration : 0.75f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (centerCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(centerStart, _stage11CenterStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(centerRotStart, _stage11CenterStartRotation, t);
                centerCoin.SetPositionAndRotation(nextPos, nextRot);
                if (centerBody != null)
                {
                    centerBody.position = nextPos;
                    centerBody.rotation = nextRot;
                }
            }

            if (leftCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(leftStart, _stage11LeftStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(leftRotStart, _stage11LeftStartRotation, t);
                leftCoin.SetPositionAndRotation(nextPos, nextRot);
                if (leftBody != null)
                {
                    leftBody.position = nextPos;
                    leftBody.rotation = nextRot;
                }
            }

            if (rightCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(rightStart, _stage11RightStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(rightRotStart, _stage11RightStartRotation, t);
                rightCoin.SetPositionAndRotation(nextPos, nextRot);
                if (rightBody != null)
                {
                    rightBody.position = nextPos;
                    rightBody.rotation = nextRot;
                }
            }

            yield return null;
        }

        if (centerCoin != null)
        {
            ApplyCoinPose(centerCoin, centerBody, centerDrag, _stage11CenterStartPosition, _stage11CenterStartRotation);
            centerCoin.GetComponent<CoinIdentity>()?.SetPassive(false);
            centerDrag?.UnfreezeAfterGoal();
        }

        if (leftCoin != null)
        {
            ApplyCoinPose(leftCoin, leftBody, leftDrag, _stage11LeftStartPosition, _stage11LeftStartRotation);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            ApplyCoinPose(rightCoin, rightBody, rightDrag, _stage11RightStartPosition, _stage11RightStartRotation);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }
    }

    void CacheStageElevenCoinPositions()
    {
        EnsureOpeningCoinResolved();
        Transform centerCoin = _openingCoin;
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);

        if (centerCoin != null)
        {
            _stage11CenterStartPosition = centerCoin.position;
            _stage11CenterStartRotation = centerCoin.rotation;
        }

        if (leftCoin != null)
        {
            _stage11LeftStartPosition = leftCoin.position;
            _stage11LeftStartRotation = leftCoin.rotation;
        }

        if (rightCoin != null)
        {
            _stage11RightStartPosition = rightCoin.position;
            _stage11RightStartRotation = rightCoin.rotation;
        }

        _hasStage11StartPoses = centerCoin != null;
    }

    void CacheStageEightCoinPositions()
    {
        EnsureOpeningCoinResolved();
        Transform centerCoin = _openingCoin;
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);

        if (centerCoin != null)
        {
            _stage8CenterStartPosition = centerCoin.position;
            _stage8CenterStartRotation = centerCoin.rotation;
        }

        if (leftCoin != null)
        {
            _stage8LeftStartPosition = leftCoin.position;
            _stage8LeftStartRotation = leftCoin.rotation;
        }

        if (rightCoin != null)
        {
            _stage8RightStartPosition = rightCoin.position;
            _stage8RightStartRotation = rightCoin.rotation;
        }

        _hasStage8StartPoses = centerCoin != null;
    }

    IEnumerator RestoreStageEightCoinPositions()
    {
        if (!_hasStage8StartPoses)
        {
            CacheStageEightCoinPositions();
        }

        EnsureOpeningCoinResolved();
        Transform centerCoin = _openingCoin;
        Transform leftCoin = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform rightCoin = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);

        Rigidbody centerBody = centerCoin != null ? centerCoin.GetComponent<Rigidbody>() : null;
        Rigidbody leftBody = leftCoin != null ? leftCoin.GetComponent<Rigidbody>() : null;
        Rigidbody rightBody = rightCoin != null ? rightCoin.GetComponent<Rigidbody>() : null;
        CoinDragController centerDrag = centerCoin != null ? centerCoin.GetComponent<CoinDragController>() : null;
        CoinDragController leftDrag = leftCoin != null ? leftCoin.GetComponent<CoinDragController>() : null;
        CoinDragController rightDrag = rightCoin != null ? rightCoin.GetComponent<CoinDragController>() : null;

        centerDrag?.CancelAim();
        leftDrag?.CancelAim();
        rightDrag?.CancelAim();

        Vector3 centerStart = centerCoin != null ? centerCoin.position : default;
        Quaternion centerRotStart = centerCoin != null ? centerCoin.rotation : Quaternion.identity;
        Vector3 leftStart = leftCoin != null ? leftCoin.position : default;
        Quaternion leftRotStart = leftCoin != null ? leftCoin.rotation : Quaternion.identity;
        Vector3 rightStart = rightCoin != null ? rightCoin.position : default;
        Quaternion rightRotStart = rightCoin != null ? rightCoin.rotation : Quaternion.identity;

        void PrepBody(Rigidbody body)
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        PrepBody(centerBody);
        PrepBody(leftBody);
        PrepBody(rightBody);

        float duration = _stage9CoinResetDuration > 0.01f ? _stage9CoinResetDuration : 0.75f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (centerCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(centerStart, _stage8CenterStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(centerRotStart, _stage8CenterStartRotation, t);
                centerCoin.SetPositionAndRotation(nextPos, nextRot);
                if (centerBody != null)
                {
                    centerBody.position = nextPos;
                    centerBody.rotation = nextRot;
                }
            }

            if (leftCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(leftStart, _stage8LeftStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(leftRotStart, _stage8LeftStartRotation, t);
                leftCoin.SetPositionAndRotation(nextPos, nextRot);
                if (leftBody != null)
                {
                    leftBody.position = nextPos;
                    leftBody.rotation = nextRot;
                }
            }

            if (rightCoin != null)
            {
                Vector3 nextPos = Vector3.Lerp(rightStart, _stage8RightStartPosition, t);
                Quaternion nextRot = Quaternion.Slerp(rightRotStart, _stage8RightStartRotation, t);
                rightCoin.SetPositionAndRotation(nextPos, nextRot);
                if (rightBody != null)
                {
                    rightBody.position = nextPos;
                    rightBody.rotation = nextRot;
                }
            }

            yield return null;
        }

        if (centerCoin != null)
        {
            ApplyCoinPose(centerCoin, centerBody, centerDrag, _stage8CenterStartPosition, _stage8CenterStartRotation);
            centerCoin.GetComponent<CoinIdentity>()?.SetPassive(false);
            centerDrag?.UnfreezeAfterGoal();
        }

        if (leftCoin != null)
        {
            ApplyCoinPose(leftCoin, leftBody, leftDrag, _stage8LeftStartPosition, _stage8LeftStartRotation);
            leftCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }

        if (rightCoin != null)
        {
            ApplyCoinPose(rightCoin, rightBody, rightDrag, _stage8RightStartPosition, _stage8RightStartRotation);
            rightCoin.GetComponent<CoinIdentity>()?.SetPassive(true);
        }
    }

    void BeginWaitingForCoinStop()
    {
        _waitingForCoinStop = true;
        _coinStopAdvanceTriggered = false;
        HideGuideVisuals();
    }

    void EnterStage2(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage2_ReleaseToShot);
    }

    void EnterReleaseToShotStage(CoinDragController dragController, GuidePhase phase)
    {
        if (phase == GuidePhase.Stage2_ReleaseToShot)
        {
            TryUpdateStageTwoAimLock(dragController);
        }
        else if (phase == GuidePhase.Stage4_AlignShot)
        {
            TryUpdateStageFourAimLock(dragController);
        }

        SetPhase(phase);
        _tutorialOverlay.HideAngleGuides();
        LockReleaseToShotAim(dragController, phase);

        if (TryGetFixedPowerAimLineEnd(dragController, out Vector3 aimEnd))
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
                FixedTutorialAimPower01);
            SetPullGuideAnchors(aimEnd);
        }

        HideCoinGuideVisuals();
        ShowPullGuideVisuals();
        SetActiveGuideText(phase == GuidePhase.Stage4_AlignShot
            ? _stage4ReleaseMessage
            : _stage2ReleaseMessage);
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
        SetActiveGuideText(_stage3DragMessage);
    }

    void EnterStage4(CoinDragController dragController)
    {
        EnterReleaseToShotStage(dragController, GuidePhase.Stage4_AlignShot);
    }

    void EnterStage5()
    {
        SetPhase(GuidePhase.Stage5_Drag);
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        EnsureStageSixGateVisible();
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_stage5DragMessage);
        SetExplanationBackground(_positiveExplanationColor);
        UpdateActiveGuideElementPosition();
    }

    void EnterStage7()
    {
        SetPhase(GuidePhase.Stage7_Drag);
        EnsureStageSevenGateVisible();
        ShowStageSevenGuideLines();
        UpdateStageSevenWedgeSpotlight();
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetGuideExplanationVisible(false);
        UpdateActiveGuideElementPosition();
        UpdateStageSevenWedgeSpotlight();
    }

    void EnterStage8(bool resetRelatedFailCounts = false)
    {
        SetPhase(GuidePhase.Stage8_Drag);
        HideTutorialOverlay();
        HideStageSevenAlert();

        if (resetRelatedFailCounts)
        {
            _stage9ConsecutiveFails = 0;
        }

        EnsureOpeningCoinResolved();
        CoinIdentity centerIdentity = _openingCoin != null
            ? _openingCoin.GetComponent<CoinIdentity>()
            : null;
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PrepareForPostTutorialOpeningShot(centerIdentity);
        }

        OnboardingSceneBootstrap.LeftCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        OnboardingSceneBootstrap.RightCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        centerIdentity?.SetPassive(false);

        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetGuideExplanationVisible(true);
        SetActiveGuideText(_stage8DragMessage);
        SetExplanationBackground(_positiveExplanationColor);
        UpdateActiveGuideElementPosition();
        CacheStageEightCoinPositions();
        EnsureStageEightGateVisible();
    }

    void EnsureOpeningCoinResolved()
    {
        if (_openingCoin != null)
        {
            return;
        }

        _openingCoin = OnboardingSceneBootstrap.CenterCoinTransform;
        if (_openingCoin != null)
        {
            return;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null
                && candidate.name == "Coin_P2"
                && candidate.gameObject.scene.IsValid())
            {
                _openingCoin = candidate;
                return;
            }
        }
    }

    void EnterStage9(CoinDragController aimingCoin = null, bool resetFailCounts = false)
    {
        if (_phase == GuidePhase.Stage9_PassBetween
            && aimingCoin != null
            && aimingCoin.IsAiming)
        {
            EnsureStageNineGateVisible(aimingCoin);
            return;
        }

        if (aimingCoin != null)
        {
            _openingCoin = aimingCoin.transform;
        }
        else if (_openingCoin == null)
        {
            _openingCoin = OnboardingSceneBootstrap.CenterCoinTransform;
            if (_openingCoin == null)
            {
                GameObject openingCoinObject = GameObject.Find("Coin_P2");
                if (openingCoinObject != null)
                {
                    _openingCoin = openingCoinObject.transform;
                }
            }
        }

        SetPhase(GuidePhase.Stage9_PassBetween);
        if (resetFailCounts)
        {
            _stage9ConsecutiveFails = 0;
        }

        HideTutorialOverlay();
        HideGuideVisuals();
        HideStageNineAlert();
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        if (!_hasStage8StartPoses)
        {
            CacheStageEightCoinPositions();
        }

        CoinIdentity centerIdentity = _openingCoin != null
            ? _openingCoin.GetComponent<CoinIdentity>()
            : aimingCoin != null ? aimingCoin.GetComponent<CoinIdentity>() : null;

        if (GameRulesManager.Instance != null)
        {
            if (centerIdentity != null)
            {
                GameRulesManager.Instance.PrepareForGuidedCoinShot(centerIdentity);
            }
            else
            {
                // Gate doğrulaması için açılış atışı muafiyetini kaldır.
                GameRulesManager.Instance.PrepareForGuidedCoinShot(
                    aimingCoin != null ? aimingCoin.GetComponent<CoinIdentity>() : null);
            }
        }

        OnboardingSceneBootstrap.LeftCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        OnboardingSceneBootstrap.RightCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        centerIdentity?.SetPassive(false);

        EnsureStageNineGateVisible(aimingCoin);
    }

    void EnterStage10(bool resetRelatedFailCounts = false)
    {
        SetPhase(GuidePhase.Stage10_PullAndGoal);
        GateIndicator.Instance?.Hide();
        HideTutorialOverlay();
        HideStageNineAlert();
        HideStageTenAlert();

        if (resetRelatedFailCounts)
        {
            _stage11ConsecutiveFails = 0;
            _stage11NoGoalFails = 0;
        }

        EnsureOpeningCoinResolved();
        CoinIdentity centerIdentity = _openingCoin != null
            ? _openingCoin.GetComponent<CoinIdentity>()
            : null;

        // Aşama 10: gate/InvalidMove kapalı; yalnızca P2 oynanır.
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PrepareForPostTutorialOpeningShot(centerIdentity);
        }

        OnboardingSceneBootstrap.LeftCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        OnboardingSceneBootstrap.RightCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        centerIdentity?.SetPassive(false);

        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetGuideExplanationVisible(true);
        SetActiveGuideText(_stage10DragMessage);
        SetExplanationBackground(_positiveExplanationColor);
        UpdateActiveGuideElementPosition();
        CacheStageElevenCoinPositions();
    }

    void EnterStage11(CoinDragController aimingCoin = null, bool resetFailCounts = false)
    {
        // Aim sırasında tekrarlı çağrı: sadece gate'i tazele.
        if (_phase == GuidePhase.Stage11_PassAndGoal
            && aimingCoin != null
            && aimingCoin.IsAiming)
        {
            EnsureStageElevenGateVisible(aimingCoin);
            return;
        }

        if (aimingCoin != null)
        {
            _openingCoin = aimingCoin.transform;
        }

        EnsureOpeningCoinResolved();
        SetPhase(GuidePhase.Stage11_PassAndGoal);
        if (resetFailCounts)
        {
            _stage11ConsecutiveFails = 0;
            _stage11NoGoalFails = 0;
        }

        HideTutorialOverlay();
        HideGuideVisuals();
        HideStageNineAlert();
        HideStageTenAlert();
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        if (!_hasStage11StartPoses)
        {
            CacheStageElevenCoinPositions();
        }

        CoinIdentity centerIdentity = _openingCoin != null
            ? _openingCoin.GetComponent<CoinIdentity>()
            : aimingCoin != null ? aimingCoin.GetComponent<CoinIdentity>() : null;

        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PrepareForGuidedCoinShot(centerIdentity);
        }

        OnboardingSceneBootstrap.LeftCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        OnboardingSceneBootstrap.RightCoinTransform?.GetComponent<CoinIdentity>()?.SetPassive(true);
        centerIdentity?.SetPassive(false);

        EnsureStageElevenGateVisible(aimingCoin);
    }

    void EnsureStageElevenGateVisible(CoinDragController aimingCoin = null)
    {
        if (_phase != GuidePhase.Stage11_PassAndGoal)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        if (indicator == null)
        {
            return;
        }

        if (indicator.IsVisible)
        {
            return;
        }

        Component settingsSource = aimingCoin != null
            ? aimingCoin
            : (Component)_openingCoin;
        CoinGateIndicatorSettings settings = settingsSource != null
            ? CoinGateIndicatorSettings.Resolve(settingsSource)
            : null;

        GameRulesManager rules = GameRulesManager.Instance;
        CoinIdentity shooter = aimingCoin != null
            ? aimingCoin.GetComponent<CoinIdentity>()
            : _openingCoin != null ? _openingCoin.GetComponent<CoinIdentity>() : null;

        if (rules != null
            && shooter != null
            && rules.TryGetGateCoins(shooter, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            indicator.Show(gateA, gateB, settings, animate: true);
            return;
        }

        Transform left = OnboardingSceneBootstrap.LeftCoinTransform;
        Transform right = OnboardingSceneBootstrap.RightCoinTransform;
        if (left == null || right == null)
        {
            return;
        }

        indicator.ShowWorldGate(left.position, right.position, settings, animate: true);
    }

    void EnsureStageEightGateVisible()
    {
        if (_phase != GuidePhase.Stage8_Drag)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        if (indicator == null)
        {
            return;
        }

        if (indicator.IsVisible)
        {
            return;
        }

        CoinGateIndicatorSettings settings = _openingCoin != null
            ? CoinGateIndicatorSettings.Resolve(_openingCoin)
            : null;

        GameRulesManager rules = GameRulesManager.Instance;
        CoinIdentity shooter = _openingCoin != null
            ? _openingCoin.GetComponent<CoinIdentity>()
            : null;

        if (rules != null
            && shooter != null
            && rules.TryGetGateCoins(shooter, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            indicator.Show(gateA, gateB, settings, animate: true);
            return;
        }

        Transform left = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform right = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);
        if (left == null || right == null)
        {
            return;
        }

        indicator.ShowWorldGate(left.position, right.position, settings, animate: true);
    }

    void EnsureStageNineGateVisible(CoinDragController aimingCoin = null)
    {
        if (_phase != GuidePhase.Stage9_PassBetween)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        if (indicator == null)
        {
            return;
        }

        if (indicator.IsVisible)
        {
            return;
        }

        Component settingsSource = aimingCoin != null
            ? aimingCoin
            : (Component)_openingCoin;
        CoinGateIndicatorSettings settings = settingsSource != null
            ? CoinGateIndicatorSettings.Resolve(settingsSource)
            : null;

        GameRulesManager rules = GameRulesManager.Instance;
        CoinIdentity shooter = aimingCoin != null
            ? aimingCoin.GetComponent<CoinIdentity>()
            : _openingCoin != null ? _openingCoin.GetComponent<CoinIdentity>() : null;

        if (rules != null
            && shooter != null
            && rules.TryGetGateCoins(shooter, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            indicator.Show(gateA, gateB, settings, animate: true);
            return;
        }

        // Fallback: yan coin world pozisyonları (TryGetGateCoins henüz 3 coin görmüyorsa).
        Transform left = OnboardingSceneBootstrap.LeftCoinTransform;
        Transform right = OnboardingSceneBootstrap.RightCoinTransform;
        if (left == null || right == null)
        {
            return;
        }

        indicator.ShowWorldGate(left.position, right.position, settings, animate: true);
    }

    void EnterStage6()
    {
        SetPhase(GuidePhase.Stage6_PowerAim);
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        CoinDragController dragController = GetCenterCoinDragController();
        if (dragController != null && dragController.IsAiming)
        {
            UpdateStageSixPowerPresentation(dragController);
            return;
        }

        GateIndicator.Instance?.Hide();
        SetCoinGuideAnchors();
        ShowCoinGuideVisuals();
        SetActiveGuideText(_stage6DragMessage);
        SetExplanationBackground(_positiveExplanationColor);
        UpdateActiveGuideElementPosition();
    }

    void EnsureStageSixGateVisible()
    {
        if (_phase != GuidePhase.Stage5_Drag && _phase != GuidePhase.Stage6_PowerAim)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        // ShowWorldGate offset'leri sıfırlar; her karede çağrılırsa dash animasyonu donar.
        if (indicator == null)
        {
            return;
        }

        if (indicator.IsVisible)
        {
            indicator.SetAnimationSpeedScale(1f);
            return;
        }

        if (!TryResolveStageSixGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return;
        }

        CoinGateIndicatorSettings settings = _openingCoin != null
            ? CoinGateIndicatorSettings.Resolve(_openingCoin)
            : null;
        indicator.ShowWorldGate(gateStart, gateEnd, settings, animate: true);
        indicator.SetAnimationSpeedScale(1f);
    }

    void EnsureStageSevenGateVisible()
    {
        if (_phase != GuidePhase.Stage7_Drag)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        if (indicator == null)
        {
            return;
        }

        float speedScale = _stage7GateAnimationSpeedScale > 0.01f
            ? _stage7GateAnimationSpeedScale
            : 0.5f;

        if (indicator.IsVisible)
        {
            indicator.SetAnimationSpeedScale(speedScale);
            return;
        }

        if (!TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return;
        }

        CoinGateIndicatorSettings settings = _openingCoin != null
            ? CoinGateIndicatorSettings.Resolve(_openingCoin)
            : null;
        indicator.ShowWorldGate(gateStart, gateEnd, settings, animate: true);
        indicator.SetAnimationSpeedScale(speedScale);
    }

    void ShowStageSevenGuideLines()
    {
        if (_phase != GuidePhase.Stage7_Drag)
        {
            return;
        }

        if (!TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return;
        }

        EnsureTutorialOverlay();
        Vector3 coinPosition = GetGuidedCoinWorldPosition();
        _tutorialOverlay.ShowGuideLinesToEndpoints(
            coinPosition,
            gateStart,
            gateEnd,
            _stage7GuideLineColor);
    }

    bool TryResolveStageSixGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd)
    {
        gateStart = default;
        gateEnd = default;
        if (_stage6GateCoin1 == null || _stage6GateCoin2 == null)
        {
            return false;
        }

        gateStart = _stage6GateCoin1.position;
        gateEnd = _stage6GateCoin2.position;
        return true;
    }

    bool TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd)
    {
        gateStart = default;
        gateEnd = default;
        if (_stage7GateCoin1 == null || _stage7GateCoin2 == null)
        {
            return false;
        }

        gateStart = _stage7GateCoin1.position;
        gateEnd = _stage7GateCoin2.position;
        return true;
    }

    bool TryGetStageSevenGateMidpoint(out Vector3 midpoint)
    {
        midpoint = default;
        if (!TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
        {
            return false;
        }

        midpoint = (gateStart + gateEnd) * 0.5f;
        return true;
    }

    void TryUpdateStageSixAimLock(CoinDragController dragController)
    {
        if (dragController == null || !dragController.IsAiming)
        {
            return;
        }

        Vector3 coinPosition = dragController.transform.position;
        Vector3 direction = GetStageSixLaunchDirection(coinPosition);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Yön kilitli, güç serbest (_isAimPullLocked = false).
        dragController.LockAimDirection(direction);
    }

    Vector3 GetStageSixLaunchDirection(Vector3 coinPosition)
    {
        Vector3 toGoal = GetEnemyGoalCenter() - coinPosition;
        toGoal.y = 0f;
        return toGoal.sqrMagnitude < 0.0001f ? Vector3.forward : toGoal.normalized;
    }

    IEnumerator ReturnCenterCoinHomeThenComplete()
    {
        HideGuideVisuals();
        yield return ShowSuccessBriefly();
        yield return ReturnCenterCoinHome();
        _stageFourPostShotSequenceActive = false;
        EnterStage5();
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
            || _phase == GuidePhase.Stage7_Drag
            || _phase == GuidePhase.Stage8_Drag
            || _phase == GuidePhase.Stage10_PullAndGoal)
        {
            SetCoinGuideAnchors();
            if (_guideElement != null && _guideElement.gameObject.activeSelf)
            {
                EnsureArrowAnimationRunning();
            }
        }
        else if (_phase == GuidePhase.Stage2_ReleaseToShot
                 || _phase == GuidePhase.Stage4_AlignShot)
        {
            if (_pullGuideElement != null && _pullGuideElement.gameObject.activeSelf)
            {
                EnsureArrowAnimationRunning();
            }
        }
        else if (_phase == GuidePhase.Stage6_PowerAim)
        {
            CoinDragController stageSixDrag = GetCenterCoinDragController();
            if (stageSixDrag != null && stageSixDrag.IsAiming)
            {
                // Aim sırasında explanation Aim ucunda; LateUpdate günceller.
            }
            else
            {
                SetCoinGuideAnchors();
                if (_guideElement != null && _guideElement.gameObject.activeSelf)
                {
                    EnsureArrowAnimationRunning();
                }
            }
        }

        if ((_phase == GuidePhase.Stage1_Drag
                || _phase == GuidePhase.Stage5_Drag
                || _phase == GuidePhase.Stage7_Drag
                || _phase == GuidePhase.Stage8_Drag
                || _phase == GuidePhase.Stage10_PullAndGoal)
            && IsCenterCoinAimLineVisible())
        {
            HideCoinGuideElementOnly();
        }
        else if (_phase == GuidePhase.Stage1_Drag
                 || _phase == GuidePhase.Stage5_Drag
                 || _phase == GuidePhase.Stage7_Drag
                 || _phase == GuidePhase.Stage8_Drag
                 || _phase == GuidePhase.Stage10_PullAndGoal)
        {
            RestoreCoinGuideElementIfNeeded();
            if (_phase == GuidePhase.Stage7_Drag)
            {
                SetGuideExplanationVisible(false);
            }
        }
    }

    bool IsGuideRunning()
    {
        return _guideStarted
               && _phase != GuidePhase.Inactive
               && _phase != GuidePhase.Completed;
    }

    /// <summary>
    /// Guide çalışırken (Aşama 1–8, 10) InvalidMove kuralları kapalı. Aşama 9/11 açık.
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
            or GuidePhase.Stage6_PowerAim
            or GuidePhase.Stage7_Drag
            or GuidePhase.Stage8_Drag
            or GuidePhase.Stage10_PullAndGoal;
    }

    void SetPhase(GuidePhase phase)
    {
        _phase = phase;
        SyncInvalidMovePresenterObject();
    }

    void SyncInvalidMovePresenterObject()
    {
        // InvalidMove yalnızca InvalidMoveFeedbackPresenter tarafından gösterilmeli.
        // Kurallar kapalıyken zorla gizle; kurallar açıkken Find ile görünür yapma.
        if (!ShouldSuppressInvalidMoveRules)
        {
            return;
        }

        GameObject invalidMove = GameObject.Find("InvalidMove");
        if (invalidMove == null)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null
                    && candidate.name == "InvalidMove"
                    && candidate.gameObject.scene.IsValid())
                {
                    invalidMove = candidate.gameObject;
                    break;
                }
            }
        }

        if (invalidMove != null)
        {
            invalidMove.SetActive(false);
        }
    }

    void OnIntroFlythroughFinished()
    {
        BeginGuide();
    }

    void SetOnboardingCanvasVisible(bool visible)
    {
        if (_onboardingCanvas != null)
        {
            _onboardingCanvas.enabled = visible;
        }
    }

    void CacheCoinSpawnPoses()
    {
        _centerCoinSpawnPosition = OnboardingSceneBootstrap.CenterCoinSpawnPosition;
        _centerCoinSpawnRotation = OnboardingSceneBootstrap.CenterCoinSpawnRotation;

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
    }

    void BeginGuide()
    {
        if (_guideStarted || _phase != GuidePhase.Inactive)
        {
            return;
        }

        MatchIntroCameraFlythrough.Finished -= OnIntroFlythroughFinished;
        CacheCoinSpawnPoses();

        SetOnboardingCanvasVisible(true);
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

    Transform GetGuidedCoinTransform()
    {
        return _openingCoin;
    }

    CoinDragController GetGuidedCoinDragController()
    {
        return GetCenterCoinDragController();
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

    void SetCoinGuideAnchors()
    {
        Vector3 coinPosition = GetGuidedCoinWorldPosition();
        _spotlightWorldAnchor = coinPosition;
        _guidePointerWorldAnchor = coinPosition;
        _guideAnchorMode = GuideAnchorMode.Coin;
    }

    Vector3 GetGuidedCoinWorldPosition()
    {
        CoinDragController dragController = GetGuidedCoinDragController();
        if (dragController != null)
        {
            return dragController.transform.position;
        }

        Transform guidedCoin = GetGuidedCoinTransform();
        return guidedCoin != null ? guidedCoin.position : Vector3.zero;
    }

    void SetPullGuideAnchors(Vector3 worldTarget)
    {
        _spotlightWorldAnchor = worldTarget;
        _guidePointerWorldAnchor = worldTarget;
        _guideAnchorMode = GuideAnchorMode.PullTarget;
    }

    float ResolveGuideVerticalOffset()
    {
        // Aşama 1 / 3: el para üzerinde; yukarı boşluk yok.
        if (UsesHandDragAnimation())
        {
            return 0f;
        }

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

    void StartArrowAnimation()
    {
        if (_arrowRoutine != null)
        {
            return;
        }

        _arrowBaseLocalPosition = _activeArrow != null ? _activeArrow.anchoredPosition : Vector2.zero;
        if (UsesHandDragAnimation())
        {
            _arrowRoutine = StartCoroutine(AnimateStageOneHandDragLoop());
            return;
        }

        _arrowRoutine = StartCoroutine(AnimateArrowLoop());
    }

    bool UsesTextOnlyExplanationLayout(RectTransform guideElement)
    {
        return !UsesPullGuideMaskAndArrow()
            && guideElement == _pullGuideElement
            && (_phase == GuidePhase.Stage2_ReleaseToShot
                || _phase == GuidePhase.Stage4_AlignShot
                || _phase == GuidePhase.Stage6_PowerAim);
    }

    bool UsesHandDragAnimation()
    {
        return _phase is GuidePhase.Stage1_Drag
            or GuidePhase.Stage3_DragAgain
            or GuidePhase.Stage5_Drag
            or GuidePhase.Stage6_PowerAim
            or GuidePhase.Stage7_Drag
            or GuidePhase.Stage8_Drag
            or GuidePhase.Stage10_PullAndGoal;
    }

    Vector2 GetActiveHandScreenOffset()
    {
        return _phase switch
        {
            GuidePhase.Stage3_DragAgain => _stage3HandScreenOffset,
            GuidePhase.Stage5_Drag => _stage5HandScreenOffset,
            GuidePhase.Stage6_PowerAim => _stage6HandScreenOffset,
            GuidePhase.Stage7_Drag => _stage7HandScreenOffset,
            GuidePhase.Stage8_Drag => _stage8HandScreenOffset,
            GuidePhase.Stage10_PullAndGoal => _stage10HandScreenOffset,
            _ => _stage1HandScreenOffset
        };
    }

    float GetActiveHandDragWorldDistance()
    {
        return _phase switch
        {
            GuidePhase.Stage3_DragAgain => _stage3HandDragWorldDistance,
            GuidePhase.Stage5_Drag => _stage5HandDragWorldDistance,
            GuidePhase.Stage6_PowerAim => _stage6HandDragWorldDistance,
            GuidePhase.Stage7_Drag => _stage7HandDragWorldDistance,
            GuidePhase.Stage8_Drag => _stage8HandDragWorldDistance,
            GuidePhase.Stage10_PullAndGoal => _stage10HandDragWorldDistance,
            _ => _stageOneHandDragWorldDistance
        };
    }

    float GetActiveHandMoveDuration()
    {
        return _phase switch
        {
            GuidePhase.Stage3_DragAgain => _stage3HandMoveDuration,
            GuidePhase.Stage5_Drag => _stage5HandMoveDuration,
            GuidePhase.Stage6_PowerAim => _stage6HandMoveDuration,
            GuidePhase.Stage7_Drag => _stage7HandMoveDuration,
            GuidePhase.Stage8_Drag => _stage8HandMoveDuration,
            GuidePhase.Stage10_PullAndGoal => _stage10HandMoveDuration,
            _ => _stageOneHandMoveDuration
        };
    }

    float GetActiveHandPressHold()
    {
        return _phase switch
        {
            GuidePhase.Stage3_DragAgain => _stage3HandPressHold,
            GuidePhase.Stage5_Drag => _stage5HandPressHold,
            GuidePhase.Stage6_PowerAim => _stage6HandPressHold,
            GuidePhase.Stage7_Drag => _stage7HandPressHold,
            GuidePhase.Stage8_Drag => _stage8HandPressHold,
            GuidePhase.Stage10_PullAndGoal => _stage10HandPressHold,
            _ => _stageOneHandPressHold
        };
    }

    float GetActiveHandPause()
    {
        return _phase switch
        {
            GuidePhase.Stage3_DragAgain => _stage3HandPause,
            GuidePhase.Stage5_Drag => _stage5HandPause,
            GuidePhase.Stage6_PowerAim => _stage6HandPause,
            GuidePhase.Stage7_Drag => _stage7HandPause,
            GuidePhase.Stage8_Drag => _stage8HandPause,
            GuidePhase.Stage10_PullAndGoal => _stage10HandPause,
            _ => _stageOneHandPause
        };
    }

    Vector2 GetActiveHandExplanationScreenOffset()
    {
        return _phase switch
        {
            GuidePhase.Stage2_ReleaseToShot => _stage2ExplanationScreenOffset,
            GuidePhase.Stage4_AlignShot => _stage4ExplanationScreenOffset,
            GuidePhase.Stage3_DragAgain => _stage3ExplanationScreenOffset,
            GuidePhase.Stage5_Drag => _stage5ExplanationScreenOffset,
            GuidePhase.Stage6_PowerAim => _stage6ExplanationScreenOffset,
            GuidePhase.Stage7_Drag => _stage7ExplanationScreenOffset,
            GuidePhase.Stage8_Drag => _stage8ExplanationScreenOffset,
            GuidePhase.Stage10_PullAndGoal => _stage10ExplanationScreenOffset,
            _ => _stage1ExplanationScreenOffset
        };
    }

    Vector2 GetHandRestLocalPosition()
    {
        Vector2 offset = GetActiveHandScreenOffset();
        // Guide clamp / perspektif sapmalarında bile eli coin'in gerçek ekran konumuna bağla.
        if (TryWorldToArrowAnchoredPosition(GetGuidedCoinWorldPosition(), out Vector2 coinLocal))
        {
            return coinLocal + offset;
        }

        return offset;
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

    bool UsesPullGuideMaskAndArrow()
    {
        return _phase != GuidePhase.Stage2_ReleaseToShot
            && _phase != GuidePhase.Stage4_AlignShot
            && _phase != GuidePhase.Stage5_Drag
            && _phase != GuidePhase.Stage6_PowerAim
            && _phase != GuidePhase.Stage7_Drag
            && _phase != GuidePhase.Stage9_PassBetween
            && _phase != GuidePhase.Stage10_PullAndGoal
            && _phase != GuidePhase.Stage11_PassAndGoal;
    }

    void HidePullGuideMaskAndArrow()
    {
        if (_overlayRect != null)
        {
            _overlayRect.gameObject.SetActive(false);
        }

        if (_activeArrow == _pullArrow || _activeArrow == _arrow)
        {
            StopArrowAnimation();
        }

        if (_pullArrow != null)
        {
            _pullArrow.gameObject.SetActive(false);
        }

        HidePullGuideVisuals();
    }

    void ShowGuideTextOnlyPresentation()
    {
        HidePullGuideMaskAndArrow();
        HideCoinGuideVisuals();

        _activeGuideElement = _pullGuideElement != null ? _pullGuideElement : _guideElement;
        _activeGuideText = _pullGuideText != null ? _pullGuideText : _guideText;
        _activeExplanationImage = GetExplanationImage(_activeGuideElement);
        _activeArrow = null;

        if (_activeGuideElement == null)
        {
            return;
        }

        _activeGuideElement.gameObject.SetActive(true);
        SyncGuideElementLayout(_activeGuideElement);
        UpdateActiveGuideElementPosition();
    }

    void EnsurePullGuidePresentation()
    {
        HideCoinGuideElementOnly();
        if (!UsesPullGuideMaskAndArrow())
        {
            if (_activeGuideElement == null
                || !_activeGuideElement.gameObject.activeSelf
                || _activeArrow != null)
            {
                ShowGuideTextOnlyPresentation();
            }
            else
            {
                UpdateActiveGuideElementPosition();
            }

            return;
        }

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
            _openingCoin = OnboardingSceneBootstrap.CenterCoinTransform;
            if (_openingCoin == null)
            {
                GameObject openingCoinObject = GameObject.Find("Coin_P2");
                if (openingCoinObject != null)
                {
                    _openingCoin = openingCoinObject.transform;
                }
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

        if (_stage6Alert == null && _canvasRect != null)
        {
            Transform alert = _canvasRect.Find("Alert-6");
            if (alert != null)
            {
                _stage6Alert = alert.gameObject;
            }
        }

        if (_stage7Alert == null && _canvasRect != null)
        {
            Transform alert7 = _canvasRect.Find("Alert-7");
            if (alert7 != null)
            {
                _stage7Alert = alert7.gameObject;
            }
        }

        if (_stage9Alert == null && _canvasRect != null)
        {
            Transform alert9 = _canvasRect.Find("Alert-9");
            if (alert9 != null)
            {
                _stage9Alert = alert9.gameObject;
            }
        }

        if (_stage10Alert == null && _canvasRect != null)
        {
            Transform alert10 = _canvasRect.Find("Alert-10");
            if (alert10 != null)
            {
                _stage10Alert = alert10.gameObject;
            }
        }

        EnsureSuccessPanelResolved();

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
            _overlayMaterial.SetFloat(WedgeEnabledId, 0f);
            _overlayMaterial.SetFloat(HalfPlaneEnabledId, 0f);
            _overlayMaterial.SetFloat(GateEnabledId, 0f);
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
        bool stageOneHand = UsesHandDragAnimation() && arrow == _arrow;
        bool textOnlyExplanation = UsesTextOnlyExplanationLayout(guideElement);
        bool hideExplanation = _phase == GuidePhase.Stage7_Drag;

        if (hideExplanation)
        {
            explanation.gameObject.SetActive(false);
        }
        else if (!explanation.gameObject.activeSelf)
        {
            explanation.gameObject.SetActive(true);
        }

        if (stageOneHand)
        {
            PrepareStageOneHandVisual();
        }
        else if (!textOnlyExplanation)
        {
            arrow.localScale = Vector3.one * (compactCoinLayout ? _coinGuideArrowScale : _pullGuideArrowScale);
            ConfigureArrowLayout(arrow, resetAnchoredPosition: compactCoinLayout);
        }
        else
        {
            arrow.gameObject.SetActive(false);
        }

        GuideExplanationAutoWidth autoWidth = explanation.GetComponent<GuideExplanationAutoWidth>();
        if (!hideExplanation)
        {
            autoWidth?.Refresh();
        }

        Canvas.ForceUpdateCanvases();
        if (!hideExplanation)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(explanation);
        }

        if (!textOnlyExplanation)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(arrow);
        }

        float arrowHeight = textOnlyExplanation ? 0f : MeasureGuideChildHeight(arrow);
        float explanationHeight = hideExplanation ? 0f : MeasureGuideChildHeight(explanation);
        float arrowWidth = textOnlyExplanation ? 0f : MeasureGuideChildWidth(arrow);
        float explanationWidth = hideExplanation ? 0f : MeasureGuideChildWidth(explanation);

        float explanationGap = compactCoinLayout ? _coinGuideExplanationGap : _explanationArrowGap;
        if (!hideExplanation && (stageOneHand || textOnlyExplanation))
        {
            // El / metin-only pull aşamalarında explanation Inspector offset'i ile konumlanır.
            explanation.anchorMin = new Vector2(0.5f, 0.5f);
            explanation.anchorMax = new Vector2(0.5f, 0.5f);
            explanation.pivot = new Vector2(0.5f, 0.5f);
            explanation.anchoredPosition = GetActiveHandExplanationScreenOffset();
        }
        else if (!hideExplanation && compactCoinLayout)
        {
            ApplyCompactCoinArrowLayout(arrow, arrowHeight);
            ApplyCompactCoinExplanationLayout(explanation, arrowHeight);
        }

        float width = Mathf.Max(arrowWidth, explanationWidth, _guideElementMinWidth);
        float height = hideExplanation
            ? (textOnlyExplanation ? 0f : arrowHeight)
            : textOnlyExplanation
            ? explanationHeight
            : stageOneHand
            ? arrowHeight * 0.5f + explanationGap + explanationHeight + arrowHeight * 0.5f
            : arrowHeight + explanationGap + explanationHeight;
        guideElement.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        guideElement.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (!hideExplanation && !stageOneHand && !textOnlyExplanation && compactCoinLayout)
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
        GateIndicator.Instance?.Hide();
        SetExplanationBackground(_positiveExplanationColor);
    }

    void HideCoinGuideVisuals()
    {
        if (_activeArrow == _arrow)
        {
            StopArrowAnimation();
        }

        RestoreDefaultArrowVisual();

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
        SetGuideExplanationVisible(_phase != GuidePhase.Stage7_Drag);
    }

    void SetGuideExplanationVisible(bool visible)
    {
        if (_guideElement == null)
        {
            return;
        }

        Transform explanation = _guideElement.Find("Explanation");
        if (explanation != null)
        {
            explanation.gameObject.SetActive(visible);
        }

        if (_pullGuideElement == null)
        {
            return;
        }

        Transform pullExplanation = _pullGuideElement.Find("Explanation");
        if (pullExplanation != null && _phase == GuidePhase.Stage7_Drag)
        {
            pullExplanation.gameObject.SetActive(false);
        }
    }

    void PrepareCoinGuideArrow()
    {
        if (_arrow == null)
        {
            _arrowBaseLocalPosition = Vector2.zero;
            return;
        }

        if (UsesHandDragAnimation())
        {
            PrepareStageOneHandVisual();
            return;
        }

        RestoreDefaultArrowVisual();
        ConfigureArrowLayout(_arrow, resetAnchoredPosition: true);
        if (_guideElement != null)
        {
            SyncGuideElementLayout(_guideElement);
        }

        _arrowBaseLocalPosition = _arrow.anchoredPosition;
    }

    void CacheDefaultArrowVisualIfNeeded()
    {
        if (_hasCachedDefaultArrowLayout || _arrow == null)
        {
            return;
        }

        Image arrowImage = _arrow.GetComponent<Image>();
        if (arrowImage != null)
        {
            _cachedDefaultArrowSprite = arrowImage.sprite;
        }

        _cachedDefaultArrowSize = _arrow.sizeDelta;
        _cachedDefaultArrowPivot = _arrow.pivot;
        if (_guideElement != null)
        {
            _cachedDefaultGuidePivot = _guideElement.pivot;
        }

        _hasCachedDefaultArrowLayout = true;
    }

    void EnsureStageOneHandSprites()
    {
        if (_handDefaultSprite != null && _handPressedSprite != null)
        {
            return;
        }

        HandCursorConfig config = Resources.Load<HandCursorConfig>("HandCursorConfig");
        if (config != null)
        {
            if (_handDefaultSprite == null)
            {
                _handDefaultSprite = config.DefaultHand;
            }

            if (_handPressedSprite == null)
            {
                _handPressedSprite = config.PressedHand;
            }
        }

        if (_handDefaultSprite == null)
        {
            _handDefaultSprite = Resources.Load<Sprite>("Hand_Default");
        }

        if (_handPressedSprite == null)
        {
            _handPressedSprite = Resources.Load<Sprite>("Hand_Pressed");
        }
    }

    void PrepareStageOneHandVisual()
    {
        if (_arrow == null)
        {
            return;
        }

        CacheDefaultArrowVisualIfNeeded();
        EnsureStageOneHandSprites();

        if (_guideElement != null)
        {
            _guideElement.pivot = new Vector2(0.5f, 0.5f);
        }

        _arrow.anchorMin = new Vector2(0.5f, 0.5f);
        _arrow.anchorMax = new Vector2(0.5f, 0.5f);
        _arrow.pivot = new Vector2(0.5f, 0.5f);
        _arrow.anchoredPosition = GetHandRestLocalPosition();
        _arrow.localScale = Vector3.one;
        _arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _stageOneHandScreenSize);
        _arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _stageOneHandScreenSize);

        ApplyStageOneHandSprite(pressed: false);
        _arrowBaseLocalPosition = _arrow.anchoredPosition;
    }

    void ApplyStageOneHandSprite(bool pressed)
    {
        if (_arrow == null)
        {
            return;
        }

        Image handImage = _arrow.GetComponent<Image>();
        if (handImage == null)
        {
            return;
        }

        Sprite sprite = pressed ? _handPressedSprite : _handDefaultSprite;
        if (sprite != null)
        {
            handImage.sprite = sprite;
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
    }

    void RestoreDefaultArrowVisual()
    {
        if (_arrow == null || !_hasCachedDefaultArrowLayout)
        {
            return;
        }

        if (_guideElement != null)
        {
            _guideElement.pivot = _cachedDefaultGuidePivot;
        }

        Image arrowImage = _arrow.GetComponent<Image>();
        if (arrowImage != null && _cachedDefaultArrowSprite != null)
        {
            arrowImage.sprite = _cachedDefaultArrowSprite;
        }

        _arrow.pivot = _cachedDefaultArrowPivot;
        _arrow.sizeDelta = _cachedDefaultArrowSize;
        ConfigureArrowLayout(_arrow, resetAnchoredPosition: true);
    }

    void ShowPullGuideVisuals()
    {
        if (!UsesPullGuideMaskAndArrow())
        {
            ShowGuideTextOnlyPresentation();
            return;
        }

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

        _pullArrow.gameObject.SetActive(true);
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
            _overlayRect.gameObject.SetActive(UsesPullGuideMaskAndArrow());
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
            GuidePhase.Stage6_PowerAim => 6,
            GuidePhase.Stage7_Drag => 7,
            GuidePhase.Stage8_Drag => 8,
            // GateIndicator denemesi oyuncu için Aşama 8'in devamı.
            GuidePhase.Stage9_PassBetween => 8,
            GuidePhase.Stage10_PullAndGoal => 9,
            GuidePhase.Stage11_PassAndGoal => 10,
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
        if (_phase == GuidePhase.Stage7_Drag)
        {
            UpdateStageSevenWedgeSpotlight();
            return;
        }

        if (!UsesPullGuideMaskAndArrow())
        {
            if (_overlayRect != null)
            {
                _overlayRect.gameObject.SetActive(false);
            }

            return;
        }

        Camera worldCamera = ResolveWorldCamera();
        if (_overlayMaterial == null || _overlayRect == null || worldCamera == null)
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
        _overlayMaterial.SetFloat(WedgeEnabledId, 0f);
        _overlayMaterial.SetFloat(HalfPlaneEnabledId, 0f);
        _overlayMaterial.SetFloat(GateEnabledId, 0f);

        if (!TryGetSpotlightWorldPosition(out Vector3 worldPosition))
        {
            return;
        }

        if (!TryGetOverlayUv(worldCamera, overlayRect, worldPosition, out Vector2 holeCenter))
        {
            return;
        }

        if (_overlayRect != null && !_overlayRect.gameObject.activeSelf)
        {
            _overlayRect.gameObject.SetActive(true);
        }

        _overlayMaterial.SetVector(HoleCenterId, new Vector4(holeCenter.x, holeCenter.y, 0f, 0f));
    }

    void UpdateStageSevenWedgeSpotlight()
    {
        Camera worldCamera = ResolveWorldCamera();
        if (_overlayMaterial == null || _overlayRect == null || worldCamera == null)
        {
            return;
        }

        if (!TryResolveStageSevenGateEndpoints(out Vector3 gateStart, out Vector3 gateEnd))
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

        Vector3 coinPosition = GetGuidedCoinWorldPosition();
        if (!TryGetOverlayUv(worldCamera, overlayRect, coinPosition, out Vector2 apexUv)
            || !TryGetOverlayUv(worldCamera, overlayRect, gateStart, out Vector2 leftUv)
            || !TryGetOverlayUv(worldCamera, overlayRect, gateEnd, out Vector2 rightUv))
        {
            return;
        }

        if (!_overlayRect.gameObject.activeSelf)
        {
            _overlayRect.gameObject.SetActive(true);
        }

        float aspect = overlayRect.width / overlayRect.height;
        _overlayMaterial.SetFloat(HoleAspectId, aspect);
        _overlayMaterial.SetFloat(HalfPlaneEnabledId, 0f);
        _overlayMaterial.SetFloat(WedgeEnabledId, 1f);
        _overlayMaterial.SetFloat(GateEnabledId, 0f);
        _overlayMaterial.SetFloat(WedgeSoftnessId, Mathf.Max(0.001f, _stage7WedgeSoftnessUv));
        _overlayMaterial.SetVector(WedgeApexId, new Vector4(apexUv.x, apexUv.y, 0f, 0f));
        _overlayMaterial.SetVector(WedgePointLeftId, new Vector4(leftUv.x, leftUv.y, 0f, 0f));
        _overlayMaterial.SetVector(WedgePointRightId, new Vector4(rightUv.x, rightUv.y, 0f, 0f));
        _overlayMaterial.SetVector(HoleCenterId, new Vector4(apexUv.x, apexUv.y, 0f, 0f));
    }

    bool TryGetOverlayUv(Camera worldCamera, Rect overlayRect, Vector3 worldPosition, out Vector2 uv)
    {
        uv = default;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRect,
                screenPoint,
                GetUiCamera(),
                out Vector2 localPoint))
        {
            return false;
        }

        // Kama geometrisi bozulmasın diye 0-1 aralığına kırpmadan hesaplanır.
        uv = new Vector2(
            (localPoint.x - overlayRect.xMin) / overlayRect.width,
            (localPoint.y - overlayRect.yMin) / overlayRect.height);
        return true;
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
        // El rehberi coin'in gerçek duruşuna yapışmalı; kenar clamp'i ofseti bozar.
        if (!UsesHandDragAnimation())
        {
            anchoredPosition = ClampGuideAnchoredPosition(anchoredPosition);
            if (_guideAnchorMode == GuideAnchorMode.Coin)
            {
                anchoredPosition.y = Mathf.Max(anchoredPosition.y, desiredAnchoredPosition.y);
            }
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

    IEnumerator AnimateStageOneHandDragLoop()
    {
        EnsureStageOneHandSprites();
        PrepareStageOneHandVisual();

        GuidePhase loopPhase = _phase;
        float moveDuration = Mathf.Max(0.05f, GetActiveHandMoveDuration());
        float pressHold = Mathf.Max(0.01f, GetActiveHandPressHold());
        float pause = Mathf.Max(0.01f, GetActiveHandPause());

        while (_phase == loopPhase && UsesHandDragAnimation() && _activeArrow != null)
        {
            UpdateActiveGuideElementPosition();
            PrepareStageOneHandVisual();

            Vector2 coinLocal = GetHandRestLocalPosition();
            Vector2 pullLocal = coinLocal + GetHandDragLocalDelta();

            // Para üstünde — Default
            ApplyStageOneHandSprite(pressed: false);
            _activeArrow.anchoredPosition = coinLocal;
            yield return new WaitForSecondsRealtime(pause);

            // Tut — Pressed
            ApplyStageOneHandSprite(pressed: true);
            yield return new WaitForSecondsRealtime(pressHold);

            // Aşama 1: atışın tersine / Aşama 3: yukarıdan aşağı
            yield return AnimateStageOneHandMove(coinLocal, pullLocal, moveDuration, loopPhase);

            // Bırak — Default
            ApplyStageOneHandSprite(pressed: false);
            yield return new WaitForSecondsRealtime(pause);

            // Tekrar paraya dön
            yield return AnimateStageOneHandMove(pullLocal, coinLocal, moveDuration, loopPhase);
        }

        _arrowRoutine = null;
    }

    Vector2 GetHandDragLocalDelta()
    {
        float fallback = _stageOneHandScreenSize * 0.85f;

        // Stage 3: açılı sürükleme — sadece yukarıdan aşağı.
        if (_phase == GuidePhase.Stage3_DragAgain)
        {
            return Vector2.down * fallback;
        }

        if (_activeArrow == null)
        {
            return Vector2.down * (_stageOneHandScreenSize * 0.75f);
        }

        Vector3 coinWorld = GetGuidedCoinWorldPosition();
        Vector3 launchDir = GetHandDragLaunchDirection(coinWorld);
        Vector3 pullWorld = -launchDir * Mathf.Max(0.05f, GetActiveHandDragWorldDistance());
        Vector3 pullEndWorld = coinWorld + pullWorld;

        if (TryWorldToArrowAnchoredPosition(coinWorld, out Vector2 coinLocal)
            && TryWorldToArrowAnchoredPosition(pullEndWorld, out Vector2 pullLocal))
        {
            Vector2 delta = pullLocal - coinLocal;
            if (delta.sqrMagnitude > 1f)
            {
                return delta;
            }
        }

        // Fallback: ekranda aşağı çek (tipik atış yukarı/kaleye).
        return Vector2.down * fallback;
    }

    Vector3 GetHandDragLaunchDirection(Vector3 coinPosition)
    {
        if (_phase == GuidePhase.Stage8_Drag)
        {
            return GetStageEightLaunchDirection(coinPosition);
        }

        if (_phase == GuidePhase.Stage7_Drag)
        {
            return GetStageSevenLaunchDirection(coinPosition);
        }

        if (_phase == GuidePhase.Stage5_Drag || _phase == GuidePhase.Stage6_PowerAim)
        {
            return GetStageSixLaunchDirection(coinPosition);
        }

        return GetStageTwoLaunchDirection(coinPosition);
    }

    Vector3 GetStageEightLaunchDirection(Vector3 coinPosition)
    {
        if (!TryGetStageNineGateMidpoint(out Vector3 gateMid))
        {
            return GetStageTwoLaunchDirection(coinPosition);
        }

        Vector3 toGate = gateMid - coinPosition;
        toGate.y = 0f;
        return toGate.sqrMagnitude < 0.0001f ? Vector3.forward : toGate.normalized;
    }

    bool TryGetStageNineGateMidpoint(out Vector3 midpoint)
    {
        midpoint = default;
        Transform left = ResolveSideCoinTransform("Coin_P1", OnboardingSceneBootstrap.LeftCoinTransform);
        Transform right = ResolveSideCoinTransform("Coin_P3", OnboardingSceneBootstrap.RightCoinTransform);
        if (left == null || right == null)
        {
            return false;
        }

        midpoint = (left.position + right.position) * 0.5f;
        return true;
    }

    Vector3 GetStageSevenLaunchDirection(Vector3 coinPosition)
    {
        if (!TryGetStageSevenGateMidpoint(out Vector3 gateMid))
        {
            return GetStageSixLaunchDirection(coinPosition);
        }

        Vector3 toGate = gateMid - coinPosition;
        toGate.y = 0f;
        return toGate.sqrMagnitude < 0.0001f ? Vector3.forward : toGate.normalized;
    }

    IEnumerator AnimateStageOneHandMove(Vector2 from, Vector2 to, float duration, GuidePhase loopPhase)
    {
        if (_activeArrow == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_phase != loopPhase || _activeArrow == null)
            {
                yield break;
            }

            UpdateActiveGuideElementPosition();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _activeArrow.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }

        if (_activeArrow != null)
        {
            _activeArrow.anchoredPosition = to;
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
