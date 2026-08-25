using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루가 끝난 뒤 열리는 장사 준비 상점 화면입니다.
/// 재료는 보유금으로 구매하며, 해금된 재료는 다음 영업일 시작 시 조리 화면에 나타납니다.
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
    private GameObject fillingCards;
    private GameObject itemCards;

    private readonly struct FillingStoreItem
    {
        public readonly string CardName;
        public readonly FillingType Filling;
        public readonly int Price;

        public FillingStoreItem(string cardName, FillingType filling, int price)
        {
            CardName = cardName;
            Filling = filling;
            Price = price;
        }
    }

    private static readonly FillingStoreItem[] FillingStoreItems =
    {
        new("RedBeanCard", FillingType.redBean, 1200),
        new("CustardCard", FillingType.custard, 1400),
        new("ChocolateCard", FillingType.nutella, 1600),
        new("GreenTeaCard", FillingType.greenTea, 1800)
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
        SetText("MoneyNum", $"{Managers.Game.Money:N0}원");
        SetText("BeanCoinText", "팥코인");
        beanCoinNum = Util.Find<TextMeshProUGUI>(gameObject, "BeanCoinNum", true);
        RefreshPlatformCurrency();

        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged += RefreshPlatformCurrency;

        if (nextDayButton != null)
            AddEvent(nextDayButton.gameObject, Managers.Game.StartNextDay);
        if (fillingButton != null)
            AddEvent(fillingButton.gameObject, ShowFillings);
        if (itemButton != null)
            AddEvent(itemButton.gameObject, ShowItems);

        BindFillingPurchaseButtons();

        ShowFillings();
    }

    private void ShowFillings()
    {
        SetCategory(true);
        RefreshFillingPurchaseCards();
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

    private void RefreshPlatformCurrency()
    {
        if (beanCoinNum == null)
            return;

        GamePlatformClient client = GamePlatformClient.Instance;
        beanCoinNum.text = client != null && client.IsLoggedIn
            ? $"{client.RedBeanCoinBalance:N0}개"
            : "—";
    }

    private void BindFillingPurchaseButtons()
    {
        foreach (FillingStoreItem item in FillingStoreItems)
        {
            Transform card = Util.Find<Transform>(gameObject, item.CardName, true);
            Button purchaseButton = card != null
                ? Util.Find<Button>(card.gameObject, "PurchaseButton", true)
                : null;
            if (purchaseButton == null)
                continue;

            FillingStoreItem capturedItem = item;
            purchaseButton.onClick.AddListener(() => TryPurchaseFilling(capturedItem));
        }

        RefreshFillingPurchaseCards();
    }

    private void TryPurchaseFilling(FillingStoreItem item)
    {
        if (Managers.Game.IsFillingUnlocked(item.Filling))
        {
            RefreshFillingPurchaseCards();
            return;
        }

        if (Managers.Game.Money < item.Price)
        {
            Debug.Log($"[상점] 보유금이 부족합니다: {item.Filling} / 필요 {item.Price:N0}원");
            return;
        }

        if (!SaveService.Instance.PurchaseFilling(item.Filling, item.Price, Managers.Game.Money))
            return;

        Managers.Game.Money -= item.Price;
        SetText("MoneyNum", $"{Managers.Game.Money:N0}원");
        RefreshFillingPurchaseCards();
    }

    private void RefreshFillingPurchaseCards()
    {
        foreach (FillingStoreItem item in FillingStoreItems)
        {
            Transform card = Util.Find<Transform>(gameObject, item.CardName, true);
            if (card == null)
                continue;

            TextMeshProUGUI priceText = Util.Find<TextMeshProUGUI>(card.gameObject, "PriceText", true);
            Button purchaseButton = Util.Find<Button>(card.gameObject, "PurchaseButton", true);
            TextMeshProUGUI buttonText = Util.Find<TextMeshProUGUI>(card.gameObject, "Label", true);
            bool owned = Managers.Game.IsFillingUnlocked(item.Filling);

            if (priceText != null)
                priceText.text = $"{item.Price:N0}원";
            if (purchaseButton != null)
                purchaseButton.interactable = !owned;
            if (buttonText != null)
                buttonText.text = owned ? "구매 완료" : "구매 가능";
        }
    }

    private void OnDestroy()
    {
        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged -= RefreshPlatformCurrency;
    }
}
