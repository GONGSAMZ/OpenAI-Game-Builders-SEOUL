using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_Intro : MonoBehaviour
{

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Managers.UI.ShowUI<UI_HowToPlay>();


    }

}
