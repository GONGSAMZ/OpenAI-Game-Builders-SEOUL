using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

[Preserve]
public sealed class GamePlatformClient : MonoBehaviour
{
    private const string PlatformCurrencyItemId = "red-bean-coin";
    private const float InventorySyncIntervalSeconds = 5f;

    [SerializeField] private string apiBaseUrl = "http://localhost:3000";

    public event Action<string> LoginSucceeded;
    public event Action<string> SessionChanged;
    public event Action SessionRestoreCompleted;
    public event Action<string> RequestFailed;
    public event Action StoreStateChanged;
    public event Action PaymentSucceeded;

    private string sessionToken;
    private bool serverSessionAvailable;
    private int sessionGeneration;
    private Coroutine inventorySyncLoop;
    private readonly Dictionary<string, int> inventory = new();

    public int RedBeanCoinBalance => GetItemQuantity(PlatformCurrencyItemId);
    public int TestPointBalance { get; private set; }
    public bool OwnsGoldenPan => GetItemQuantity("golden-pan") > 0;
    public bool IsGoldenPanEquipped { get; private set; }
    public float BakingTimeMultiplier => IsGoldenPanEquipped ? 0.8f : 1f;
    public string AccountSubject => SessionSubject;

    public static GamePlatformClient Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GameBridge_RegisterSessionReceiver(string gameObject, string method);

    [DllImport("__Internal")]
    private static extern void GameBridge_Login(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_Logout(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_OpenShop(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_OpenNicePay(string productId, string gameObject, string successMethod, string errorMethod);
#endif

    public bool IsLoggedIn => serverSessionAvailable || !string.IsNullOrWhiteSpace(sessionToken);
    public string SessionSubject { get; private set; }
    public bool IsSessionRestoreComplete { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntimeClient()
    {
        if (Instance != null) return;
        var gameObject = new GameObject("@GamePlatformClient");
        gameObject.AddComponent<GamePlatformClient>();
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

#if UNITY_WEBGL && !UNITY_EDITOR
        if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri gameUri))
            apiBaseUrl = gameUri.GetLeftPart(UriPartial.Authority);
        GameBridge_RegisterSessionReceiver(gameObject.name, nameof(OnExternalSessionChanged));
#endif
    }

    private void Start()
    {
        StartInventorySync();
        StartCoroutine(RestoreSession());
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
    }

    public void Configure(string baseUrl)
    {
        apiBaseUrl = baseUrl.TrimEnd('/');
    }

    public void LoginWithHive()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameBridge_Login(gameObject.name, nameof(OnHiveLoginSuccess), nameof(OnBridgeError));
#else
        OnBridgeError("Hive 팝업 로그인은 WebGL 빌드에서 테스트하세요.");
#endif
    }

    public void Logout()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameBridge_Logout(gameObject.name, nameof(OnHiveLogoutSuccess), nameof(OnBridgeError));
#else
        sessionToken = null;
        serverSessionAvailable = false;
        SetSessionSubject(null);
        ClearStoreState();
#endif
    }

