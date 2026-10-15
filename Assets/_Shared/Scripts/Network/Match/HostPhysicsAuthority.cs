using UnityEngine;

/// <summary>
/// Online maçlarda host-authoritative fizik: host, tahtadaki 6 paranın pozisyonunu hareket
/// SÜRESİNCE sürekli (periyodik) misafire yayınlar (Faz 1) ve misafirin atışları için TEK
/// otoriter gol hakemliğini yapar (Faz 2).
///
/// NEDEN SÜREKLİ (tek seferlik "durunca düzelt" DEĞİL): Unity'nin PhysX'i farklı cihaz
/// mimarilerinde (ARM/x86, farklı telefonlar) resmi olarak deterministik DEĞİL — iki bağımsız
/// simülasyon birkaç çarpışmadan sonra KAOTİK şekilde ayrışabiliyor (küçük bir başlangıç farkı,
/// birkaç sekme sonra tamamen farklı bir sonuca dönüşebiliyor). Atış bitene kadar bekleyip TEK
/// bir düzeltme yapmak bu sapmayı yakalayamıyordu — kaos çoktan gerçekleşmiş oluyordu. Bunun
/// yerine misafir, host'un pozisyonunu atış BOYUNCA sürekli, SmoothDamp ile yumuşakça takip
/// ediyor (bkz. FixedUpdate) — sapma büyümeye hiç fırsat bulamadan erkenden düzeltiliyor. Bu,
/// Photon Fusion'ın kendi "Interpolation"/"Forecast Physics" sisteminin ve genel olarak
/// client-side prediction + server reconciliation netcode deseninin temel tekniğidir.
///
/// ÖNEMLİ ASİMETRİ: misafir KENDİ attığı atışı çözümlerken (GameRulesManager.ResolvingShotCoin)
/// bu takip SADECE O TEK PARA için duruyor — kendi atışımızı sıfır gecikmeyle görürken, host'un
/// o atış için gönderdiği rapor her zaman bir ağ gidiş-dönüşü kadar geride olur; sürekli takip,
/// güncel yerel durumu host'un eski raporuna geri çekip "ileri-geri" sarsıntısı yaratırdı.
/// DİĞER 5 PARA (kendi atışımızın çarpabileceği paralar dahil) bu süre boyunca da host
/// düzeltmesini almaya devam eder — tüm tahtayı bastırmak, atış süresince oluşan çarpışma
/// kaynaklı sapmanın hiç telafi edilmeden birikmesine yol açıyordu.
/// </summary>
public class HostPhysicsAuthority : MonoBehaviour
{
    public static HostPhysicsAuthority Instance { get; private set; }

    static readonly string[] PlayerCoinSenderNames = { "Coin_P1", "Coin_P2", "Coin_P3" };
    static readonly string[] EnemyCoinSenderNames = { "Coin_E1", "Coin_E2", "Coin_E3" };

    // Gönderilen sıra: p1,p2,p3 (host'un kendi takımı), e1,e2,e3 (host'un rakip gördüğü takım).
    // Misafir tarafında bu sıraya karşılık gelen YEREL hedef adları (FlipTeamToken ile P<->E
    // değişimi, rakam aynı kalır) — önceden hesaplanmış, her alışta FlipTeamToken çağırmaya gerek yok.
    static readonly string[] LocalFollowTargetNames = { "Coin_E1", "Coin_E2", "Coin_E3", "Coin_P1", "Coin_P2", "Coin_P3" };
    const int SlotCount = 6;

    const float SnapshotRateHz = 15f;
    const float SnapshotIntervalSeconds = 1f / SnapshotRateHz;

    /// <summary>SmoothDamp'in yaklaşık "hedefe ulaşma süresi" — kısa tutulursa host'a sıkı
    /// takip eder (sapma büyümez), çok kısa olursa 66ms'lik adımlar arasında yine titreyebilir.</summary>
    const float FollowSmoothTime = 0.06f;

