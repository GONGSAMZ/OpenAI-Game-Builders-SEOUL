using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루가 끝난 뒤 열리는 장사 준비 상점 화면입니다.
/// 실제 구매 데이터는 아직 연결되어 있지 않아, 이 스크립트는 화면 전환과 다음 날 시작만 담당합니다.
/// </summary>
public class UI_Store : UI_Base
{
    private static readonly Color32 ActiveTabColor = new(24, 91, 97, 255);
    private static readonly Color32 InactiveTabColor = new(238, 220, 179, 255);
    private static readonly Color32 ActiveTabTextColor = new(255, 247, 226, 255);
    private static readonly Color32 InactiveTabTextColor = new(54, 45, 32, 255);

    private Button nextDayButton;
    private Button fillingButton;
    private Button itemButton;
    private Image fillingTabBackground;
    private Image itemTabBackground;
    private TextMeshProUGUI fillingTabLabel;
    private TextMeshProUGUI itemTabLabel;
    private GameObject fillingCards;
    private GameObject itemCards;

    protected override void Init()
    {
        nextDayButton = Util.Find<Button>(gameObject, "NextDayButton");
        fillingButton = Util.Find<Button>(gameObject, "FillingButton");
        itemButton = Util.Find<Button>(gameObject, "SkillButton");
        fillingTabBackground = fillingButton != null ? fillingButton.GetComponent<Image>() : null;
        itemTabBackground = itemButton != null ? itemButton.GetComponent<Image>() : null;
        fillingTabLabel = fillingButton != null ? fillingButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        itemTabLabel = itemButton != null ? itemButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        fillingCards = Util.Find<Transform>(gameObject, "FillingCards", true)?.gameObject;
        itemCards = Util.Find<Transform>(gameObject, "ItemCards", true)?.gameObject;

        SetText("TitleText", "내일 장사 준비");
        SetText("SubtitleText", "팔고 싶은 붕어빵 소를 골라 보세요.");
        SetText("MoneyText", "보유금");
        SetText("MoneyNum", $"{Managers.Game.Money:N0}원");
        // 팥코인 경제 시스템은 아직 게임 데이터에 없으므로, 표시만 준비해 둡니다.
        SetText("BeanCoinText", "팥코인");
        SetText("BeanCoinNum", "0개");

        if (nextDayButton != null)
            AddEvent(nextDayButton.gameObject, Managers.Game.StartNextDay);
        if (fillingButton != null)
            AddEvent(fillingButton.gameObject, ShowFillings);
        if (itemButton != null)
            AddEvent(itemButton.gameObject, ShowItems);

        ShowFillings();
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

        SetTabStyle(fillingTabBackground, fillingTabLabel, showFillings);
        SetTabStyle(itemTabBackground, itemTabLabel, !showFillings);
    }

    private static void SetTabStyle(Image background, TextMeshProUGUI label, bool selected)
    {
        if (background != null)
            background.color = selected ? ActiveTabColor : InactiveTabColor;
        if (label != null)
            label.color = selected ? ActiveTabTextColor : InactiveTabTextColor;
    }

    private void SetText(string objectName, string value)
    {
        TextMeshProUGUI text = Util.Find<TextMeshProUGUI>(gameObject, objectName, true);
        if (text != null)
            text.text = value;
    }
}
