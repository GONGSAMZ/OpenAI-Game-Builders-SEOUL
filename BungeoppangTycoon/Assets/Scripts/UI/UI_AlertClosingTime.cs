using System.Collections;
using UnityEngine;

public class UI_AlertClosingTime : UI_Base
{
    public static bool IsVisible { get; private set; }

    private void OnEnable() => IsVisible = true;
    private void OnDisable() => IsVisible = false;

    protected override void Init()
    {
        StartCoroutine(close());
    }

    IEnumerator close()
    {
        yield return new WaitForSeconds(2f);

        Managers.UI.CloseUI(false);
        IsVisible = false;
        yield break;
    }
}