    MatchShotNetworkRelay _boundRelay;
    GameRulesManager _boundRules;

    // Host: pozisyon yayını
    bool _wasAtRest = true;
    float _snapshotTimer;
    int _batchSeq;
    Transform[] _cachedPlayerCoins;
    Transform[] _cachedEnemyCoins;

    // Host: gol hakemliği (misafirin atışları için)
    CoinIdentity _pendingGuestShotCoin;
    int _pendingGuestShotSeq = -1;
    bool _pendingGuestShotEnteredGoal;

    // Misafir: snapshot dedup + sürekli yumuşak takip durumu (6 slot, LocalFollowTargetNames sırasıyla)
    int _lastAppliedBatchSeq = -1;
    CoinIdentity[] _followCoins;
    Rigidbody[] _followRigidbodies;
    Vector3[] _followTargets;
    Vector3[] _followVelocities;
    bool[] _followHasTarget;

    float? _mirrorCenterZ;
    GoalZone _cachedMyGoal;

    // Kendi atışımız bitince (ResolvingShotCoin null'a dönünce) o coin için hedefi yeniden
    // taban alabilmek amacıyla önceki frame'in bastırılan parasını takip ediyoruz — bkz. FixedUpdate.
    CoinIdentity _previousResolvingCoin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("HostPhysicsAuthority");
        Instance = go.AddComponent<HostPhysicsAuthority>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        OnlineMatchSession.SessionStarted += ResetMatchState;
        OnlineMatchSession.SessionCleared += ResetMatchState;
    }

    void OnDisable()
    {
        OnlineMatchSession.SessionStarted -= ResetMatchState;
        OnlineMatchSession.SessionCleared -= ResetMatchState;
    }

    void ResetMatchState()
    {
        _wasAtRest = true;
        _snapshotTimer = 0f;
        _cachedPlayerCoins = null;
        _cachedEnemyCoins = null;
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;
        _pendingGuestShotEnteredGoal = false;
        _mirrorCenterZ = null;
        _cachedMyGoal = null;

        _followCoins = null;
        _followRigidbodies = null;
        _followTargets = null;
        _followVelocities = null;
        _followHasTarget = null;
        _previousResolvingCoin = null;
    }

    GoalZone GetMyGoalZone()
    {
        if (_cachedMyGoal == null)
        {
            _cachedMyGoal = GoalZone.FindPlayerGoalArea();
        }

        return _cachedMyGoal;
    }

    void Update()
    {
        if (!OnlineMatchSession.IsOnlineMatch || OnlineMatchSession.Channel == null)
        {
            return;
        }

        EnsureRelayBound();
        EnsureRulesBound();

        if (OnlineMatchSession.Channel.IsHost)
        {
            HostUpdate();
        }
    }

    /// <summary>
    /// Misafir tarafı: host'tan gelen en son hedefe doğru 6 parayı SÜREKLİ, fizik-adımı başına
    /// SmoothDamp ile yumuşakça kaydırır — ani teleport yok, ve host'un gerçek konumundan uzun
    /// süre kopup kaotik şekilde ayrılmasına da izin verilmiyor.
    /// </summary>
    void FixedUpdate()
    {
        if (!OnlineMatchSession.IsOnlineMatch || OnlineMatchSession.Channel == null || OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        GameRulesManager rules = GameRulesManager.Instance;

        // Gol dizisi (freeze → kutlama → AnimateCoinsToPositions ile reset tween'i) sürerken
        // TÜM tahtayı bastırıyoruz: bu pencerede GameRulesManager.ResolvingShotCoin HER ZAMAN
        // null'dır (TryBeginGoalSequence, _resolvingShotCoin'i golden önce temizler) — yani
        // aşağıdaki tek-para bastırması bu pencereyi hiç KAPSAMAZ. Kapsamazsa, host'un ESKİ
        // (golden önceki, henüz güncellenmemiş) snapshot hedefine doğru rb.MovePosition ile
        // sürekli çekiştirme, GameRulesManager'ın reset tween'iyle AYNI ANDA aynı Rigidbody'yi
        // hareket ettirip onunla yarışır — "gol olduğunda coinler reset pozisyonuna gelmiyor"
        // bug'ının sebebi tam olarak buydu. OnLocalRoundReset zaten reset bitince hedefleri
        // temizliyor; bu pencere boyunca da hiç dokunmamak güvenli.
        if (rules != null && (rules.IsGoalSequenceActive || rules.HasPendingGoalFreeze))
        {
            return;
        }

        // KENDİ atışımız çözümlenirken (GameRulesManager.ResolvingShotCoin SADECE yerel
        // oyuncunun kendi başlattığı atış için set edilir — gelen rakip atışlarında hiç set
        // edilmez) O TEK PARA için takibi durduruyoruz. Sebep: kendi atışımızı SIFIR gecikmeyle
        // (yerel) görüyoruz, ama host'un o aynı atış için gönderdiği rapor her zaman bir ağ
        // gidiş-dönüşü KADAR GERİDE — sürekli takip, o paranın güncel/doğru pozisyonunu host'un
        // eski raporuna geri çekip "ileri-geri" sarsıntısına yol açardı. ÖNEMLİ: bu bastırma
        // SADECE o paraya uygulanır — diğer 5 para (kendi atışımızın çarpabileceği paralar dahil)
        // host düzeltmesini almaya devam etmeli. Eskiden TÜM tahta için bastırılıyordu; bu, atış
        // süresince oluşan çarpışma kaynaklı sapmanın hiç telafi edilmeden birikmesine (gerçek
        // cihazlarda "pozisyonlar çok uzaklaşıyor" bug'ına) yol açıyordu.
        CoinIdentity localResolvingCoin = rules != null ? rules.ResolvingShotCoin : null;

        EnsureFollowCache();

        if (_previousResolvingCoin != null && localResolvingCoin != _previousResolvingCoin)
        {
            // Kendi atışımız AZ ÖNCE bitti (bastırma bu coin için kalktı). Host, bizim atışımızı
            // ağ gecikmesi KADAR GERİDEN başlatıp kendi kopyasını simüle ediyor — biz yerel
            // fiziğimizle bu anda zaten durmuşken, host'un o coin için en son gönderdiği hedef
            // hâlâ host'un KENDİ (henüz bitmemiş) atışının ORTASINDAN bir pozisyon olabilir. Hedefi
            // olduğu gibi kullanıp SmoothDamp'e bırakırsak, para küçük bir "geri" sıçrama yapıyordu
            // (kullanıcı: "tam durduğunda ufak bir düzeltmeyle para çok az geri geliyor"). Bunun
            // yerine hedefi kendi GÜNCEL (doğru) pozisyonumuza sıfırlıyoruz — host'un atışı bitip
            // TAZE bir snapshot gönderdiğinde oradan (varsa, genelde milimetrik) yumuşakça düzeltilir.
            RebaselineFollowTarget(_previousResolvingCoin);
        }

        _previousResolvingCoin = localResolvingCoin;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!_followHasTarget[i])
            {
                continue;
            }

            CoinIdentity coin = _followCoins[i];
            if (coin == null)
            {
                continue;
            }

            if (coin == localResolvingCoin)
            {
                continue;
            }

            CoinDragController drag = coin.DragController;
            if (drag != null && drag.IsAiming)
            {
                // Tam o anda nişan alınıyorsa dokunma.
                continue;
            }

            if (coin.IsFrozenByBooster())
            {
                // IceFrozenCoinState kendi FixedUpdate'inde pozisyonu zaten sabitliyor —
                // burada da dokunursak ikisi birbirini ezip titreşim yaratabilir.
                continue;
            }

            Transform t = coin.transform;
            Vector3 current = t.position;
            Vector3 target = _followTargets[i];
            target.y = current.y;

            Vector3 velocity = _followVelocities[i];
            Vector3 next = Vector3.SmoothDamp(
                current, target, ref velocity, FollowSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
            _followVelocities[i] = velocity;

            Rigidbody rb = _followRigidbodies[i];
            if (rb != null)
            {
                rb.MovePosition(next);
            }
            else
            {
                t.position = next;
            }
        }
    }

    /// <summary>Verilen coin'in takip hedefini onun GÜNCEL pozisyonuna sıfırlar (sıfır hız ile) —
    /// bkz. FixedUpdate'teki _previousResolvingCoin geçiş kontrolü.</summary>
    void RebaselineFollowTarget(CoinIdentity coin)
    {
        if (_followCoins == null)
        {
            return;
        }

        for (int i = 0; i < _followCoins.Length; i++)
        {
            if (_followCoins[i] != coin)
            {
                continue;
            }

            _followTargets[i] = coin.transform.position;
            _followVelocities[i] = Vector3.zero;
            return;
        }
    }

    void EnsureFollowCache()
    {
        _followCoins ??= new CoinIdentity[SlotCount];
        _followRigidbodies ??= new Rigidbody[SlotCount];
        _followTargets ??= new Vector3[SlotCount];
        _followVelocities ??= new Vector3[SlotCount];
        _followHasTarget ??= new bool[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            if (_followCoins[i] == null)
            {
                _followCoins[i] = FindCoinByName(LocalFollowTargetNames[i]);
                _followRigidbodies[i] = _followCoins[i] != null ? _followCoins[i].GetComponent<Rigidbody>() : null;
            }
        }
    }

    /// <summary>
    /// MatchShotNetworkRelay.Instance her maçta YENİ bir nesne olarak spawn ediliyor — bir
    /// bool bayrak yerine gerçek referansı karşılaştırıyoruz, yoksa ikinci maçta (Instance
    /// değiştiğinde) olaylara hiç yeniden abone olunmaz (bu oturumda birkaç kez karşılaşılan
    /// bir bug sınıfı).
    /// </summary>
    void EnsureRelayBound()
    {
        if (ReferenceEquals(_boundRelay, MatchShotNetworkRelay.Instance))
        {
            return;
        }

        if (_boundRelay != null)
        {
            _boundRelay.CoinSnapshotReceived -= OnCoinSnapshotReceived;
            _boundRelay.ShotReceived -= OnHostObservedShotReceived;
            _boundRelay.ShotRollbackReceived -= OnHostObservedRollbackReceived;
        }

        _boundRelay = MatchShotNetworkRelay.Instance;

        if (_boundRelay != null)
        {
            _boundRelay.CoinSnapshotReceived += OnCoinSnapshotReceived;
            _boundRelay.ShotReceived += OnHostObservedShotReceived;
            _boundRelay.ShotRollbackReceived += OnHostObservedRollbackReceived;
        }
    }

    /// <summary>
    /// GameRulesManager.Instance de her maçta YENİ bir nesne — aynı ReferenceEquals deseniyle
    /// yeniden bağlanıyoruz. RoundReset'e abone oluyoruz: tur resetlendiğinde takip sistemindeki
    /// ESKİ (golden önceki atışa ait) hedefleri temizlemezsek, reset animasyonu bittiği an takip
    /// paraları sessizce eski/yanlış pozisyona geri çekiyordu — "bir tarafta sıfırlanıyor diğer
    /// tarafta sıfırlanmıyor" bug'ının sebebi buydu (sadece misafir tarafında takip çalışıyor).
    /// </summary>
    void EnsureRulesBound()
    {
        if (ReferenceEquals(_boundRules, GameRulesManager.Instance))
        {
            return;
        }

        if (_boundRules != null)
        {
            _boundRules.RoundReset -= OnLocalRoundReset;
        }

        _boundRules = GameRulesManager.Instance;

        if (_boundRules != null)
        {
            _boundRules.RoundReset += OnLocalRoundReset;
        }
    }

    void OnLocalRoundReset()
    {
        if (_followHasTarget == null)
        {
            return;
        }

        for (int i = 0; i < _followHasTarget.Length; i++)
        {
            _followHasTarget[i] = false;
            _followVelocities[i] = Vector3.zero;
        }

        Debug.Log("[GuestReconcile] RoundReset — eski takip hedefleri temizlendi, yeni snapshot bekleniyor.");
    }

    // ===================== HOST: pozisyon yayını (Faz 1) =====================

    void HostUpdate()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        // Bekleyen bir misafir atışı varsa HER FRAME kale alanıyla çakışmayı kontrol edip
        // kilitliyoruz — top kaleye girip sonra dışarı sekse bile gol sayılmalı. Sadece
        // "top durduğunda" bakmak bu geçici girişi kaçırıyordu.
        if (_pendingGuestShotCoin != null && !_pendingGuestShotEnteredGoal)
        {
            GoalZone myGoalLive = GetMyGoalZone();
            if (myGoalLive != null && myGoalLive.OverlapsCoin(_pendingGuestShotCoin))
            {
                _pendingGuestShotEnteredGoal = true;
                Debug.Log($"[GoalArbiter] Latch: {_pendingGuestShotCoin.name} kale alanına girdi (henüz durmadı).");
            }
        }

        bool atRest = GameRulesManager.Instance.AreAllRoundCoinsAtRest();

        if (atRest)
        {
            if (!_wasAtRest)
            {
                // hareket → durgun: nihai pozisyonu garantiye almak için son bir snapshot.
                BroadcastSnapshot(isFinal: true);
                _wasAtRest = true;
                _snapshotTimer = 0f;
            }

            EvaluatePendingGuestGoalArbitration();
            return;
        }

        // Hareket SÜRESİNCE periyodik yayın — kaosun büyümeden erken yakalanması için.
        _wasAtRest = false;
        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= SnapshotIntervalSeconds)
        {
            _snapshotTimer = 0f;
            BroadcastSnapshot(isFinal: false);
        }
    }

    void BroadcastSnapshot(bool isFinal)
    {
        if (_boundRelay == null)
        {
            return;
        }

        EnsureCoinCache();

        Vector3 p1 = PositionOf(_cachedPlayerCoins, 0);
        Vector3 p2 = PositionOf(_cachedPlayerCoins, 1);
        Vector3 p3 = PositionOf(_cachedPlayerCoins, 2);
        Vector3 e1 = PositionOf(_cachedEnemyCoins, 0);
        Vector3 e2 = PositionOf(_cachedEnemyCoins, 1);
        Vector3 e3 = PositionOf(_cachedEnemyCoins, 2);

        _batchSeq++;
        if (isFinal || _batchSeq % 5 == 0)
        {
            // 15Hz'in tamamını değil, her 5. paketi + nihai paketi logla (log şişmesin).
            Debug.Log($"[HostPhysics] SEND batchSeq={_batchSeq} final={isFinal} p1={p1:F2} e1={e1:F2}");
        }

        _boundRelay.TrySendCoinSnapshot(_batchSeq,
            p1.x, p1.z, p2.x, p2.z, p3.x, p3.z,
            e1.x, e1.z, e2.x, e2.z, e3.x, e3.z);
    }

    void EnsureCoinCache()
    {
        // Maç başında coin'ler henüz sahnede değilse null kalabilir — hepsi bulunana kadar
        // her çağrıda tekrar dener, sonra kalıcı olarak önbelleğe alır.
        if (_cachedPlayerCoins == null || HasNullEntry(_cachedPlayerCoins))
        {
            _cachedPlayerCoins = ResolveCoinTransforms(PlayerCoinSenderNames);
        }

        if (_cachedEnemyCoins == null || HasNullEntry(_cachedEnemyCoins))
        {
            _cachedEnemyCoins = ResolveCoinTransforms(EnemyCoinSenderNames);
        }
    }

    static bool HasNullEntry(Transform[] transforms)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    static Transform[] ResolveCoinTransforms(string[] names)
    {
        var result = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            CoinIdentity coin = FindCoinByName(names[i]);
            result[i] = coin != null ? coin.transform : null;
            if (coin == null)
            {
                Debug.LogWarning($"[HostPhysics] Coin bulunamadı (cache): {names[i]}");
            }
        }

        return result;
    }

    static Vector3 PositionOf(Transform[] cache, int index)
    {
        Transform t = cache != null && index < cache.Length ? cache[index] : null;
        return t != null ? t.position : Vector3.zero;
    }

    /// <summary>
    /// RemoteOpponentController.FindRemoteMappedCoin ile AYNI desen: GameObject.Find'ı tek
    /// başına güvenmek yerine önce CoinIdentity tipine göre arar (bu projede isim-bazlı
    /// GameObject.Find'ın tek başına güvenilmez olduğu zaten biliniyordu).
    /// </summary>
    static CoinIdentity FindCoinByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] != null && coins[i].gameObject.name == name)
            {
                return coins[i];
            }
        }

        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<CoinIdentity>() : null;
    }

    // ===================== HOST: misafirin atışları için gol hakemliği (Faz 2) =====================

    void OnHostObservedShotReceived(ShotIntentMessage shot)
    {
        if (shot == null || OnlineMatchSession.Channel == null || !OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        string localName = RemoteOpponentController.FlipTeamToken(shot.coinObjectName ?? string.Empty);
        CoinIdentity identity = FindCoinByName(localName);
        if (identity == null)
        {
            Debug.LogWarning($"[GoalArbiter] Coin bulunamadı: {localName}");
            return;
        }

        _pendingGuestShotCoin = identity;
        _pendingGuestShotSeq = shot.seq;
        _pendingGuestShotEnteredGoal = false;
        Debug.Log($"[GoalArbiter] Pending guest shot coin={identity.name} shotSeq={shot.seq}");
    }

    void OnHostObservedRollbackReceived(ShotRollbackMessage rollback)
    {
        if (rollback == null || _pendingGuestShotSeq != rollback.seq)
        {
            return;
        }

        // Misafirin kendi kapı-kuralı ihlali — host bunu asla ezip gol saymamalı.
        Debug.Log($"[GoalArbiter] Rollback alındı — bekleyen hakemlik golsüz iptal shotSeq={rollback.seq}");
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;
        _pendingGuestShotEnteredGoal = false;
    }

    /// <summary>
    /// scored, HostUpdate()'in her frame kilitlediği "atış boyunca herhangi bir anda kale
    /// alanına girdi mi" bilgisidir — sadece durma anındaki pozisyona bakmak, topun kaleye
    /// girip tekrar dışarı sektiği (geçerli bir gol) durumunu kaçırırdı.
    /// </summary>
    void EvaluatePendingGuestGoalArbitration()
    {
        if (_pendingGuestShotCoin == null)
        {
            return;
        }

        CoinIdentity coin = _pendingGuestShotCoin;
        int shotSeq = _pendingGuestShotSeq;
        bool scored = _pendingGuestShotEnteredGoal;
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;
        _pendingGuestShotEnteredGoal = false;

        Debug.Log($"[GoalArbiter] Evaluate shotSeq={shotSeq} coin={coin.name} scored={scored}");

        if (scored)
        {
            // Host kendi RPC_Goal yayınını InvokeLocal=false yüzünden hiç almaz — kendi
            // "gol yedim" skor/kutlama işlemini burada açıkça tetikliyoruz (bkz. ApplyConcededGoal).
            OnlineMatchGoalSync.ApplyConcededGoal();
            _boundRelay?.TrySendGoal(hostScored: false, shotSeq: shotSeq);
        }

        // scored=false: hiçbir RPC gönderilmez — misafirin bekleme süresinin zaman aşımına
        // uğraması "gol değil" sinyalinin kendisidir (bkz. GameRulesManager.WaitForHostGoalConfirmation).
    }

    // ===================== MİSAFİR: gelen pozisyon snapshot'ını hedefe dönüştür (Faz 1) =====================

    /// <summary>
    /// Burada paraları HAREKET ETTİRMİYORUZ — sadece hedef pozisyonu güncelliyoruz. Gerçek
    /// yumuşak takip FixedUpdate()'te, SmoothDamp ile sürekli olarak yapılıyor.
    /// </summary>
    void OnCoinSnapshotReceived(int batchSeq,
        float p1x, float p1z, float p2x, float p2z, float p3x, float p3z,
        float e1x, float e1z, float e2x, float e2z, float e3x, float e3z)
    {
        if (OnlineMatchSession.Channel != null && OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        if (batchSeq <= _lastAppliedBatchSeq)
        {
            return;
        }

        _lastAppliedBatchSeq = batchSeq;

        if (batchSeq % 5 == 0)
        {
            Debug.Log($"[GuestReconcile] RECV batchSeq={batchSeq} p1=({p1x:F2},{p1z:F2}) e1=({e1x:F2},{e1z:F2})");
        }

        EnsureFollowCache();

        SetFollowTarget(0, p1x, p1z);
        SetFollowTarget(1, p2x, p2z);
        SetFollowTarget(2, p3x, p3z);
        SetFollowTarget(3, e1x, e1z);
        SetFollowTarget(4, e2x, e2z);
        SetFollowTarget(5, e3x, e3z);
    }

    /// <summary>
    /// x/z, gönderenin (host'un) kendi yerel koordinatıdır — aynalanmış tahtalar yüzünden Z
    /// ekseni tahta merkezine göre yansıtılarak bizim yerel koordinatımıza çevrilir. X ekseni
    /// (ShotIntentMessage.dirX ile aynı sözleşme) değişmeden kalır.
    /// </summary>
    void SetFollowTarget(int slot, float x, float z)
    {
        float mirroredZ = (2f * GetMirrorCenterZ()) - z;
        _followTargets[slot] = new Vector3(x, 0f, mirroredZ);
        _followHasTarget[slot] = true;
    }

    /// <summary>
    /// Tahta dünya-orijininde ortalanmış DEĞİL (ör. Z hep ~1.4-2.7 aralığında) — bu yüzden
    /// basit "-z" negatif çevirme paraları tahtanın tamamen dışına ışınlıyordu. Doğrusu,
    /// tahtanın GERÇEK Z merkezine göre yansıtmak: kendi kale alanımla rakip kale alanımın
    /// Z ortalaması — bu değer host ve misafirde AYNI sayı çıkar (toplama değişmeli olduğu
    /// için), tek taraf hesaplasa da tutarlı.
    /// </summary>
    float GetMirrorCenterZ()
    {
        if (_mirrorCenterZ.HasValue)
        {
            return _mirrorCenterZ.Value;
        }

        GoalZone myGoal = GoalZone.FindPlayerGoalArea();
        GoalZone enemyGoal = GoalZone.FindOpponentGoalArea();
        if (myGoal == null || enemyGoal == null)
        {
            // Kale alanları henüz sahnede değilse hesaplamayı ertele (önbelleğe alma).
            return 0f;
        }

        float center = (myGoal.transform.position.z + enemyGoal.transform.position.z) * 0.5f;
        _mirrorCenterZ = center;
        Debug.Log($"[GuestReconcile] Mirror center Z = {center:F2}");
        return center;
    }
}
