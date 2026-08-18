using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Intro : MonoBehaviour
{
    private const string GameSceneName = "GameScene";

    private Button startButton;
    private Button quitButton;
    private bool isStarting;

    private void Start()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name == "Start")
                startButton = button;
            else if (button.name == "Quit")
                quitButton = button;
        }

        if (startButton == null)
        {
            Debug.LogError("[UI_Intro] Start 버튼을 찾지 못했습니다.", this);
        }
        else
        {
            startButton.onClick.RemoveListener(StartBtn);
            startButton.onClick.AddListener(StartBtn);
        }

        if (quitButton == null)
        {
            Debug.LogError("[UI_Intro] Quit 버튼을 찾지 못했습니다.", this);
        }
        else
        {
            quitButton.onClick.RemoveListener(QuitBtn);
            quitButton.onClick.AddListener(QuitBtn);
        }
    }

    private void Update()
    {
        bool startButtonIsVisible = startButton != null && startButton.gameObject.activeInHierarchy;

        if (!startButtonIsVisible && Input.GetMouseButtonDown(0))
            StartBtn();
    }

    public void StartBtn()
    {
        if (isStarting)
            return;

        isStarting = true;

        if (startButton != null)
            startButton.interactable = false;

        UI_Tutorial.RequestPromptForGameStart();
        SceneManager.LoadScene(GameSceneName);
    }

    public void QuitBtn()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
