using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UI_InAppMarket : UI_Base
{
    private Button closeButton;
    private Button loginButton;
    private Button retryButton;
    private TextMeshProUGUI loginStatusText;
    private TextMeshProUGUI loginButtonText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI emptyStateText;
    private Transform productContent;
    private GameObject productCardTemplate;

    private readonly List<UI_InAppMarketProductCard> cards = new();
    private readonly Dictionary<string, int> inventory = new();

    private GamePlatformClient platformClient;
    private InAppMarketProduct[] products = Array.Empty<InAppMarketProduct>();
    private string catalogMode = "mock";
    private string hiveWebShopUrl;
    private bool wasGameRunning;
    private bool didPauseGame;
    private bool isClosing;

    protected override void Init()
    {
        closeButton = Util.Find<Button>(gameObject, "CloseButton", true);
        loginButton = Util.Find<Button>(gameObject, "LoginButton", true);
        retryButton = Util.Find<Button>(gameObject, "RetryButton", true);
        loginStatusText = Util.Find<TextMeshProUGUI>(gameObject, "LoginStatusText", true);
        loginButtonText = Util.Find<TextMeshProUGUI>(gameObject, "LoginButtonText", true);
        statusText = Util.Find<TextMeshProUGUI>(gameObject, "StatusText", true);
        emptyStateText = Util.Find<TextMeshProUGUI>(gameObject, "EmptyStateText", true);
        productContent = Util.FindObject(gameObject, "ProductContent", true).transform;
        productCardTemplate = Util.FindObject(gameObject, "ProductCardTemplate", true);

        if (!ValidateReferences())
        {
            Debug.LogError("인앱 마켓 프리팹의 필수 UI 연결이 누락됐습니다.");
            enabled = false;
            return;
        }

        wasGameRunning = Managers.Game.isRunning;
        Managers.Game.isRunning = false;
        didPauseGame = true;

        platformClient = FindFirstObjectByType<GamePlatformClient>();
        if (platformClient == null)
        {
            GameObject clientObject = new("@GamePlatformClient");
            platformClient = clientObject.AddComponent<GamePlatformClient>();
            DontDestroyOnLoad(clientObject);
        }

        platformClient.LoginSucceeded += OnLoginSucceeded;
        platformClient.RequestFailed += OnRequestFailed;

        closeButton.onClick.AddListener(Close);
        loginButton.onClick.AddListener(LoginOrRefresh);
        retryButton.onClick.AddListener(RefreshMarket);
        retryButton.gameObject.SetActive(false);

        RefreshLoginState();
        StartCoroutine(LoadInitialData());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void OnDestroy()
    {
        if (platformClient != null)
        {
            platformClient.LoginSucceeded -= OnLoginSucceeded;
            platformClient.RequestFailed -= OnRequestFailed;
        }

        if (didPauseGame)
            Managers.Game.isRunning = wasGameRunning;
    }

    private bool ValidateReferences()
    {
        return closeButton != null &&
            loginButton != null &&
            retryButton != null &&
            loginStatusText != null &&
            loginButtonText != null &&
            statusText != null &&
            emptyStateText != null &&
            productContent != null &&
            productCardTemplate != null;
    }

    private IEnumerator LoadInitialData()
    {
        SetStatus("상점 정보를 불러오는 중입니다…", false);
        yield return platformClient.GetPublicConfig(ParsePublicConfig);
        bool catalogReceived = false;
        yield return platformClient.GetStoreCatalog(json =>
        {
            catalogReceived = true;
            ParseCatalog(json);
        });

        if (!catalogReceived)
        {
            RenderProducts();
            yield break;
        }

        if (platformClient.IsLoggedIn)
        {
            bool inventoryReceived = false;
            yield return platformClient.GetInventory(json =>
            {
                inventoryReceived = true;
                ParseInventory(json);
            });
            if (!inventoryReceived)
            {
                RenderProducts();
                yield break;
            }
        }

        RenderProducts();
        SetStatus(platformClient.IsLoggedIn
            ? "상품을 선택해 주세요."
            : "보유 아이템 확인과 구매를 위해 HIVE 로그인이 필요합니다.", false);
    }

    private void RefreshMarket()
    {
        retryButton.gameObject.SetActive(false);
        StartCoroutine(LoadInitialData());
    }

    private void LoginOrRefresh()
    {
        if (platformClient.IsLoggedIn)
        {
            StartCoroutine(RefreshInventory());
            return;
        }

        SetStatus("HIVE 로그인 창을 여는 중입니다…", false);
        platformClient.LoginWithHive();
    }

    private IEnumerator RefreshInventory()
    {
        SetCardsBusy(true);
        SetStatus("보유 아이템을 확인하는 중입니다…", false);
        bool inventoryReceived = false;
        yield return platformClient.GetInventory(json =>
        {
            inventoryReceived = true;
            ParseInventory(json);
        });
        if (!inventoryReceived)
        {
            SetCardsBusy(false);
            yield break;
        }
        RenderProducts();
        SetStatus("보유 아이템을 새로고침했습니다.", false);
    }

    private void OnLoginSucceeded(string _)
    {
        RefreshLoginState();
        StartCoroutine(RefreshInventory());
    }

    private void OnRequestFailed(string message)
    {
        SetCardsBusy(false);
        SetStatus(string.IsNullOrWhiteSpace(message)
            ? "서버 요청에 실패했습니다."
            : message, true);
    }

    private void ParsePublicConfig(string json)
    {
        InAppMarketPublicConfig config = ParseJson<InAppMarketPublicConfig>(json);
        if (config == null)
            return;

        if (!string.IsNullOrWhiteSpace(config.storeMode))
            catalogMode = config.storeMode;
        hiveWebShopUrl = config.hiveWebShopUrl;
    }

    private void ParseCatalog(string json)
    {
        InAppMarketCatalog catalog = ParseJson<InAppMarketCatalog>(json);
        if (catalog == null)
            return;

        if (!string.IsNullOrWhiteSpace(catalog.mode))
            catalogMode = catalog.mode;
        products = catalog.products ?? Array.Empty<InAppMarketProduct>();
    }

    private void ParseInventory(string json)
    {
        InAppMarketInventoryResponse response = ParseJson<InAppMarketInventoryResponse>(json);
        ApplyInventory(response?.inventory);
    }

    private void ParsePurchase(string json)
    {
        InAppMarketPurchaseResponse response = ParseJson<InAppMarketPurchaseResponse>(json);
        if (response == null)
            return;
        ApplyInventory(response?.inventory);
        RenderProducts();
        SetStatus(response != null && response.duplicate
            ? "이미 처리된 구매입니다. 보유 수량을 다시 확인했습니다."
            : "상품이 계정 보유 아이템에 지급됐습니다.", false);
    }

    private T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("서버가 빈 응답을 반환했습니다.", true);
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("상점 응답을 읽지 못했습니다.", true);
            return null;
        }
    }

    private void ApplyInventory(InAppMarketInventoryEntry[] entries)
    {
        inventory.Clear();
        if (entries == null)
            return;

        foreach (InAppMarketInventoryEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
                continue;
            inventory[entry.itemId] = Mathf.Max(0, entry.quantity);
        }
    }

    private void RenderProducts()
    {
        foreach (UI_InAppMarketProductCard card in cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        cards.Clear();

        bool hasProducts = products != null && products.Length > 0;
        emptyStateText.gameObject.SetActive(!hasProducts);
        if (!hasProducts)
        {
            emptyStateText.text = "표시할 상품이 없습니다.";
            return;
        }

        bool opensWebShop = string.Equals(catalogMode, "hive-web-shop", StringComparison.OrdinalIgnoreCase);
        foreach (InAppMarketProduct product in products)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.id))
                continue;

            GameObject cardObject = Instantiate(productCardTemplate, productContent);
            cardObject.name = $"ProductCard_{product.id}";
            cardObject.SetActive(true);

            UI_InAppMarketProductCard card = cardObject.GetComponent<UI_InAppMarketProductCard>();
            int ownedQuantity = 0;
            if (product.grant != null && !string.IsNullOrWhiteSpace(product.grant.itemId))
                inventory.TryGetValue(product.grant.itemId, out ownedQuantity);

            card.SetData(product, ownedQuantity, platformClient.IsLoggedIn, opensWebShop, Purchase);
            cards.Add(card);
        }

        RefreshLoginState();
        SelectInitialControl();
    }

    private void Purchase(InAppMarketProduct product)
    {
        if (!platformClient.IsLoggedIn)
        {
            SetStatus("먼저 HIVE에 로그인해 주세요.", true);
            EventSystem.current?.SetSelectedGameObject(loginButton.gameObject);
            platformClient.LoginWithHive();
            return;
        }

        bool opensWebShop = string.Equals(catalogMode, "hive-web-shop", StringComparison.OrdinalIgnoreCase);
        if (opensWebShop)
        {
            if (string.IsNullOrWhiteSpace(hiveWebShopUrl))
            {
                SetStatus("HIVE 웹 상점 주소가 설정되지 않았습니다.", true);
                return;
            }

            Application.OpenURL(hiveWebShopUrl);
            SetStatus("HIVE 웹 상점을 새 창에서 열었습니다.", false);
            return;
        }

        StartCoroutine(PurchaseMock(product));
    }

    private IEnumerator PurchaseMock(InAppMarketProduct product)
    {
        SetCardsBusy(true);
        SetStatus($"{product.name} 구매를 처리하는 중입니다…", false);
        bool purchaseReceived = false;
        yield return platformClient.CreateMockPurchase(product.id, json =>
        {
            purchaseReceived = true;
            ParsePurchase(json);
        });
        SetCardsBusy(false);
        if (!purchaseReceived)
            yield break;
    }

    private void SetCardsBusy(bool isBusy)
    {
        foreach (UI_InAppMarketProductCard card in cards)
            card.SetBusy(isBusy);
    }

    private void RefreshLoginState()
    {
        if (platformClient == null)
            return;

        loginStatusText.text = platformClient.IsLoggedIn ? "HIVE 로그인됨" : "로그인하지 않음";
        loginButtonText.text = platformClient.IsLoggedIn ? "보유품 새로고침" : "HIVE 로그인";
    }

    private void SetStatus(string message, bool isError)
    {
        statusText.text = message;
        statusText.color = isError
            ? new Color32(173, 55, 42, 255)
            : new Color32(79, 58, 43, 255);
        retryButton.gameObject.SetActive(isError);
    }

    private void SelectInitialControl()
    {
        if (EventSystem.current == null)
            return;

        if (!platformClient.IsLoggedIn)
        {
            EventSystem.current.SetSelectedGameObject(loginButton.gameObject);
            return;
        }

        foreach (UI_InAppMarketProductCard card in cards)
        {
            if (card.PurchaseButton != null && card.PurchaseButton.interactable)
            {
                EventSystem.current.SetSelectedGameObject(card.PurchaseButton.gameObject);
                return;
            }
        }

        EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void Close()
    {
        if (isClosing)
            return;
        isClosing = true;
        Managers.UI.CloseUI(false);
    }
}
