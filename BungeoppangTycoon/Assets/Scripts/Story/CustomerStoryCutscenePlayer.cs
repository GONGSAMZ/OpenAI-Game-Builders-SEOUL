using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>특별 주문 성공 뒤 5개 컷씬과 해금 일러스트를 한 장면씩 재생합니다.</summary>
public sealed class CustomerStoryCutscenePlayer : MonoBehaviour
{
    private static CustomerStoryCutscenePlayer instance;
    private GameObject root;
    private Image artImage;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private CustomerStoryCutsceneData[] scenes;
    private int sceneIndex;
    private int openedFrame;
    private bool wasGameRunning;
    private Action onFinished;

    public static void PlaySpecialOrderSuccess(CustomerType customerType, Action finished)
    {
        CustomerStoryCutscenePlayer player = Ensure();
        if (player != null)
            player.Open(CustomerStoryCutsceneCatalog.Get(customerType), finished);
    }

    /// <summary>손님이 등장할 때 컷씬 화면을 미리 만들어, 주문 성공 프레임에서의 생성 지연을 없앱니다.</summary>
    public static void Preload()
    {
        Ensure();
    }

    public static void Replay(CustomerType customerType, Action finished)
    {
        CustomerStoryCutscenePlayer player = Ensure();
        if (player != null)
            player.Open(CustomerStoryCutsceneCatalog.Get(customerType), finished);
    }

    // 기존 호출부와 호환됩니다.
    public static void PlayJeongHyunUnlock(Action finished) => PlaySpecialOrderSuccess(CustomerType.JeongHyun, finished);
    public static void ReplayJeongHyun(Action finished) => Replay(CustomerType.JeongHyun, finished);

    private static CustomerStoryCutscenePlayer Ensure()
    {
        if (instance != null) return instance;
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/UI_CustomerStoryCutscene");
        if (prefab == null)
        {
            Debug.LogError("[손님 이야기] UI_CustomerStoryCutscene 프리팹을 찾지 못했습니다.");
            return null;
        }

        GameObject go = Instantiate(prefab);
        go.name = "@CustomerStoryCutscenePlayer";
        DontDestroyOnLoad(go);
        instance = go.GetComponent<CustomerStoryCutscenePlayer>();
        if (instance == null)
            instance = go.AddComponent<CustomerStoryCutscenePlayer>();

        CustomerStoryCutsceneView view = go.GetComponent<CustomerStoryCutsceneView>();
        if (view == null || view.ArtImage == null || view.BodyText == null)
        {
            Debug.LogError("[손님 이야기] 컷씬 프리팹의 필수 UI 참조가 비어 있습니다.");
            Destroy(go);
            instance = null;
            return null;
        }

        instance.root = go;
        instance.artImage = view.ArtImage;
        instance.progressText = view.ProgressText;
        instance.titleText = view.TitleText;
        instance.speakerText = view.SpeakerText;
        instance.bodyText = view.BodyText;
        instance.hintText = view.HintText;
        instance.root.SetActive(false);
        return instance;
    }

    private void Open(CustomerStoryCutsceneData[] newScenes, Action finished)
    {
        if (newScenes == null || newScenes.Length == 0)
        {
            Debug.LogError("[손님 이야기] 컷씬을 열지 못했습니다: 컷씬 데이터가 비어 있습니다.");
            finished?.Invoke();
            return;
        }

        scenes = newScenes;
        onFinished = finished;
        sceneIndex = 0;
        wasGameRunning = Managers.Game != null && Managers.Game.isRunning;
        if (Managers.Game != null) Managers.Game.isRunning = false;
        root.SetActive(true);
        openedFrame = Time.frameCount;
        Debug.Log($"[손님 이야기] 컷씬 시작 | 장면 수={scenes.Length} | 첫 장면={scenes[0].Title}");
        Render();
    }

    private void Update()
    {
        if (root == null || !root.activeSelf || Time.frameCount == openedFrame)
            return;

        if (GameInput.LeftClickPressed || GameInput.KeyPressed(Key.Space) ||
            GameInput.KeyPressed(Key.Enter) || GameInput.KeyPressed(Key.NumpadEnter))
            Advance();
    }

    private void Advance()
    {
        if (sceneIndex + 1 < scenes.Length)
        {
            sceneIndex++;
            Render();
            return;
        }

        root.SetActive(false);
        if (Managers.Game != null) Managers.Game.isRunning = wasGameRunning;
        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    private void Render()
    {
        CustomerStoryCutsceneData scene = scenes[sceneIndex];
        CustomerStoryCutsceneLine line = scene.Lines != null && scene.Lines.Length > 0
            ? scene.Lines[0]
            : new CustomerStoryCutsceneLine(string.Empty, string.Empty);

        Sprite[] sprites = Resources.LoadAll<Sprite>(scene.ResourcePath);
        Sprite sprite = sprites.Length > 0 ? sprites[0] : null;
        artImage.sprite = sprite;
        artImage.enabled = sprite != null;

        progressText.text = $"{sceneIndex + 1:00} / {scenes.Length:00}";
        titleText.text = scene.Title;
        speakerText.text = line.Speaker;
        speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(line.Speaker));
        bodyText.text = line.Text;
        hintText.text = scene.IsUnlock ? "클릭하여 마치기  ›" : "클릭 · Space · Enter  ›";

        if (sprite == null)
            Debug.LogWarning($"[손님 이야기] 컷씬 이미지가 아직 없습니다: {scene.ResourcePath}");
    }

}
