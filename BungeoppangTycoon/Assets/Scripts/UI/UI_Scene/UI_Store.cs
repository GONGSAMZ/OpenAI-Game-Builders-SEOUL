using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루가 끝난 뒤 열리는 장사 준비 상점 화면입니다.
/// 팀원이 만든 재료·도구 카드 구조는 유지하고 서버의 계정별 일반 상점 상태를 바인딩합니다.
/// </summary>
public class UI_Store : UI_Base
{
    private static readonly Color32 ActiveTabTextColor = new(255, 247, 226, 255);
    private static readonly Color32 InactiveTabTextColor = new(54, 45, 32, 255);

    private Button nextDayButton;
    private Button fillingButton;
    private Button itemButton;
    private RawImage fillingTabSurface;
    private RawImage itemTabSurface;
    private TextMeshProUGUI fillingTabLabel;
    private TextMeshProUGUI itemTabLabel;
    private TextMeshProUGUI beanCoinNum;
    private TextMeshProUGUI moneyNum;
    private GameObject fillingCards;
    private GameObject itemCards;
    private string processingProductId;
    private GamePlatformClient platformClient;

    private static readonly Dictionary<string, string> CardProducts = new()
    {
        { "RedBeanCard", "filling-red-bean" },
        { "CustardCard", "filling-custard" },
        { "ChocolateCard", "filling-nutella" },
        { "GreenTeaCard", "filling-green-tea" },
        { "GoldenPanCard", "item-double-golden-mold" },
        { "DualPourCard", "item-dual-pour" },
        { "CookingFeverCard", "item-cooking-fever" }
    };

    protected override void Init()
    {
        nextDayButton = Util.Find<Button>(gameObject, "NextDayButton");
        fillingButton = Util.Find<Button>(gameObject, "FillingButton");
        itemButton = Util.Find<Button>(gameObject, "SkillButton");
        fillingTabSurface = Util.Find<RawImage>(gameObject, "FillingTabSurface", true);
        itemTabSurface = Util.Find<RawImage>(gameObject, "ItemTabSurface", true);
        fillingTabLabel = fillingButton != null ? fillingButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        itemTabLabel = itemButton != null ? itemButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        fillingCards = Util.Find<Transform>(gameObject, "FillingCards", true)?.gameObject;
        itemCards = Util.Find<Transform>(gameObject, "ItemCards", true)?.gameObject;

        SetText("TitleText", "내일 장사 준비");
        SetText("MoneyText", "보유금");
        moneyNum = Util.Find<TextMeshProUGUI>(gameObject, "MoneyNum", true);
        SetText("BeanCoinText", "팥코인");
        beanCoinNum = Util.Find<TextMeshProUGUI>(gameObject, "BeanCoinNum", true);
        RefreshBalances();

        GamePlatformClient.InstanceChanged += BindPlatformClient;
        BindPlatformClient(GamePlatformClient.Instance);
        SaveService.Instance.GameStoreChanged += BindStoreCards;
        SaveService.Instance.DataChanged += RefreshBalances;

        if (nextDayButton != null)
            AddEvent(nextDayButton.gameObject, Managers.Game.StartNextDay);
        if (fillingButton != null)
            AddEvent(fillingButton.gameObject, ShowFillings);
        if (itemButton != null)
            AddEvent(itemButton.gameObject, ShowItems);

        ConfigurePurchaseButtons();
        SetCardsLoading();

        ShowFillings();
        SaveService.Instance.RefreshGameStore(OnStoreRefreshed);
    }

    private void ShowFillings()
    {
        SetCategory(true);
    }

    private void ShowItems()
    {
        SetCategory(false);
    }

    private void SetCategory(bool showFillings)
    {
        if (fillingCards != null)
            fillingCards.SetActive(showFillings);
        if (itemCards != null)
            itemCards.SetActive(!showFillings);

        SetText("TitleText", showFillings ? "내일 장사 준비" : "내일 장사 도구");
        // 피그마 시안에서 아이템 탭의 제목은 상점 소 탭보다 조금 위·오른쪽에 있습니다.
        // 탭을 전환해도 같은 위치에 남지 않도록 실제 UI에서도 함께 갱신합니다.
        RectTransform titleRect = Util.Find<RectTransform>(gameObject, "TitleText", true);
        if (titleRect != null)
            // 현재 프로젝트의 TMP 글꼴은 시안 글꼴보다 윗 여백이 작습니다.
            // 아이템 탭에서 제목 윗부분이 잘리지 않는 공통 높이를 사용합니다.
            titleRect.anchoredPosition = showFillings ? new Vector2(142f, -85f) : new Vector2(150f, -85f);

        SetTabStyle(fillingTabSurface, fillingTabLabel, showFillings);
        SetTabStyle(itemTabSurface, itemTabLabel, !showFillings);
    }

