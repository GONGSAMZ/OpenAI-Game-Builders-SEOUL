using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Ending : UI_Base
{
    Image img;
    TextMeshProUGUI TMP;

    string ImagePath;
    string[] lines; 
    int index;

    protected override void Init()
    {
        Debug.Log("UI_Ending-Init");

        img = Util.Find<Image>(gameObject, "Image");
        TMP = Util.Find<TextMeshProUGUI>(gameObject, "Text (TMP)");

        img.sprite = Managers.Resource.LoadSprite($"Ending/{ImagePath}");

        //대사
        index = 0;
        TMP.text = lines[index];
        gameObject.AddEvent(nextLine);
    }

    public void SetInfo(EndingType Ending)
    {
        Debug.Log($"UI_Ending-SetInfo: {Ending}");

        ImagePath = Define.EndingImagePath[(int)Ending];

        if (Ending == EndingType.Over)
        {
            lines = Define.OverEndingText;
        }
        else if(Ending == EndingType.Normal)
        {
            lines = Define.NormalEndingText;
        }
        else
        {
            lines = Define.ClearEndingText;
        }
    }

    void nextLine()
    {
        //마지막 대사 처리
        if (index == (lines.Length-1))
        {
            Managers.Game.QuitGame();
            return;
        }

        TMP.text = lines[++index];
    }
}
