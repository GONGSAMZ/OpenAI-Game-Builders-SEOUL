using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveService : MonoBehaviour
{
    private const string GuestScope = "guest";
    private const string LegacyStoryKey = "customer_story_jeonghyun_v1";
    private const string LegacyMetCustomerKey = "collection_met_customers_v1";
    private const float RemoteRetrySeconds = 5f;

    private ILocalSaveStore localStore;
    private string currentScope = GuestScope;
    private string accountSubject;
    private bool remoteDirty;
    private Coroutine remoteSyncRoutine;
    private int accountLoadGeneration;
    // 원격 요청 시작 뒤에도 새 변경이 생겼는지 식별하는 로컬 변경 번호다.
    private int localMutationVersion;
    private int pendingSettlementDay = -1;
    private string pendingSettlementIdempotencyKey;
    private GamePlatformClient platformClient;

    public static SaveService Instance { get; private set; }
    public static SaveGameData Data => EnsureInstance().Current;
    public SaveGameData Current { get; private set; }
    public bool IsAccountSave => !string.IsNullOrEmpty(accountSubject);
    public bool IsRemoteSyncing { get; private set; }
    public bool IsAccountLoading { get; private set; }
    public bool IsReadyForGameplay =>
        !IsAccountLoading &&
        (GamePlatformClient.Instance == null || GamePlatformClient.Instance.IsSessionRestoreComplete);
    public GameStoreCatalogData GameStoreCatalog { get; private set; }
    public GameStoreStateData GameStoreState { get; private set; }
    public event Action DataChanged;
    public event Action GameStoreChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntimeService()
    {
        EnsureInstance();
    }

    private static SaveService EnsureInstance()
    {
        if (Instance != null) return Instance;
        GameObject root = new("@SaveService");
        return root.AddComponent<SaveService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        localStore = new PlayerPrefsLocalSaveStore();
        Current = LoadOrCreate(GuestScope, true);
        ApplyRuntimeSettings();
        GamePlatformClient.InstanceChanged += BindPlatformClient;
        BindPlatformClient(GamePlatformClient.Instance);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        GamePlatformClient.InstanceChanged -= BindPlatformClient;
        BindPlatformClient(null);
        Instance = null;
    }

    private void BindPlatformClient(GamePlatformClient client)
    {
        if (platformClient == client) return;
        if (platformClient != null)
            platformClient.SessionChanged -= OnSessionChanged;
        platformClient = client;
        if (platformClient == null) return;
        platformClient.SessionChanged += OnSessionChanged;
        if (!string.IsNullOrWhiteSpace(platformClient.SessionSubject))
            OnSessionChanged(platformClient.SessionSubject);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && Current != null) PersistLocalOnly();
    }

    private void OnApplicationQuit()
    {
        if (Current != null) PersistLocalOnly();
    }

    public void UnlockFilling(FillingType filling)
    {
        string id = SaveIds.Filling(filling);
        if (Current.run.unlockedFillingIds.Contains(id)) return;
        Current.run.unlockedFillingIds.Add(id);
        Persist("재료 해금");
    }

    public bool IsFillingUnlocked(FillingType filling) =>
        Current.run.unlockedFillingIds.Contains(SaveIds.Filling(filling));

    public bool IsFillingSelected(FillingType filling) =>
        Current.run.selectedFillingIds.Contains(SaveIds.Filling(filling));

    public CustomerStoryRunState GetCustomerStoryRunState(CustomerType customerType) =>
        SaveDataFactory.FindOrCreateCustomerStoryState(Current, customerType);

    public void AddGameplayItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("아이템 ID가 필요합니다.", nameof(itemId));
        if (Current.run.ownedGameplayItemIds.Contains(itemId)) return;
        Current.run.ownedGameplayItemIds.Add(itemId);
        Persist("게임 아이템 획득");
    }

    public CustomerProgressData GetCustomer(CustomerType customerType)
    {
        string id = SaveIds.Customer(customerType);
        CustomerProgressData progress = Current.account.customers.Find(value => value.customerId == id);
        if (progress != null) return progress;
        progress = new CustomerProgressData { customerId = id };
        Current.account.customers.Add(progress);
        return progress;
    }

    public bool HasMet(CustomerType customerType) => GetCustomer(customerType).met;

    public void MarkCustomerMet(CustomerType customerType)
    {
        CustomerProgressData progress = GetCustomer(customerType);
        progress.met = true;
        progress.visitCount++;
        AchievementCatalog.Evaluate(Current);
        Persist("손님 조우");
    }

    public void SaveStoryProgress()
    {
        AchievementCatalog.Evaluate(Current);
        Persist("손님 이야기");
    }

    public void SetMasterVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        if (Mathf.Approximately(Current.settings.masterVolume, normalized)) return;
        Current.settings.masterVolume = normalized;
        AudioListener.volume = normalized;
        Persist("전체 음량 설정");
    }

    public void SetKeyboardHintsEnabled(bool enabled)
    {
        if (Current.settings.keyboardHintsEnabled == enabled) return;
        Current.settings.keyboardHintsEnabled = enabled;
        Persist("키보드 안내 설정");
    }

    public void MarkTutorialCompleted()
    {
        if (Current.settings.tutorialCompleted) return;
        Current.settings.tutorialCompleted = true;
        Persist("튜토리얼 완료");
    }

    public void DiscoverSoul(FillingType filling, QualityStatus bake, CustomerType? linkedCustomer = null)
    {
        if (bake != QualityStatus.soft && bake != QualityStatus.perfect && bake != QualityStatus.crisp)
            return;
        string soulId = SaveIds.Soul(filling, bake);
        SoulDiscoveryData existing = Current.account.discoveredSouls.Find(value => value.soulId == soulId);
        string customerId = linkedCustomer.HasValue ? SaveIds.Customer(linkedCustomer.Value) : string.Empty;
        if (existing == null)
        {
            Current.account.discoveredSouls.Add(new SoulDiscoveryData
            {
                soulId = soulId,
                fillingId = SaveIds.Filling(filling),
                bakeStateId = SaveIds.Bake(bake),
                firstDiscoveredDay = Managers.Game.Day,
                linkedCustomerId = customerId
            });
        }
        else if (string.IsNullOrEmpty(existing.linkedCustomerId) && !string.IsNullOrEmpty(customerId))
        {
            existing.linkedCustomerId = customerId;
        }
        AchievementCatalog.Evaluate(Current);
        Persist("영혼 발견");
    }

    public void RefreshGameStore(Action<bool, string> onComplete = null)
    {
        StartCoroutine(RefreshGameStoreRoutine(onComplete));
    }

    public void PurchaseGameStoreProduct(string productId, Action<bool, string> onComplete)
    {
        if (!IsAccountSave || GamePlatformClient.Instance?.IsLoggedIn != true)
        {
            onComplete?.Invoke(false, "일반 상점 구매는 로그인 후 이용할 수 있습니다.");
            return;
        }
        if (string.IsNullOrWhiteSpace(productId))
        {
            onComplete?.Invoke(false, "구매할 상품 정보가 없습니다.");
            return;
        }
        StartCoroutine(PurchaseGameStoreProductRoutine(productId, onComplete));
    }

    public void SettleDay(
        int day,
        int revenue,
        int ingredientCost,
        int sold,
        int customers,
        Action<bool, string> onComplete)
    {
        if (!IsAccountSave)
        {
            Current.run.nextDay = Mathf.Max(1, day + 1);
            Current.run.money += Mathf.Max(0, revenue) - Mathf.Max(0, ingredientCost);
            Current.run.selectedFillingIds.Clear();
            Current.run.queuedDayEffects.RemoveAll(effect => effect == null || effect.targetDay <= day);
            LifetimeStatsData stats = Current.account.lifetimeStats;
            stats.totalSales += Mathf.Max(0, sold);
            stats.totalCustomers += Mathf.Max(0, customers);
            stats.totalRevenue += Mathf.Max(0, revenue);
            stats.bestDailyProfit = Mathf.Max(stats.bestDailyProfit, revenue - ingredientCost);
            AchievementCatalog.Evaluate(Current);
            Managers.Game.Money = Current.run.money;
            Persist("하루 마감");
            onComplete?.Invoke(true, string.Empty);
            return;
        }
        StartCoroutine(SettleDayRoutine(day, revenue, ingredientCost, sold, customers, onComplete));
    }

    public void ResetRunProgress(Action<bool, string> onComplete)
    {
        if (!IsAccountSave)
        {
            SaveDataFactory.ResetRun(Current);
            PersistLocalOnly();
            Managers.Game.Money = Current.run.money;
            DataChanged?.Invoke();
            onComplete?.Invoke(true, string.Empty);
            return;
        }
        StartCoroutine(ResetRunProgressRoutine(onComplete));
    }

    private IEnumerator RefreshGameStoreRoutine(Action<bool, string> onComplete)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null)
        {
            onComplete?.Invoke(false, "게임 서버 클라이언트를 찾을 수 없습니다.");
            yield break;
        }

        string catalogJson = null;
        string failure = string.Empty;
        yield return client.GetGameStoreCatalog(
            json => catalogJson = json,
            (_, body) => failure = ReadApiError(body, "일반 상점 상품을 불러오지 못했습니다."));
        if (string.IsNullOrWhiteSpace(catalogJson))
        {
            onComplete?.Invoke(false, failure);
            yield break;
        }
        GameStoreCatalog = JsonUtility.FromJson<GameStoreCatalogData>(catalogJson);

        if (!IsAccountSave || !client.IsLoggedIn)
        {
            GameStoreState = CreateReadOnlyGameStoreState();
            GameStoreChanged?.Invoke();
            onComplete?.Invoke(true, string.Empty);
            yield break;
        }

        string stateJson = null;
        failure = string.Empty;
        yield return client.GetGameStoreState(
            json => stateJson = json,
            (_, body) => failure = ReadApiError(body, "일반 상점 보유 정보를 불러오지 못했습니다."));
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            onComplete?.Invoke(false, failure);
            yield break;
        }
        ApplyGameStoreState(JsonUtility.FromJson<GameStoreStateData>(stateJson));
        onComplete?.Invoke(true, string.Empty);
    }

    private IEnumerator PurchaseGameStoreProductRoutine(string productId, Action<bool, string> onComplete)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        string json = null;
        long failureStatus = 0;
        string failureBody = string.Empty;
        yield return client.PurchaseGameStoreProduct(
            productId,
            Current.revision,
            Guid.NewGuid().ToString(),
            value => json = value,
            (status, body) =>
            {
                failureStatus = status;
                failureBody = body;
            });

        if (string.IsNullOrWhiteSpace(json))
        {
            ApplyConflictProfile(failureStatus, failureBody);
            onComplete?.Invoke(false, ReadApiError(failureBody, "상품을 구매하지 못했습니다."));
            yield break;
        }

        GameStoreMutationEnvelope envelope = JsonUtility.FromJson<GameStoreMutationEnvelope>(json);
        if (envelope?.profile == null || envelope.store == null)
        {
            onComplete?.Invoke(false, "구매 결과 형식이 올바르지 않습니다.");
            yield break;
        }
        ApplyAuthoritativeProfile(envelope.profile);
        GameStoreState = envelope.store;
        GameStoreChanged?.Invoke();
        onComplete?.Invoke(true, string.Empty);
    }

    private IEnumerator SettleDayRoutine(
        int day,
        int revenue,
        int ingredientCost,
        int sold,
        int customers,
        Action<bool, string> onComplete)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null || !client.IsLoggedIn)
        {
            onComplete?.Invoke(false, "로그인 세션이 없어 영업일을 정산할 수 없습니다.");
            yield break;
        }

        string json = null;
        long failureStatus = 0;
        string failureBody = string.Empty;
        yield return client.SettleGameDay(
            day,
            Mathf.Max(0, revenue),
            Mathf.Max(0, ingredientCost),
            Mathf.Max(0, sold),
            Mathf.Max(0, customers),
            Current.revision,
            GetSettlementIdempotencyKey(day),
            value => json = value,
            (status, body) =>
            {
                failureStatus = status;
                failureBody = body;
            });

        if (string.IsNullOrWhiteSpace(json))
        {
            ApplyConflictProfile(failureStatus, failureBody);
            onComplete?.Invoke(false, ReadApiError(failureBody, "영업일 정산에 실패했습니다."));
            yield break;
        }

        GameRunMutationEnvelope envelope = JsonUtility.FromJson<GameRunMutationEnvelope>(json);
        if (envelope?.profile == null)
        {
            onComplete?.Invoke(false, "정산 결과 형식이 올바르지 않습니다.");
            yield break;
        }
        ApplyAuthoritativeProfile(envelope.profile);
        pendingSettlementDay = -1;
        pendingSettlementIdempotencyKey = null;
        AchievementCatalog.Evaluate(Current);
        Managers.Game.Money = Current.run.money;
        Persist("하루 마감 업적 동기화");
        onComplete?.Invoke(true, string.Empty);
    }

    private string GetSettlementIdempotencyKey(int day)
    {
        if (pendingSettlementDay == day && !string.IsNullOrWhiteSpace(pendingSettlementIdempotencyKey))
            return pendingSettlementIdempotencyKey;
        pendingSettlementDay = day;
        pendingSettlementIdempotencyKey = Guid.NewGuid().ToString();
        return pendingSettlementIdempotencyKey;
    }

    private IEnumerator ResetRunProgressRoutine(Action<bool, string> onComplete)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null || !client.IsLoggedIn)
        {
            onComplete?.Invoke(false, "로그인 세션이 없어 진행을 초기화할 수 없습니다.");
            yield break;
        }

        string json = null;
        long failureStatus = 0;
        string failureBody = string.Empty;
        yield return client.ResetGameRun(
            Current.revision,
            Guid.NewGuid().ToString(),
            value => json = value,
            (status, body) =>
            {
                failureStatus = status;
                failureBody = body;
            });
        if (string.IsNullOrWhiteSpace(json))
        {
            ApplyConflictProfile(failureStatus, failureBody);
            onComplete?.Invoke(false, ReadApiError(failureBody, "진행을 초기화하지 못했습니다."));
            yield break;
        }

        GameStoreMutationEnvelope envelope = JsonUtility.FromJson<GameStoreMutationEnvelope>(json);
        if (envelope?.profile == null)
        {
            onComplete?.Invoke(false, "초기화 결과 형식이 올바르지 않습니다.");
            yield break;
        }
        ApplyAuthoritativeProfile(envelope.profile);
        GameStoreState = envelope.store;
        GameStoreChanged?.Invoke();
        onComplete?.Invoke(true, string.Empty);
    }

    private void ApplyAuthoritativeProfile(SaveGameData profile)
    {
        Current = SaveDataFactory.MergeAfterRemoteConflict(profile, Current);
        SaveDataFactory.Normalize(Current);
        PersistLocalOnly();
        ApplyRuntimeSettings();
        Managers.Game.Money = Current.run.money;
        DataChanged?.Invoke();
    }

    private void ApplyGameStoreState(GameStoreStateData state)
    {
        if (state == null) return;
        Current.revision = state.revision;
        Current.run.money = state.money;
        Current.run.unlockedFillingIds = new List<string>(state.unlockedFillingIds ?? Array.Empty<string>());
        Current.run.selectedFillingIds = new List<string>(state.selectedFillingIds ?? Array.Empty<string>());
        Current.run.ownedGameplayItemIds = new List<string>(state.ownedGameplayItemIds ?? Array.Empty<string>());
        Current.run.queuedDayEffects = new List<QueuedDayEffectData>(
            state.queuedDayEffects ?? Array.Empty<QueuedDayEffectData>());
        SaveDataFactory.Normalize(Current);
        PersistLocalOnly();
        Managers.Game.Money = Current.run.money;
        GameStoreState = state;
        DataChanged?.Invoke();
        GameStoreChanged?.Invoke();
    }

    private GameStoreStateData CreateReadOnlyGameStoreState()
    {
        List<GameStoreProductStateData> states = new();
        foreach (GameStoreProductData product in GameStoreCatalog?.products ?? Array.Empty<GameStoreProductData>())
        {
            bool owned = product.effect?.code == "select-filling"
                ? Current.run.selectedFillingIds.Contains(product.effect.fillingId)
                : product.ownership == "run-permanent"
                    ? Current.run.ownedGameplayItemIds.Contains(product.productId)
                    : Current.run.queuedDayEffects.Exists(value =>
                        value != null && value.productId == product.productId &&
                        value.targetDay == Current.run.nextDay);
            states.Add(new GameStoreProductStateData
            {
                productId = product.productId,
                status = product.availability != "available"
                    ? "locked"
                    : owned
                        ? product.effect?.code == "select-filling" ? "selected" : "owned"
                        : "login-required"
            });
        }
        return new GameStoreStateData
        {
            revision = Current.revision,
            money = Current.run.money,
            unlockedFillingIds = Current.run.unlockedFillingIds.ToArray(),
            selectedFillingIds = Current.run.selectedFillingIds.ToArray(),
            ownedGameplayItemIds = Current.run.ownedGameplayItemIds.ToArray(),
            queuedDayEffects = Current.run.queuedDayEffects.ToArray(),
            products = states.ToArray()
        };
    }

    private void ApplyConflictProfile(long status, string body)
    {
        if (status != 409 || string.IsNullOrWhiteSpace(body)) return;
        ApiErrorEnvelope conflict = JsonUtility.FromJson<ApiErrorEnvelope>(body);
        if (conflict?.profile != null)
            ApplyAuthoritativeProfile(conflict.profile);
    }

    private static string ReadApiError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            ApiErrorEnvelope envelope = JsonUtility.FromJson<ApiErrorEnvelope>(body);
            return string.IsNullOrWhiteSpace(envelope?.error?.message)
                ? fallback
                : envelope.error.message;
        }
        catch
        {
            return fallback;
        }
    }

    private SaveGameData LoadOrCreate(string scope, bool migrateLegacy)
    {
        if (localStore.TryLoad(scope, out SaveGameData loaded)) return loaded;
        SaveGameData created = SaveDataFactory.CreateDefault();
        if (migrateLegacy) MigrateLegacy(created);
        localStore.Save(scope, created);
        return created;
    }

    private void MigrateLegacy(SaveGameData data)
    {
        string storyJson = PlayerPrefs.GetString(LegacyStoryKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(storyJson))
        {
            LegacyCustomerStorySaveData legacy = JsonUtility.FromJson<LegacyCustomerStorySaveData>(storyJson);
            if (legacy != null)
            {
                CustomerProgressData story = FindOrCreate(data, CustomerType.JeongHyun);
                CustomerStoryRunState storyRun = FindOrCreateStoryRunState(data, CustomerType.JeongHyun);
                storyRun.lastTalkDay = legacy.lastTalkDay;
                storyRun.nextSpecialOrderDay = legacy.specialOrderDueDay > 0
                    ? legacy.specialOrderDueDay
                    : legacy.retryAvailableDay;
                story.storyCompleted = legacy.storyCompleted;
                if (legacy.completedTopicIndexes != null)
                    foreach (int index in legacy.completedTopicIndexes)
                        if (!story.completedTopicIds.Contains(SaveIds.Topic(index))) story.completedTopicIds.Add(SaveIds.Topic(index));
            }
        }

        foreach (CustomerType type in Enum.GetValues(typeof(CustomerType)))
        {
            if (PlayerPrefs.GetInt(LegacyMetCustomerKey + "_" + (int)type, 0) != 1) continue;
            CustomerProgressData customer = FindOrCreate(data, type);
            customer.met = true;
            customer.visitCount = Mathf.Max(1, customer.visitCount);
        }
        AchievementCatalog.Evaluate(data);
    }

    private static CustomerProgressData FindOrCreate(SaveGameData data, CustomerType type)
    {
        string id = SaveIds.Customer(type);
        CustomerProgressData progress = data.account.customers.Find(value => value.customerId == id);
        if (progress != null) return progress;
        progress = new CustomerProgressData { customerId = id };
        data.account.customers.Add(progress);
        return progress;
    }

    private static CustomerStoryRunState FindOrCreateStoryRunState(SaveGameData data, CustomerType type) =>
        SaveDataFactory.FindOrCreateCustomerStoryState(data, type);

    private void Persist(string reason)
    {
        PersistLocalOnly();
        DataChanged?.Invoke();
        if (!IsAccountSave) return;
        localMutationVersion++;
        remoteDirty = true;
        if (remoteSyncRoutine == null)
            remoteSyncRoutine = StartCoroutine(RemoteSyncLoop());
        Debug.Log($"[저장] {reason} 로컬 저장 완료");
    }

    private void PersistLocalOnly()
    {
        // 스크립트 재컴파일 또는 화면 전환 중 저장 요청이 먼저 들어와도,
        // 특별 주문 성공 처리(그리고 바로 이어지는 컷씬)가 예외로 끊기지 않게 한다.
        if (localStore == null)
            localStore = new PlayerPrefsLocalSaveStore();
        if (Current == null)
            Current = LoadOrCreate(currentScope, true);

        Current.updatedAt = DateTime.UtcNow.ToString("O");
        SaveDataFactory.Normalize(Current);
        localStore.Save(currentScope, Current);
    }

    private void ApplyRuntimeSettings()
    {
        if (Current?.settings == null) return;
        AudioListener.volume = Mathf.Clamp01(Current.settings.masterVolume);
    }

    private IEnumerator RemoteSyncLoop()
    {
        yield return new WaitForSecondsRealtime(1f);
        while (remoteDirty && IsAccountSave)
        {
            bool success = false;
            bool retryImmediately = false;
            yield return PutRemote((ok, retryNow, _) =>
            {
                success = ok;
                retryImmediately = retryNow;
            });
            if (success) remoteDirty = retryImmediately;
            if (remoteDirty)
                yield return new WaitForSecondsRealtime(retryImmediately ? 0.1f : RemoteRetrySeconds);
        }
        remoteSyncRoutine = null;
    }

    private void FlushRemoteNow(
        Action<bool, string> onComplete,
        bool preferLocalRunOnConflict = false)
    {
        if (remoteSyncRoutine != null)
        {
            StopCoroutine(remoteSyncRoutine);
            remoteSyncRoutine = null;
        }
        StartCoroutine(FlushRemoteNowRoutine(onComplete, preferLocalRunOnConflict));
    }

    private IEnumerator FlushRemoteNowRoutine(
        Action<bool, string> onComplete,
        bool preferLocalRunOnConflict)
    {
        while (IsAccountSave)
        {
            bool success = false;
            bool retryImmediately = false;
            string message = string.Empty;
            yield return PutRemote((ok, retryNow, resultMessage) =>
            {
                success = ok;
                retryImmediately = retryNow;
                message = resultMessage;
            }, preferLocalRunOnConflict);

            if (!success)
            {
                onComplete?.Invoke(false, message);
                yield break;
            }

            if (!retryImmediately)
            {
                remoteDirty = false;
                onComplete?.Invoke(true, string.Empty);
                yield break;
            }

            remoteDirty = true;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        onComplete?.Invoke(false, "로그인 계정이 변경되어 저장을 완료하지 못했습니다.");
    }

    private IEnumerator PutRemote(
        Action<bool, bool, string> onComplete,
        bool preferLocalRunOnConflict = false)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null || !client.IsLoggedIn)
        {
            onComplete?.Invoke(false, false, "서버에 연결할 수 없습니다. 로그인 상태를 확인해 주세요.");
            yield break;
        }

        IsRemoteSyncing = true;
        string targetSubject = accountSubject;
        int targetGeneration = accountLoadGeneration;
        int requestMutationVersion = localMutationVersion;
        SaveGameData snapshot = SaveDataFactory.Clone(Current);
        string body = JsonUtility.ToJson(new SavePutRequest
        {
            expectedRevision = snapshot.revision,
            profile = snapshot
        });
        bool success = false;
        bool retryImmediately = false;
        string message = string.Empty;
        yield return client.PutSaveProfile(body,
            json =>
            {
                if (accountSubject != targetSubject || accountLoadGeneration != targetGeneration) return;
                RemoteSaveEnvelope envelope = JsonUtility.FromJson<RemoteSaveEnvelope>(json);
                if (envelope?.profile == null) return;
                bool hasNewerLocalChanges = localMutationVersion != requestMutationVersion;
                if (hasNewerLocalChanges)
                {
                    // 요청 중 바뀐 값은 유지하고, 다음 PUT에서 사용할 서버 revision만 갱신한다.
                    Current.revision = envelope.profile.revision;
                    Current.updatedAt = envelope.profile.updatedAt;
                }
                else
                {
                    Current = envelope.profile;
                }
                SaveDataFactory.Normalize(Current);
                PersistLocalOnly();
                ApplyRuntimeSettings();
                success = true;
                retryImmediately = hasNewerLocalChanges;
            },
            (status, errorJson) =>
            {
                if (accountSubject != targetSubject || accountLoadGeneration != targetGeneration) return;
                message = status == 409
                    ? "다른 기기의 저장과 합친 뒤 다시 저장합니다."
                    : "서버 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.";
                if (status != 409) return;
                RemoteSaveEnvelope conflict = JsonUtility.FromJson<RemoteSaveEnvelope>(errorJson);
                if (conflict?.profile == null) return;
                Current = SaveDataFactory.MergeAfterRemoteConflict(
                    conflict.profile,
                    Current,
                    preferLocalRunOnConflict);
                SaveDataFactory.Normalize(Current);
                PersistLocalOnly();
                ApplyRuntimeSettings();
                localMutationVersion++;
                remoteDirty = true;
                DataChanged?.Invoke();
                success = true;
                retryImmediately = true;
            });
        IsRemoteSyncing = false;
        onComplete?.Invoke(success, retryImmediately, message);
    }

    private void OnSessionChanged(string subject)
    {
        accountLoadGeneration++;
        remoteDirty = false;
        if (remoteSyncRoutine != null)
        {
            StopCoroutine(remoteSyncRoutine);
            remoteSyncRoutine = null;
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            IsAccountLoading = false;
            accountSubject = null;
            currentScope = GuestScope;
            Current = LoadOrCreate(currentScope, true);
            GameStoreState = null;
            ApplyRuntimeSettings();
            DataChanged?.Invoke();
            GameStoreChanged?.Invoke();
            return;
        }
        GameStoreState = null;
        GameStoreChanged?.Invoke();
        StartCoroutine(LoadAccount(subject, accountLoadGeneration));
    }

    private IEnumerator LoadAccount(string subject, int generation)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null) yield break;
        IsAccountLoading = true;
        string accountScope = "account_" + ScopeHash(subject);
        bool hasCached = localStore.TryLoad(accountScope, out SaveGameData cached);
        SaveGameData accountCandidate = hasCached
            ? cached
            : string.IsNullOrWhiteSpace(accountSubject)
                ? SaveDataFactory.Clone(Current)
                : SaveDataFactory.CreateDefault();
        accountCandidate.revision = hasCached ? accountCandidate.revision : 0;
        accountSubject = subject;
        currentScope = accountScope;
        Current = accountCandidate;
        PersistLocalOnly();
        ApplyRuntimeSettings();
        DataChanged?.Invoke();

        string json = null;
        bool failed = false;
        yield return client.GetSaveProfile(value => json = value, (_, __) => failed = true);
        if (generation != accountLoadGeneration || accountSubject != subject)
            yield break;
        if (failed || string.IsNullOrWhiteSpace(json))
        {
            IsAccountLoading = false;
            RefreshGameStore();
            yield break;
        }

        RemoteSaveEnvelope envelope = JsonUtility.FromJson<RemoteSaveEnvelope>(json);
        if (envelope?.profile != null)
        {
            bool requiresSchemaUpgrade = envelope.profile.schemaVersion < SaveDataFactory.CurrentSchemaVersion;
            localStore.Backup(GuestScope, "guest_backup");
            Current = envelope.profile;
            SaveDataFactory.Normalize(Current);
            accountSubject = subject;
            currentScope = accountScope;
            PersistLocalOnly();
            ApplyRuntimeSettings();
            IsAccountLoading = false;
            DataChanged?.Invoke();
            if (requiresSchemaUpgrade)
                Persist("저장 형식 업그레이드");
            RefreshGameStore();
            yield break;
        }

        // 서버에 저장이 없는 새 계정은 익명/다른 계정의 진행을 복사하지 않는다.
        SaveGameData guest = SaveDataFactory.CreateDefault();
        accountSubject = subject;
        currentScope = accountScope;
        Current = guest;
        PersistLocalOnly();
        ApplyRuntimeSettings();
        bool uploaded = false;
        yield return PutRemote((success, _, __) => uploaded = success);
        if (generation != accountLoadGeneration || accountSubject != subject)
            yield break;
        if (!uploaded)
        {
            accountSubject = null;
            currentScope = GuestScope;
            Current = LoadOrCreate(GuestScope, true);
            ApplyRuntimeSettings();
        }
        IsAccountLoading = false;
        DataChanged?.Invoke();
        if (IsAccountSave)
            RefreshGameStore();
    }

    private static string ScopeHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value) { hash ^= character; hash *= 16777619; }
            return hash.ToString("x8");
        }
    }

    [Serializable]
    private sealed class LegacyCustomerStorySaveData
    {
        public int lastTalkDay = -1;
        public List<int> completedTopicIndexes = new();
        public int specialOrderDueDay = -1;
        public int retryAvailableDay = -1;
        public bool storyCompleted;
    }

    [Serializable]
    private sealed class SavePutRequest
    {
        public long expectedRevision;
        public SaveGameData profile;
    }

    [Serializable]
    private sealed class RemoteSaveEnvelope
    {
        public SaveGameData profile;
    }
}