    private static void SetTabStyle(RawImage surface, TextMeshProUGUI label, bool selected)
    {
        if (surface != null)
            surface.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.48f);
        if (label != null)
            label.color = selected ? ActiveTabTextColor : InactiveTabTextColor;
    }

    private void SetText(string objectName, string value)
    {
        TextMeshProUGUI text = Util.Find<TextMeshProUGUI>(gameObject, objectName, true);
        if (text != null)
            text.text = value;
    }

    private void ConfigurePurchaseButtons()
    {
        foreach ((string cardName, string productId) in CardProducts)
        {
            Transform card = Util.Find<Transform>(gameObject, cardName, true);
            Button purchase = card != null
                ? Util.Find<Button>(card.gameObject, "PurchaseButton", true)
                : null;
            if (purchase == null) continue;
            string capturedProductId = productId;
            purchase.onClick.AddListener(() => Purchase(capturedProductId));
        }
    }

    private void SetCardsLoading()
    {
        if (nextDayButton != null)
            nextDayButton.interactable = false;
        foreach ((string cardName, _) in CardProducts)
        {
            Transform card = Util.Find<Transform>(gameObject, cardName, true);
            if (card == null) continue;
            SetCardButton(card.gameObject, "불러오는 중", false);
        }
        Transform next = Util.Find<Transform>(gameObject, "NextItemCard", true);
        if (next != null)
            SetCardButton(next.gameObject, "준비 중", false);
    }

    private void BindStoreCards()
    {
        if (this == null) return;
        GameStoreCatalogData catalog = SaveService.Instance.GameStoreCatalog;
        GameStoreStateData state = SaveService.Instance.GameStoreState;
        if (catalog == null || state == null)
        {
            SetCardsLoading();
            RefreshBalances();
            return;
        }

        foreach ((string cardName, string productId) in CardProducts)
        {
            Transform card = Util.Find<Transform>(gameObject, cardName, true);
            if (card == null) continue;
            GameStoreProductData product = catalog.Find(productId);
            GameStoreProductStateData productState = state.Find(productId);
            if (product == null || productState == null)
            {
                SetCardButton(card.gameObject, "준비 중", false);
                continue;
            }

            SetCardText(card.gameObject, "ProductNameText", product.displayName);
            SetCardText(card.gameObject, "ProductDescriptionText", product.description);
            SetCardText(card.gameObject, "PriceText", $"{product.price:N0}원");

            bool processing = processingProductId == productId;
            bool purchasable = !processing && productState.status == "purchasable";
            string label = processing
                ? product.category == "filling" ? "선택 처리 중" : "구매 처리 중"
                : StatusLabel(productState.status, product.category);
            SetCardButton(card.gameObject, label, purchasable);
        }

        Transform next = Util.Find<Transform>(gameObject, "NextItemCard", true);
        if (next != null)
            SetCardButton(next.gameObject, "준비 중", false);
        if (nextDayButton != null)
            nextDayButton.interactable = state.selectedFillingIds != null && state.selectedFillingIds.Length > 0;
        RefreshBalances();
    }

    private static string StatusLabel(string status, string category) => status switch
    {
        "owned" => "보유 중",
        "selected" => "선택됨",
        "purchasable" => category == "filling" ? "선택하기" : "구매 가능",
        "insufficient-funds" => "잔액 부족",
        "login-required" => "로그인 필요",
        _ => "잠김"
    };

    private static void SetCardText(GameObject card, string objectName, string value)
    {
        TextMeshProUGUI text = Util.Find<TextMeshProUGUI>(card, objectName, true);
        if (text != null) text.text = value;
    }

    private static void SetCardButton(GameObject card, string labelValue, bool interactable)
    {
        Button button = Util.Find<Button>(card, "PurchaseButton", true);
        if (button == null) return;
        button.interactable = interactable;
        TextMeshProUGUI label = Util.Find<TextMeshProUGUI>(button.gameObject, "Label", true);
        RawImage surface = Util.Find<RawImage>(button.gameObject, "PurchaseSurface", true);
        if (label != null)
        {
            label.text = labelValue;
            label.color = interactable ? ActiveTabTextColor : InactiveTabTextColor;
        }
        if (surface != null)
            surface.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
    }

    private void Purchase(string productId)
    {
        if (!string.IsNullOrEmpty(processingProductId)) return;
        processingProductId = productId;
        BindStoreCards();
        SaveService.Instance.PurchaseGameStoreProduct(productId, (success, message) =>
        {
            if (this == null) return;
            processingProductId = null;
            if (!success)
                Debug.LogWarning($"[상점] {message}");
            BindStoreCards();
        });
    }

    private void OnStoreRefreshed(bool success, string message)
    {
        if (this == null) return;
        if (!success)
            Debug.LogWarning($"[상점] {message}");
        BindStoreCards();
    }

    private void RefreshBalances()
    {
        if (moneyNum != null)
            moneyNum.text = $"{SaveService.Data.run.money:N0}원";

        GamePlatformClient client = GamePlatformClient.Instance;
        if (beanCoinNum != null)
            beanCoinNum.text = client != null && client.IsLoggedIn
                ? $"{client.RedBeanCoinBalance:N0}개"
                : "—";
    }

    private void BindPlatformClient(GamePlatformClient client)
    {
        if (platformClient == client) return;
        if (platformClient != null)
            platformClient.StoreStateChanged -= RefreshBalances;
        platformClient = client;
        if (platformClient != null)
            platformClient.StoreStateChanged += RefreshBalances;
        RefreshBalances();
    }

    private void OnDestroy()
    {
        GamePlatformClient.InstanceChanged -= BindPlatformClient;
        BindPlatformClient(null);
        if (SaveService.Instance != null)
        {
            SaveService.Instance.GameStoreChanged -= BindStoreCards;
            SaveService.Instance.DataChanged -= RefreshBalances;
        }
    }
}
