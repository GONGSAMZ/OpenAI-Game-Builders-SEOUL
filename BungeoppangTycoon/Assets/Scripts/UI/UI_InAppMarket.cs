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
    private Button productTabButton;
    private Button purchaseHistoryTabButton;
    private Button loadMorePurchasesButton;
    private TextMeshProUGUI loginStatusText;
    private TextMeshProUGUI loginButtonText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI emptyStateText;
    private TextMeshProUGUI purchaseHistoryEmptyText;
    private TextMeshProUGUI loadMorePurchasesText;
    private GameObject productScrollView;
    private GameObject purchaseHistoryScrollView;
    private Transform productContent;
    private Transform purchaseHistoryContent;
    private GameObject productCardTemplate;
    private GameObject purchaseHistoryRowTemplate;

    private readonly List<UI_InAppMarketProductCard> cards = new();
    private readonly List<GameObject> purchaseHistoryRows = new();
    private GamePlatformClient platformClient;
    private InAppMarketProduct[] products = Array.Empty<InAppMarketProduct>();
    private string catalogMode = "mock";
    private string purchaseHistoryCursor;
    private bool wasGameRunning;
    private bool didPauseGame;
    private bool isClosing;
    private bool showingPurchaseHistory;
    private bool purchaseHistoryLoaded;
    private bool purchaseHistoryLoading;
    private int purchaseHistoryGeneration;
    private bool lastObservedLoginState;

    protected override void Init()
    {
        closeButton = Util.Find<Button>(gameObject, "CloseButton", true);
        loginButton = Util.Find<Button>(gameObject, "LoginButton", true);
        retryButton = Util.Find<Button>(gameObject, "RetryButton", true);
        productTabButton = Util.Find<Button>(gameObject, "ProductTabButton", true);
        purchaseHistoryTabButton = Util.Find<Button>(gameObject, "PurchaseHistoryTabButton", true);
        loadMorePurchasesButton = Util.Find<Button>(gameObject, "LoadMorePurchasesButton", true);
        loginStatusText = Util.Find<TextMeshProUGUI>(gameObject, "LoginStatusText", true);
        loginButtonText = Util.Find<TextMeshProUGUI>(gameObject, "LoginButtonText", true);
        statusText = Util.Find<TextMeshProUGUI>(gameObject, "StatusText", true);
        emptyStateText = Util.Find<TextMeshProUGUI>(gameObject, "EmptyStateText", true);
        purchaseHistoryEmptyText = Util.Find<TextMeshProUGUI>(gameObject, "PurchaseHistoryEmptyText", true);
        loadMorePurchasesText = Util.Find<TextMeshProUGUI>(gameObject, "LoadMorePurchasesText", true);
        productScrollView = Util.FindObject(gameObject, "ProductScrollView", true);
        purchaseHistoryScrollView = Util.FindObject(gameObject, "PurchaseHistoryScrollView", true);
        productContent = Util.FindObject(gameObject, "ProductContent", true)?.transform;
        purchaseHistoryContent = Util.FindObject(gameObject, "PurchaseHistoryContent", true)?.transform;
        productCardTemplate = Util.FindObject(gameObject, "ProductCardTemplate", true);
        purchaseHistoryRowTemplate = Util.FindObject(gameObject, "PurchaseHistoryRowTemplate", true);

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
        platformClient.SessionChanged += OnSessionChanged;
        platformClient.RequestFailed += OnRequestFailed;
        platformClient.StoreStateChanged += OnStoreStateChanged;
        platformClient.PaymentSucceeded += OnPaymentSucceeded;
        lastObservedLoginState = platformClient.IsLoggedIn;

        closeButton.onClick.AddListener(Close);
        loginButton.onClick.AddListener(LoginOrRefresh);
        retryButton.onClick.AddListener(RefreshCurrentTab);
        productTabButton.onClick.AddListener(ShowProductTab);
        purchaseHistoryTabButton.onClick.AddListener(ShowPurchaseHistoryTab);
        loadMorePurchasesButton.onClick.AddListener(LoadMorePurchases);
        retryButton.gameObject.SetActive(false);

        SetActiveTab(false);
        RefreshLoginState();
        StartCoroutine(LoadInitialData());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        if (platformClient == null || lastObservedLoginState == platformClient.IsLoggedIn) return;
        lastObservedLoginState = platformClient.IsLoggedIn;
        OnSessionChanged(platformClient.AccountSubject);
    }

    private void OnDestroy()
    {
        if (platformClient != null)
        {
            platformClient.LoginSucceeded -= OnLoginSucceeded;
            platformClient.SessionChanged -= OnSessionChanged;
            platformClient.RequestFailed -= OnRequestFailed;
            platformClient.StoreStateChanged -= OnStoreStateChanged;
            platformClient.PaymentSucceeded -= OnPaymentSucceeded;
        }
        if (didPauseGame) Managers.Game.isRunning = wasGameRunning;
    }

    private bool ValidateReferences()
    {
        return closeButton != null && loginButton != null && retryButton != null &&
            productTabButton != null && purchaseHistoryTabButton != null &&
            loadMorePurchasesButton != null && loginStatusText != null &&
            loginButtonText != null && statusText != null && emptyStateText != null &&
            purchaseHistoryEmptyText != null && loadMorePurchasesText != null &&
            productScrollView != null && purchaseHistoryScrollView != null &&
            productContent != null && purchaseHistoryContent != null &&
            productCardTemplate != null && purchaseHistoryRowTemplate != null;
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
            yield return platformClient.GetInventory(_ => inventoryReceived = true);
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

    private void RefreshCurrentTab()
    {
        retryButton.gameObject.SetActive(false);
        if (showingPurchaseHistory)
        {
            ResetPurchaseHistory();
            StartCoroutine(LoadPurchaseHistoryPage());
            return;
        }
        RefreshMarket();
    }

    private void ShowProductTab()
    {
        SetActiveTab(false);
        SetStatus(platformClient.IsLoggedIn
            ? "상품을 선택해 주세요."
            : "보유 아이템 확인과 구매를 위해 HIVE 로그인이 필요합니다.", false);
        SelectInitialControl();
    }

    private void ShowPurchaseHistoryTab()
    {
        SetActiveTab(true);
        if (!platformClient.IsLoggedIn)
        {
            RenderPurchaseHistoryLoginRequired();
            SetStatus("구매 내역은 HIVE 로그인 후 확인할 수 있습니다.", false);
            return;
        }
        if (!purchaseHistoryLoaded && !purchaseHistoryLoading)
            StartCoroutine(LoadPurchaseHistoryPage());
    }

    private void SetActiveTab(bool history)
    {
        showingPurchaseHistory = history;
        productScrollView.SetActive(!history);
        purchaseHistoryScrollView.SetActive(history);
        loadMorePurchasesButton.gameObject.SetActive(history && platformClient != null &&
            platformClient.IsLoggedIn && purchaseHistoryLoaded &&
            !string.IsNullOrWhiteSpace(purchaseHistoryCursor));
        SetTabVisual(productTabButton, !history);
        SetTabVisual(purchaseHistoryTabButton, history);
    }

    private static void SetTabVisual(Button button, bool selected)
    {
        if (button.targetGraphic != null)
            button.targetGraphic.color = selected
                ? new Color32(112, 68, 42, 255)
                : new Color32(235, 219, 184, 255);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.selectedColor = Color.white;
        button.colors = colors;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = selected ? Color.white : new Color32(112, 68, 42, 255);
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
        yield return platformClient.GetInventory(_ => inventoryReceived = true);
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
        lastObservedLoginState = platformClient.IsLoggedIn;
        RefreshLoginState();
        ResetPurchaseHistory();
        if (showingPurchaseHistory) StartCoroutine(LoadPurchaseHistoryPage());
        else RefreshMarket();
    }

    private void OnSessionChanged(string _)
    {
        lastObservedLoginState = platformClient.IsLoggedIn;
        ResetPurchaseHistory();
        RefreshLoginState();
        if (!showingPurchaseHistory) return;
        if (platformClient.IsLoggedIn) StartCoroutine(LoadPurchaseHistoryPage());
        else RenderPurchaseHistoryLoginRequired();
    }

    private void OnStoreStateChanged()
    {
        if (isClosing || platformClient == null) return;
        RefreshLoginState();
        if (products != null && products.Length > 0) RenderProducts();
    }

    private void OnRequestFailed(string message)
    {
        SetCardsBusy(false);
        if (showingPurchaseHistory && purchaseHistoryRows.Count == 0)
        {
            purchaseHistoryEmptyText.gameObject.SetActive(true);
            purchaseHistoryEmptyText.text = "구매 내역을 불러오지 못했습니다. 다시 시도해 주세요.";
        }
        SetStatus(string.IsNullOrWhiteSpace(message) ? "서버 요청에 실패했습니다." : message, true);
    }

    private void ParsePublicConfig(string json)
    {
        InAppMarketPublicConfig config = ParseJson<InAppMarketPublicConfig>(json);
        if (config != null && !string.IsNullOrWhiteSpace(config.storeMode)) catalogMode = config.storeMode;
    }

    private void ParseCatalog(string json)
    {
        InAppMarketCatalog catalog = ParseJson<InAppMarketCatalog>(json);
        if (catalog == null) return;
        if (!string.IsNullOrWhiteSpace(catalog.mode)) catalogMode = catalog.mode;
        products = catalog.products ?? Array.Empty<InAppMarketProduct>();
    }

    private T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("서버가 빈 응답을 반환했습니다.", true);
            return null;
        }
        try { return JsonUtility.FromJson<T>(json); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("상점 응답을 읽지 못했습니다.", true);
            return null;
        }
    }

    private void RenderProducts()
    {
        foreach (UI_InAppMarketProductCard card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();

        bool hasProducts = products != null && products.Length > 0;
        emptyStateText.gameObject.SetActive(!hasProducts);
        if (!hasProducts)
        {
            emptyStateText.text = "표시할 상품이 없습니다.";
            return;
        }

        foreach (InAppMarketProduct product in products)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.id)) continue;
            GameObject cardObject = Instantiate(productCardTemplate, productContent);
            cardObject.name = $"ProductCard_{product.id}";
            cardObject.SetActive(true);
            UI_InAppMarketProductCard card = cardObject.GetComponent<UI_InAppMarketProductCard>();
            int ownedQuantity = product.grant != null && !string.IsNullOrWhiteSpace(product.grant.itemId)
                ? platformClient.GetItemQuantity(product.grant.itemId)
                : 0;
            bool isEquipped = product.grant?.itemId == "golden-pan" && platformClient.IsGoldenPanEquipped;
            card.SetData(product, ownedQuantity, platformClient.IsLoggedIn, catalogMode, isEquipped, Purchase, ChangeEquipment);
            cards.Add(card);
        }
        RefreshLoginState();
        if (!showingPurchaseHistory) SelectInitialControl();
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
        if (string.Equals(catalogMode, "hive-web-shop", StringComparison.OrdinalIgnoreCase))
        {
            platformClient.OpenHiveWebShop();
            SetStatus("HIVE 웹 상점을 새 창에서 열었습니다.", false);
            return;
        }
        if (string.Equals(catalogMode, "nicepay-test", StringComparison.OrdinalIgnoreCase))
        {
            SetCardsBusy(true);
            SetStatus($"{product.name} NICEPAY 테스트 결제창을 열었습니다.", false);
            platformClient.OpenNicePayTestCheckout(product.id);
            return;
        }
        StartCoroutine(PurchaseMock(product));
    }

    private void OnPaymentSucceeded()
    {
        SetCardsBusy(false);
        ResetPurchaseHistory();
        if (showingPurchaseHistory) StartCoroutine(LoadPurchaseHistoryPage());
        SetStatus("NICEPAY 테스트 결제가 계정 보유 아이템에 지급됐습니다.", false);
    }

    private IEnumerator PurchaseMock(InAppMarketProduct product)
    {
        SetCardsBusy(true);
        SetStatus($"{product.name} 구매를 처리하는 중입니다…", false);
        bool purchaseReceived = false;
        bool duplicate = false;
        yield return platformClient.CreateMockPurchase(product.id, json =>
        {
            purchaseReceived = true;
            InAppMarketPurchaseResponse response = ParseJson<InAppMarketPurchaseResponse>(json);
            duplicate = response != null && response.duplicate;
        });
        ResetPurchaseHistory();
        if (!purchaseReceived)
        {
            SetCardsBusy(false);
            yield break;
        }
        bool stateReceived = false;
        yield return platformClient.GetInventory(_ => stateReceived = true);
        SetCardsBusy(false);
        if (!stateReceived) yield break;
        RenderProducts();
        SetStatus(duplicate
            ? "이미 처리된 구매입니다. 보유 수량을 다시 확인했습니다."
            : "상품이 계정 보유 아이템에 지급됐습니다.", false);
    }

    private void ChangeEquipment(InAppMarketProduct product, bool equip)
    {
        if (product?.grant?.itemId != "golden-pan" || !platformClient.IsLoggedIn) return;
        StartCoroutine(ChangeEquipmentRoutine(equip));
    }

    private IEnumerator ChangeEquipmentRoutine(bool equip)
    {
        SetCardsBusy(true);
        SetStatus(equip ? "황금 붕어빵 틀을 장착하는 중입니다…" : "황금 붕어빵 틀을 해제하는 중입니다…", false);
        bool updated = false;
        yield return platformClient.SetMoldSkin(equip, _ => updated = true);
        SetCardsBusy(false);
        if (!updated) yield break;
        RenderProducts();
        SetStatus(equip ? "황금 붕어빵 틀을 장착했습니다." : "기본 붕어빵 틀로 돌아왔습니다.", false);
    }

    private void SetCardsBusy(bool isBusy)
    {
        foreach (UI_InAppMarketProductCard card in cards) card.SetBusy(isBusy);
    }

    private void LoadMorePurchases()
    {
        if (!purchaseHistoryLoading && !string.IsNullOrWhiteSpace(purchaseHistoryCursor))
            StartCoroutine(LoadPurchaseHistoryPage());
    }

    private IEnumerator LoadPurchaseHistoryPage()
    {
        if (!platformClient.IsLoggedIn)
        {
            RenderPurchaseHistoryLoginRequired();
            yield break;
        }

        int generation = purchaseHistoryGeneration;
        string subject = platformClient.SessionSubject;
        purchaseHistoryLoading = true;
        loadMorePurchasesButton.interactable = false;
        loadMorePurchasesText.text = "불러오는 중…";
        if (purchaseHistoryRows.Count == 0)
        {
            purchaseHistoryEmptyText.gameObject.SetActive(true);
            purchaseHistoryEmptyText.text = "구매 내역을 불러오는 중입니다…";
        }
        SetStatus("계정 구매 내역을 불러오는 중입니다…", false);

        bool received = false;
        yield return platformClient.GetPurchaseHistory(purchaseHistoryCursor, json =>
        {
            if (generation != purchaseHistoryGeneration ||
                subject != platformClient.SessionSubject) return;
            InAppMarketPurchaseHistoryResponse response = ParseJson<InAppMarketPurchaseHistoryResponse>(json);
            if (response == null) return;
            received = true;
            AppendPurchaseHistory(response.purchases ?? Array.Empty<InAppMarketPurchaseHistoryEntry>());
            purchaseHistoryCursor = response.nextCursor;
        });

        if (generation != purchaseHistoryGeneration || subject != platformClient.SessionSubject)
            yield break;

        purchaseHistoryLoading = false;
        purchaseHistoryLoaded |= received;
        loadMorePurchasesButton.interactable = true;
        loadMorePurchasesText.text = "더 보기";
        loadMorePurchasesButton.gameObject.SetActive(showingPurchaseHistory && received &&
            !string.IsNullOrWhiteSpace(purchaseHistoryCursor));
        if (!received) yield break;

        purchaseHistoryEmptyText.gameObject.SetActive(purchaseHistoryRows.Count == 0);
        purchaseHistoryEmptyText.text = "아직 구매 내역이 없습니다.";
        SetStatus(purchaseHistoryRows.Count == 0
            ? "첫 구매가 완료되면 이 계정의 내역이 표시됩니다."
            : $"최근 구매 내역 {purchaseHistoryRows.Count:N0}건을 표시했습니다.", false);
    }

    private void AppendPurchaseHistory(InAppMarketPurchaseHistoryEntry[] entries)
    {
        foreach (InAppMarketPurchaseHistoryEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.purchaseId)) continue;
            GameObject row = Instantiate(purchaseHistoryRowTemplate, purchaseHistoryContent);
            row.name = $"PurchaseHistory_{entry.purchaseId}";
            row.SetActive(true);
            Util.Find<TextMeshProUGUI>(row, "PurchaseDateText", true).text = FormatPurchaseDate(entry.createdAt);
            Util.Find<TextMeshProUGUI>(row, "PurchaseProductText", true).text =
                string.IsNullOrWhiteSpace(entry.productName) ? entry.productId : entry.productName;
            Util.Find<TextMeshProUGUI>(row, "PurchaseDetailText", true).text =
                $"{ProviderLabel(entry.provider)} · {entry.quantity:N0}개 · {FormatAmount(entry)}";
            TextMeshProUGUI statusLabel = Util.Find<TextMeshProUGUI>(row, "PurchaseStatusText", true);
            statusLabel.text = StatusLabel(entry.status);
            statusLabel.color = StatusColor(entry.status);
            purchaseHistoryRows.Add(row);
        }
    }

    private void ResetPurchaseHistory()
    {
        purchaseHistoryGeneration++;
        foreach (GameObject row in purchaseHistoryRows)
            if (row != null) Destroy(row);
        purchaseHistoryRows.Clear();
        purchaseHistoryCursor = null;
        purchaseHistoryLoaded = false;
        purchaseHistoryLoading = false;
        if (loadMorePurchasesButton != null) loadMorePurchasesButton.gameObject.SetActive(false);
    }

    private void RenderPurchaseHistoryLoginRequired()
    {
        ResetPurchaseHistory();
        purchaseHistoryEmptyText.gameObject.SetActive(true);
        purchaseHistoryEmptyText.text = "HIVE 로그인 후 계정 구매 내역을 확인할 수 있습니다.";
        EventSystem.current?.SetSelectedGameObject(loginButton.gameObject);
    }

    private static string FormatPurchaseDate(string value) => DateTime.TryParse(value, out DateTime parsed)
        ? parsed.ToLocalTime().ToString("yyyy.MM.dd HH:mm")
        : "날짜 정보 없음";

    private static string FormatAmount(InAppMarketPurchaseHistoryEntry entry) =>
        string.Equals(entry.currency, "KRW", StringComparison.OrdinalIgnoreCase)
            ? $"₩{entry.amount:N0}"
            : $"{entry.amount:N0} {entry.currency}";

    private static string ProviderLabel(string provider) => provider switch
    {
        "nicepay-test" => "NICEPAY 테스트",
        "hive-web-shop" => "HIVE 웹 상점",
        "mock" => "테스트 포인트",
        _ => "결제"
    };

    private static string StatusLabel(string status) => status switch
    {
        "pending" => "결제 대기",
        "succeeded" => "결제 완료",
        "failed" => "결제 실패",
        "cancelled" => "결제 취소",
        "expired" => "시간 만료",
        _ => "상태 확인"
    };

    private static Color StatusColor(string status) => status switch
    {
        "succeeded" => new Color32(74, 126, 68, 255),
        "pending" => new Color32(188, 116, 37, 255),
        _ => new Color32(173, 55, 42, 255)
    };

    private void RefreshLoginState()
    {
        if (platformClient == null) return;
        loginStatusText.text = platformClient.IsLoggedIn
            ? (string.Equals(catalogMode, "mock", StringComparison.OrdinalIgnoreCase)
                ? $"HIVE 로그인됨 · 테스트 {platformClient.TestPointBalance:N0}P · 팥 코인 {platformClient.RedBeanCoinBalance:N0}개"
                : $"HIVE 로그인됨 · 팥 코인 {platformClient.RedBeanCoinBalance:N0}개")
            : "로그인하지 않음 · 팥 코인 —";
        loginButtonText.text = platformClient.IsLoggedIn ? "보유품 새로고침" : "HIVE 로그인";
    }

    private void SetStatus(string message, bool isError)
    {
        statusText.text = message;
        statusText.color = isError ? new Color32(173, 55, 42, 255) : new Color32(79, 58, 43, 255);
        retryButton.gameObject.SetActive(isError);
    }

    private void SelectInitialControl()
    {
        if (EventSystem.current == null) return;
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
        if (isClosing) return;
        isClosing = true;
        Managers.UI.CloseUI(false);
    }
}
