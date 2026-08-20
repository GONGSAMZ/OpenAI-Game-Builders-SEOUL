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

    public static SaveService Instance { get; private set; }
    public static SaveGameData Data => EnsureInstance().Current;
    public SaveGameData Current { get; private set; }
    public bool IsAccountSave => !string.IsNullOrEmpty(accountSubject);
    public bool IsRemoteSyncing { get; private set; }
    public bool IsAccountLoading { get; private set; }
    public bool IsReadyForGameplay =>
        !IsAccountLoading &&
        (GamePlatformClient.Instance == null || GamePlatformClient.Instance.IsSessionRestoreComplete);
    public event Action DataChanged;

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
    }

    private void Start()
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null) return;
        client.SessionChanged += OnSessionChanged;
        if (!string.IsNullOrWhiteSpace(client.SessionSubject))
            OnSessionChanged(client.SessionSubject);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.SessionChanged -= OnSessionChanged;
        Instance = null;
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

    public void CommitDay(int day, int settledMoney, int sold, int customers, int revenue, int netProfit)
    {
        Current.run.nextDay = Mathf.Max(1, day + 1);
        Current.run.money = settledMoney;
        LifetimeStatsData stats = Current.account.lifetimeStats;
        stats.totalSales += Mathf.Max(0, sold);
        stats.totalCustomers += Mathf.Max(0, customers);
        stats.totalRevenue += Mathf.Max(0, revenue);
        stats.bestDailyProfit = Mathf.Max(stats.bestDailyProfit, netProfit);
        AchievementCatalog.Evaluate(Current);
        Persist("하루 마감");
    }

    public void ResetRunProgress(Action<bool, string> onComplete)
    {
        SaveGameData beforeReset = SaveDataFactory.Clone(Current);
        SaveDataFactory.ResetRun(Current);
        PersistLocalOnly();

        if (!IsAccountSave)
        {
            DataChanged?.Invoke();
            onComplete?.Invoke(true, string.Empty);
            return;
        }

        FlushRemoteNow((success, message) =>
        {
            bool serverConflictApplied = !success && message.StartsWith("다른 기기", StringComparison.Ordinal);
            if (!success && !serverConflictApplied)
            {
                Current = beforeReset;
                PersistLocalOnly();
                DataChanged?.Invoke();
            }
            onComplete?.Invoke(success, message);
        });
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
                story.lastTalkDay = legacy.lastTalkDay;
                story.specialOrderDueDay = legacy.specialOrderDueDay;
                story.retryAvailableDay = legacy.retryAvailableDay;
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

    private void Persist(string reason)
    {
        PersistLocalOnly();
        DataChanged?.Invoke();
        if (!IsAccountSave) return;
        remoteDirty = true;
        if (remoteSyncRoutine == null)
            remoteSyncRoutine = StartCoroutine(RemoteSyncLoop());
        Debug.Log($"[저장] {reason} 로컬 저장 완료");
    }

    private void PersistLocalOnly()
    {
        Current.updatedAt = DateTime.UtcNow.ToString("O");
        SaveDataFactory.Normalize(Current);
        localStore.Save(currentScope, Current);
    }

    private IEnumerator RemoteSyncLoop()
    {
        yield return new WaitForSecondsRealtime(1f);
        while (remoteDirty && IsAccountSave)
        {
            bool finished = false;
            bool success = false;
            yield return PutRemote((ok, _) => { success = ok; finished = true; });
            if (finished && success) remoteDirty = false;
            if (remoteDirty) yield return new WaitForSecondsRealtime(RemoteRetrySeconds);
        }
        remoteSyncRoutine = null;
    }

    private void FlushRemoteNow(Action<bool, string> onComplete)
    {
        if (remoteSyncRoutine != null)
        {
            StopCoroutine(remoteSyncRoutine);
            remoteSyncRoutine = null;
        }
        StartCoroutine(PutRemote(onComplete));
    }

    private IEnumerator PutRemote(Action<bool, string> onComplete)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null || !client.IsLoggedIn)
        {
            onComplete?.Invoke(false, "서버에 연결할 수 없습니다. 로그인 상태를 확인해 주세요.");
            yield break;
        }

        IsRemoteSyncing = true;
        string body = JsonUtility.ToJson(new SavePutRequest
        {
            expectedRevision = Current.revision,
            profile = Current
        });
        bool success = false;
        string message = string.Empty;
        yield return client.PutSaveProfile(body,
            json =>
            {
                RemoteSaveEnvelope envelope = JsonUtility.FromJson<RemoteSaveEnvelope>(json);
                if (envelope?.profile == null) return;
                Current = envelope.profile;
                SaveDataFactory.Normalize(Current);
                PersistLocalOnly();
                success = true;
            },
            (status, errorJson) =>
            {
                message = status == 409
                    ? "다른 기기의 최신 저장을 불러왔습니다. 다시 확인해 주세요."
                    : "서버 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.";
                if (status != 409) return;
                RemoteSaveEnvelope conflict = JsonUtility.FromJson<RemoteSaveEnvelope>(errorJson);
                if (conflict?.profile == null) return;
                Current = conflict.profile;
                SaveDataFactory.Normalize(Current);
                PersistLocalOnly();
                remoteDirty = false;
                DataChanged?.Invoke();
            });
        IsRemoteSyncing = false;
        onComplete?.Invoke(success, message);
    }

    private void OnSessionChanged(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            accountSubject = null;
            currentScope = GuestScope;
            Current = LoadOrCreate(currentScope, true);
            DataChanged?.Invoke();
            return;
        }
        StartCoroutine(LoadAccount(subject));
    }

    private IEnumerator LoadAccount(string subject)
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        if (client == null) yield break;
        IsAccountLoading = true;
        string accountScope = "account_" + ScopeHash(subject);
        string json = null;
        bool failed = false;
        yield return client.GetSaveProfile(value => json = value, (_, __) => failed = true);
        if (failed || string.IsNullOrWhiteSpace(json))
        {
            if (localStore.TryLoad(accountScope, out SaveGameData cached))
            {
                accountSubject = subject;
                currentScope = accountScope;
                Current = cached;
                DataChanged?.Invoke();
            }
            IsAccountLoading = false;
            yield break;
        }

        RemoteSaveEnvelope envelope = JsonUtility.FromJson<RemoteSaveEnvelope>(json);
        if (envelope?.profile != null)
        {
            localStore.Backup(GuestScope, "guest_backup");
            Current = envelope.profile;
            SaveDataFactory.Normalize(Current);
            accountSubject = subject;
            currentScope = accountScope;
            PersistLocalOnly();
            IsAccountLoading = false;
            DataChanged?.Invoke();
            yield break;
        }

        SaveGameData guest = SaveDataFactory.Clone(Current);
        guest.revision = 0;
        accountSubject = subject;
        currentScope = accountScope;
        Current = guest;
        PersistLocalOnly();
        bool uploaded = false;
        yield return PutRemote((success, _) => uploaded = success);
        if (!uploaded)
        {
            accountSubject = null;
            currentScope = GuestScope;
            Current = LoadOrCreate(GuestScope, true);
        }
        IsAccountLoading = false;
        DataChanged?.Invoke();
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
