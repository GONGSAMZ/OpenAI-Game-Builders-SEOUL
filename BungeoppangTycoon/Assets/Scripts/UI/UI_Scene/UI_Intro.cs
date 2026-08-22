using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
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
            if (SaveService.Instance != null)
                startButton.interactable = SaveService.Instance.IsReadyForGameplay;
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
        bool saveReady = SaveService.Instance == null || SaveService.Instance.IsReadyForGameplay;
        if (startButton != null && !isStarting)
            startButton.interactable = saveReady;
        bool startButtonIsVisible = startButton != null && startButton.gameObject.activeInHierarchy;

        if (saveReady && !startButtonIsVisible && GameInput.LeftClickPressed)
            StartBtn();
    }

    public void StartBtn()
    {
        if (isStarting || (SaveService.Instance != null && !SaveService.Instance.IsReadyForGameplay))
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
