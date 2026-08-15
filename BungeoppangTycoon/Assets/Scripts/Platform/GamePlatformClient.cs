using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class GamePlatformClient : MonoBehaviour
{
    [SerializeField] private string apiBaseUrl = "http://localhost:3000";

    public event Action<string> LoginSucceeded;
    public event Action<string> RequestFailed;

    private string sessionToken;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GameBridge_Login(string gameObject, string successMethod, string errorMethod);

    [DllImport("__Internal")]
    private static extern void GameBridge_Logout(string gameObject, string successMethod, string errorMethod);
#endif

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(sessionToken);

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri gameUri))
            apiBaseUrl = gameUri.GetLeftPart(UriPartial.Authority);
#endif
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
#endif
    }

    public IEnumerator GetSession(Action<string> onSuccess)
    {
        yield return SendJson("GET", "/api/v1/auth/session", null, onSuccess);
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
        LoginSucceeded?.Invoke(token);
    }

    public void OnHiveLogoutSuccess(string _)
    {
        sessionToken = null;
    }

    public void OnBridgeError(string message)
    {
        Debug.LogError(message);
        RequestFailed?.Invoke(message);
    }

    private IEnumerator SendJson(string method, string path, string body, Action<string> onSuccess)
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
            OnBridgeError($"HTTP {request.responseCode}: {request.downloadHandler.text}");
            yield break;
        }

        onSuccess?.Invoke(request.downloadHandler.text);
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
}
