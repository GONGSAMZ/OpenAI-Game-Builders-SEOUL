using System.Collections;
using UnityEngine;

public class UI_AlertClosingTime : UI_Base
{
    protected override void Init()
    {
        StartCoroutine(close());
    }

    IEnumerator close()
    {
        yield return new WaitForSeconds(2f);

        Managers.UI.CloseUI(false);
        yield break;
    }
}
