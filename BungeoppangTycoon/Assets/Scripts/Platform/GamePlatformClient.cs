using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public sealed class GamePlatformClient : MonoBehaviour
{
    private const string PlatformCurrencyItemId = "red-bean-coin";
    private const float InventorySyncIntervalSeconds = 5f;

    [SerializeField] private string apiBaseUrl = "http://localhost:3000";

    public event Action<string> LoginSucceeded;
    public event Action<string> RequestFailed;

    private string sessionToken;
    private bool serverSessionAvailable;
    private Coroutine inventorySyncLoop;
    private int pendingPlatformCurrency;
    private int appliedPlatformCurrency;

    public static GamePlatformClient Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GameBridge_Login(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_Logout(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_OpenShop(string gameObject, string errorMethod);
#endif

    public bool IsLoggedIn => serverSessionAvailable || !string.IsNullOrWhiteSpace(sessionToken);

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
        SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri gameUri))
            apiBaseUrl = gameUri.GetLeftPart(UriPartial.Authority);
#endif
    }

    private void Start()
    {
        StartCoroutine(RestoreSession());
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
#endif
    }

    public void OpenHiveWebShop()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameBridge_OpenShop(gameObject.name, nameof(OnBridgeError));
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
        yield return SendJson("GET", "/api/v1/store/me", null, onSuccess);
    }

    public void SyncInventoryNow()
    {
        if (!IsLoggedIn) return;
        StartCoroutine(GetInventory(OnInventoryUpdated));
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

    public void OnHiveLoginSuccess(string token)
    {
        sessionToken = token;
        serverSessionAvailable = true;
        StartInventorySync();
        SyncInventoryNow();
        LoginSucceeded?.Invoke(token);
    }

    public void OnHiveLogoutSuccess(string _)
    {
        sessionToken = null;
        serverSessionAvailable = false;
        StopInventorySync();
    }

    public void OnInventoryUpdated(string json)
    {
        try
        {
            InventoryEnvelope envelope = JsonUtility.FromJson<InventoryEnvelope>(json);
            int nextCurrency = 0;
            if (envelope?.inventory != null)
            {
                foreach (InventoryEntry entry in envelope.inventory)
                {
                    if (entry != null && entry.itemId == PlatformCurrencyItemId)
                    {
                        nextCurrency = Mathf.Max(0, entry.quantity);
                        break;
                    }
                }
            }

            pendingPlatformCurrency = nextCurrency;
            ApplyPlatformCurrencyWhenReady();
        }
        catch (Exception error)
        {
            OnBridgeError($"플랫폼 재화 동기화에 실패했습니다: {error.Message}");
        }
    }

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
        using var request = new UnityWebRequest(apiBaseUrl.TrimEnd('/') + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();

        if (body != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (IsLoggedIn)
        {
            request.SetRequestHeader("Authorization", "Bearer " + sessionToken);
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (reportFailure)
                OnBridgeError($"HTTP {request.responseCode}: {request.downloadHandler.text}");
            yield break;
        }

        onSuccess?.Invoke(request.downloadHandler.text);
    }

    private IEnumerator RestoreSession()
    {
        bool restored = false;
        yield return SendJson(
            "GET",
            "/api/v1/auth/session",
            null,
            _ => restored = true,
            false);

        serverSessionAvailable = restored;
        if (!restored) yield break;
        StartInventorySync();
        SyncInventoryNow();
    }

    private void StartInventorySync()
    {
        if (inventorySyncLoop == null)
            inventorySyncLoop = StartCoroutine(InventorySyncLoop());
    }

    private void StopInventorySync()
    {
        if (inventorySyncLoop == null) return;
        StopCoroutine(inventorySyncLoop);
        inventorySyncLoop = null;
    }

    private IEnumerator InventorySyncLoop()
    {
        while (IsLoggedIn)
        {
            yield return new WaitForSecondsRealtime(InventorySyncIntervalSeconds);
            if (IsLoggedIn) SyncInventoryNow();
        }
        inventorySyncLoop = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene.name != "GameScene") return;
        appliedPlatformCurrency = 0;
        StartCoroutine(ApplyAfterGameInitialization());
    }

    private IEnumerator ApplyAfterGameInitialization()
    {
        while (GameObject.Find("@Managers") == null) yield return null;
        yield return null;
        ApplyPlatformCurrencyWhenReady();
    }

    private void ApplyPlatformCurrencyWhenReady()
    {
        if (GameObject.Find("@Managers") == null) return;
        int delta = pendingPlatformCurrency - appliedPlatformCurrency;
        if (delta == 0) return;

        Managers.Game.Money += delta;
        appliedPlatformCurrency = pendingPlatformCurrency;
        Debug.Log($"플랫폼 재화 동기화: {delta:+#;-#;0}, 서버 잔액 {pendingPlatformCurrency}");
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
    private sealed class InventoryEnvelope
    {
        public InventoryEntry[] inventory;
    }

    [Serializable]
    private sealed class InventoryEntry
    {
        public string itemId;
        public int quantity;
    }
}
