using TMPro;
using UnityEngine.UI;

public abstract class UI_SettingsPopupBase : UI_Base
{
    protected TextMeshProUGUI titleText;
    protected TextMeshProUGUI bodyText;
    protected Button closeButton;

    protected override void Init()
    {
        titleText = Util.Find<TextMeshProUGUI>(gameObject, "TitleText", true);
        bodyText = Util.Find<TextMeshProUGUI>(gameObject, "BodyText", true);
        closeButton = Util.Find<Button>(gameObject, "CloseButton", true);
        AddEvent(closeButton.gameObject, () => Managers.UI.CloseUI(false));
        Render();
    }

    protected abstract void Render();
}