    public void OpenHiveWebShop()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameBridge_OpenShop(gameObject.name, nameof(OnHiveShopClosed), nameof(OnBridgeError));
#else
        OnBridgeError("HIVE 웹 상점은 WebGL 빌드에서 테스트하세요.");
#endif
    }

    public IEnumerator GetSession(Action<string> onSuccess)
    {
        yield return SendJson("GET", "/api/v1/auth/session", null, onSuccess);
    }

    public IEnumerator GetPublicConfig(Action<string> onSuccess)
    {
        yield return SendJson("GET", "/api/v1/config/public", null, onSuccess);
    }

    public IEnumerator CreateNpcReaction(
        string situation,
        string playerAction,
        Action<string> onSuccess)
    {
        var payload = JsonUtility.ToJson(new NpcReactionRequest
        {
            situation = situation,
            playerAction = playerAction,
            locale = "ko"
        });
        yield return SendJson("POST", "/api/v1/ai/npc-reaction", payload, onSuccess);
    }

    public IEnumerator GetStoreCatalog(Action<string> onSuccess)
    {
        yield return SendJson("GET", "/api/v1/store/catalog", null, onSuccess);
    }

    public IEnumerator GetInventory(Action<string> onSuccess)
    {
        int requestGeneration = sessionGeneration;
        yield return SendJson("GET", "/api/v1/store/me", null, json =>
        {
            if (requestGeneration != sessionGeneration || !IsLoggedIn) return;
            ApplyStoreState(json);
            onSuccess?.Invoke(json);
        });
    }

    public IEnumerator GetPurchaseHistory(string cursor, Action<string> onSuccess)
    {
        string path = "/api/v1/store/purchases?limit=20";
        if (!string.IsNullOrWhiteSpace(cursor))
            path += "&cursor=" + UnityWebRequest.EscapeURL(cursor);
        yield return SendJson("GET", path, null, onSuccess);
    }

    public IEnumerator GetSaveProfile(Action<string> onSuccess, Action<long, string> onFailure)
    {
        yield return SendJsonDetailed("GET", "/api/v1/save/profile", null, onSuccess, onFailure);
    }

    public IEnumerator PutSaveProfile(string payload, Action<string> onSuccess, Action<long, string> onFailure)
    {
        yield return SendJsonDetailed("PUT", "/api/v1/save/profile", payload, onSuccess, onFailure);
    }

    public void OpenNicePayTestCheckout(string productId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameBridge_OpenNicePay(productId, gameObject.name, nameof(OnNicePayPaymentCompleted), nameof(OnBridgeError));
#else
        OnBridgeError("NICEPAY 테스트 결제는 WebGL 빌드에서 테스트하세요.");
#endif
    }

    public void SyncInventoryNow()
    {
        if (!IsLoggedIn) return;
        StartCoroutine(GetInventory(null));
    }

    public int GetItemQuantity(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && inventory.TryGetValue(itemId, out int quantity)
            ? quantity
            : 0;
    }

    public IEnumerator SetMoldSkin(bool equip, Action<string> onSuccess)
    {
        string payload = equip
            ? "{\"itemId\":\"golden-pan\"}"
            : "{\"itemId\":null}";
        yield return SendJson("PUT", "/api/v1/store/equipment/mold", payload, json =>
        {
            ApplyStoreState(json);
            onSuccess?.Invoke(json);
        });
    }

    public IEnumerator CreateMockPurchase(string productId, Action<string> onSuccess)
    {
        var payload = JsonUtility.ToJson(new MockPurchaseRequest
        {
            productId = productId,
            idempotencyKey = Guid.NewGuid().ToString()
        });
        yield return SendJson("POST", "/api/v1/store/mock-purchases", payload, onSuccess);
    }

    [Preserve]
    public void OnHiveLoginSuccess(string token)
    {
        Debug.Log("[Platform] WebGL session login received.");
        sessionGeneration++;
        sessionToken = token;
        serverSessionAvailable = true;
        StartCoroutine(InitializeAuthenticatedState(false));
        LoginSucceeded?.Invoke(token);
    }

    [Preserve]
    public void OnHiveLogoutSuccess(string _)
    {
        Debug.Log("[Platform] WebGL session logout received; clearing account store state.");
        sessionGeneration++;
        sessionToken = null;
        serverSessionAvailable = false;
        SetSessionSubject(null);
        ClearStoreState();
    }

    [Preserve]
    public void OnExternalSessionChanged(string value)
    {
        Debug.Log($"[Platform] External session change received: {value}");
        if (string.Equals(value, "logout", StringComparison.OrdinalIgnoreCase))
        {
            OnHiveLogoutSuccess(string.Empty);
            return;
        }

        if (string.Equals(value, "refresh", StringComparison.OrdinalIgnoreCase))
        {
            sessionGeneration++;
            sessionToken = null;
            StartCoroutine(InitializeAuthenticatedState(false));
            return;
        }

        if (!string.IsNullOrWhiteSpace(value))
            OnHiveLoginSuccess(value);
    }

    [Preserve]
    public void OnHiveShopClosed(string _)
    {
        SyncInventoryNow();
    }

    [Preserve]
    public void OnNicePayPaymentCompleted(string _)
    {
        SyncInventoryNow();
        PaymentSucceeded?.Invoke();
    }

    [Preserve]
    public void OnInventoryUpdated(string json)
    {
        if (!IsLoggedIn) return;
        ApplyStoreState(json);
    }

    private void ApplyStoreState(string json)
    {
        try
        {
            StoreStateEnvelope envelope = JsonUtility.FromJson<StoreStateEnvelope>(json);
            inventory.Clear();
            if (envelope?.inventory != null)
            {
                foreach (InventoryEntry entry in envelope.inventory)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
                        continue;
                    inventory[entry.itemId] = Mathf.Max(0, entry.quantity);
                }
            }

            if (envelope?.equipment != null)
                IsGoldenPanEquipped = envelope.equipment.moldSkin == "golden-pan" && OwnsGoldenPan;
            else if (!OwnsGoldenPan)
                IsGoldenPanEquipped = false;
            if (envelope?.wallet != null)
                TestPointBalance = Mathf.Max(0, envelope.wallet.testPoints);
            StoreStateChanged?.Invoke();
        }
        catch (Exception error)
        {
            OnBridgeError($"플랫폼 재화 동기화에 실패했습니다: {error.Message}");
        }
    }

    [Preserve]
    public void OnBridgeError(string message)
    {
        Debug.LogError(message);
        RequestFailed?.Invoke(message);
    }

    private IEnumerator SendJson(
        string method,
        string path,
        string body,
        Action<string> onSuccess,
        bool reportFailure = true)
    {
        yield return SendJsonDetailed(method, path, body, onSuccess, (status, responseBody) =>
        {
            if (reportFailure) OnBridgeError($"HTTP {status}: {responseBody}");
        });
    }

    private IEnumerator SendJsonDetailed(
        string method,
        string path,
        string body,
        Action<string> onSuccess,
        Action<long, string> onFailure)
    {
        using var request = new UnityWebRequest(apiBaseUrl.TrimEnd('/') + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();

        if (body != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (IsLoggedIn)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
                request.SetRequestHeader("Authorization", "Bearer " + sessionToken);
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            // The server session is authoritative. This also clears account data if a
            // cross-frame logout notification is missed or the session expires remotely.
            if (request.responseCode == 401 && IsLoggedIn)
                OnHiveLogoutSuccess(string.Empty);
            onFailure?.Invoke(request.responseCode, request.downloadHandler.text);
            yield break;
        }

        onSuccess?.Invoke(request.downloadHandler.text);
    }

    private IEnumerator RestoreSession()
    {
        yield return InitializeAuthenticatedState(true);
    }

    private IEnumerator InitializeAuthenticatedState(bool notifyRestoreCompleted)
    {
        int requestGeneration = sessionGeneration;
        bool restored = false;
        string sessionJson = null;
        yield return SendJson(
            "GET",
            "/api/v1/auth/session",
            null,
            json =>
            {
                restored = true;
                sessionJson = json;
            },
            false);

        if (requestGeneration != sessionGeneration)
            yield break;

        serverSessionAvailable = restored;
        if (!restored)
        {
            sessionToken = null;
            SetSessionSubject(null);
        }
        else
        {
            ApplySession(sessionJson);
            SyncInventoryNow();
        }

        if (notifyRestoreCompleted)
        {
            IsSessionRestoreComplete = true;
            SessionRestoreCompleted?.Invoke();
        }
    }

    private void ApplySession(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        SessionEnvelope envelope = JsonUtility.FromJson<SessionEnvelope>(json);
        SetSessionSubject(envelope?.session?.subject);
    }

    private void StartInventorySync()
    {
        if (inventorySyncLoop == null)
            inventorySyncLoop = StartCoroutine(InventorySyncLoop());
    }

    private IEnumerator InventorySyncLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(InventorySyncIntervalSeconds);
            yield return InitializeAuthenticatedState(false);
        }
    }

    private void ClearStoreState()
    {
        inventory.Clear();
        TestPointBalance = 0;
        IsGoldenPanEquipped = false;
        StoreStateChanged?.Invoke();
    }

    private void SetSessionSubject(string subject)
    {
        string normalized = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        if (SessionSubject == normalized) return;
        SessionSubject = normalized;
        SessionChanged?.Invoke(normalized);
    }

    [Serializable]
    private sealed class NpcReactionRequest
    {
        public string situation;
        public string playerAction;
        public string locale;
    }

    [Serializable]
    private sealed class MockPurchaseRequest
    {
        public string productId;
        public string idempotencyKey;
    }

    [Serializable]
    private sealed class StoreStateEnvelope
    {
        public InventoryEntry[] inventory;
        public StoreEquipment equipment;
        public StoreWallet wallet;
    }

    [Serializable]
    private sealed class InventoryEntry
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    private sealed class StoreEquipment
    {
        public string moldSkin;
    }

    [Serializable]
    private sealed class StoreWallet
    {
        public int testPoints;
    }

    [Serializable]
    private sealed class SessionEnvelope
    {
        public SessionData session;
    }

    [Serializable]
    private sealed class SessionData
    {
        public string subject;
    }
}
