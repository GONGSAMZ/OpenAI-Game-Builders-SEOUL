
using UnityEngine.SceneManagement;

public class UI_HowToPlay : UI_Base
{
    protected override void Init()
    {
        gameObject.AddEvent( 
            () => {
                Managers.UI.CloseUI();
                SceneManager.LoadScene("GameScene");
                 } );
    }

    
}
