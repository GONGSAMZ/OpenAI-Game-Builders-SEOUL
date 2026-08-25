using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 메뉴에서 여는 손님·스토리 도감 팝업입니다.</summary>
public sealed class UI_Collection : UI_Base
{
    private Button closeButton;
    private Button customerTabButton;
    private Button storyTabButton;
    private Button backButton;
    private GameObject customerPanel;
    private GameObject storyPanel;
    private GameObject detailPanel;
    private GameObject replayPanel;
    private Transform customerGrid;
    private Transform storyGrid;
    private Image detailPortrait;
    private TextMeshProUGUI detailName;
    private TextMeshProUGUI detailAge;
    private TextMeshProUGUI detailJob;
    private TextMeshProUGUI detailIntroduction;
    private TextMeshProUGUI recentTalks;
    private RawImage replayImage;
    private TextMeshProUGUI replayTitle;
    private Button previousSceneButton;
    private Button nextSceneButton;
    private int replaySceneIndex;
    private CustomerCollectionEntry replayEntry;

    protected override void Init()
    {
        closeButton = FindButton("CloseButton");
        customerTabButton = FindButton("CustomerTabButton");
        storyTabButton = FindButton("StoryTabButton");
        backButton = FindButton("BackButton");
        previousSceneButton = FindButton("PreviousSceneButton");
        nextSceneButton = FindButton("NextSceneButton");
        customerPanel = FindObject("CustomerPanel");
        storyPanel = FindObject("StoryPanel");
        detailPanel = FindObject("DetailPanel");
        replayPanel = FindObject("ReplayPanel");
        customerGrid = FindObject("CustomerGrid").transform;
        storyGrid = FindObject("StoryGrid").transform;
        detailPortrait = Util.Find<Image>(gameObject, "DetailPortrait", true);
        detailName = FindText("DetailName"); detailAge = FindText("DetailAge"); detailJob = FindText("DetailJob");
        detailIntroduction = FindText("DetailIntroduction"); recentTalks = FindText("RecentTalks"); replayTitle = FindText("ReplayTitle");
        replayImage = Util.Find<RawImage>(gameObject, "ReplayImage", true);

        Bind(closeButton, Close); Bind(customerTabButton, ShowCustomerList); Bind(storyTabButton, ShowStoryList); Bind(backButton, ShowCustomerList);
        Bind(previousSceneButton, () => MoveReplayScene(-1)); Bind(nextSceneButton, () => MoveReplayScene(1));
        CustomerCollectionProgress.Changed += RefreshProgress;
        CustomerStoryProgress.Changed += RefreshProgress;
        ShowCustomerList();
    }

    private void OnDestroy()
    {
        CustomerCollectionProgress.Changed -= RefreshProgress;
        CustomerStoryProgress.Changed -= RefreshProgress;
    }

    private void RefreshProgress()
    {
        if (customerPanel != null && customerPanel.activeSelf)
            BuildCustomerCards();
        else if (storyPanel != null && storyPanel.activeSelf)
            BuildStoryCards();
    }

    private void ShowCustomerList()
    {
        replayPanel.SetActive(false); detailPanel.SetActive(false); storyPanel.SetActive(false); customerPanel.SetActive(true);
        SetTabButtonsVisible(true);
        BuildCustomerCards();
        SetTabVisual(customerTabButton, true); SetTabVisual(storyTabButton, false);
    }

    private void ShowStoryList()
    {
        replayPanel.SetActive(false); detailPanel.SetActive(false); customerPanel.SetActive(false); storyPanel.SetActive(true);
        SetTabButtonsVisible(true);
        BuildStoryCards();
        SetTabVisual(customerTabButton, false); SetTabVisual(storyTabButton, true);
    }

    private void BuildCustomerCards()
    {
        Clear(customerGrid);
        foreach (CustomerCollectionEntry entry in CustomerCollectionCatalog.Entries)
        {
            bool unlocked = CustomerCollectionProgress.HasMet(entry.CustomerType);
            Button card = CreateCard(customerGrid, unlocked ? entry.DisplayName : "???", unlocked ? entry.Job : "아직 만나지 못한 손님", LoadPortrait(entry), unlocked);
            if (unlocked) card.onClick.AddListener(() => ShowDetail(entry));
        }
    }

    private void ShowDetail(CustomerCollectionEntry entry)
    {
        customerPanel.SetActive(false); detailPanel.SetActive(true);
        SetTabButtonsVisible(false);
        detailPortrait.sprite = LoadPortrait(entry);
        detailName.text = entry.DisplayName; detailAge.text = entry.Age + "세"; detailJob.text = entry.Job;
        detailIntroduction.text = entry.PlayerIntroduction;
        recentTalks.text = BuildRecentTalks(entry);
    }

    private void BuildStoryCards()
    {
        Clear(storyGrid);
        foreach (CustomerCollectionEntry entry in CustomerCollectionCatalog.Entries)
        {
            bool unlocked = IsStoryUnlocked(entry);
            Button card = CreateCard(storyGrid, unlocked ? entry.StoryTitle : "???", unlocked ? entry.StorySoulName + " · 다시 보기" : "특별 주문을 완성해 보세요", LoadPortrait(entry), unlocked);
            if (unlocked) card.onClick.AddListener(() => OpenReplay(entry));
        }
    }

    private bool IsStoryUnlocked(CustomerCollectionEntry entry) => CustomerStoryProgress.IsStoryCompletedFor(entry.CustomerType);

    private string BuildRecentTalks(CustomerCollectionEntry entry)
    {
        CustomerStoryData story = CustomerStoryCatalog.Get(entry.CustomerType);
        if (story == null) return "아직 나눈 대화가 없어요.";
        List<string> talks = new();
        for (int i = 0; i < story.Topics.Length; i++)
            if (CustomerStoryProgress.CompletedTopicsFor(entry.CustomerType).Contains(i)) talks.Add("• " + story.Topics[i].Choice);
        return talks.Count == 0 ? "아직 나눈 대화가 없어요." : string.Join("\n", talks);
    }

    private void OpenReplay(CustomerCollectionEntry entry)
    {
        gameObject.SetActive(false);
        CustomerStoryCutscenePlayer.Replay(entry.CustomerType,() =>
        {
            if (this == null) return;
            gameObject.SetActive(true);
            ShowStoryList();
        });
    }

    private void MoveReplayScene(int delta)
    {
        replaySceneIndex = Mathf.Clamp(replaySceneIndex + delta, 1, 5);
        RenderReplayScene();
    }

    private void RenderReplayScene()
    {
        Texture2D scene = Resources.Load<Texture2D>($"StoryCutscenes/{replayEntry.StoryFolder}/story-{replaySceneIndex:00}");
        replayImage.texture = scene;
        replayTitle.text = $"{replayEntry.DisplayName} · {replayEntry.StoryTitle}  ({replaySceneIndex}/5)";
        previousSceneButton.interactable = replaySceneIndex > 1;
        nextSceneButton.interactable = replaySceneIndex < 5;
    }

    private Button CreateCard(Transform parent, string title, string subtitle, Sprite portrait, bool unlocked)
    {
        GameObject root = new("CollectionCard", typeof(RectTransform), typeof(Image), typeof(Button)); root.transform.SetParent(parent, false);
        Image bg = root.GetComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("Sprites/UI/SettingsV4/Generated/card-surface-v4");
        bg.type = Image.Type.Sliced;
        bg.color = unlocked ? Color.white : new Color(.42f, .43f, .40f, 1f);
        Button button = root.GetComponent<Button>(); button.interactable = unlocked;
        Image art = CreateImage("Portrait", root.transform, portrait); art.rectTransform.anchorMin = new Vector2(.08f, .42f); art.rectTransform.anchorMax = new Vector2(.92f, .92f); art.rectTransform.offsetMin = art.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI titleText = CreateText("Title", root.transform, 24, FontStyles.Bold); titleText.text = title; titleText.color = unlocked ? new Color(.27f, .19f, .12f) : Color.white; titleText.rectTransform.anchorMin = new Vector2(.10f, .22f); titleText.rectTransform.anchorMax = new Vector2(.90f, .40f); titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI subtitleText = CreateText("Subtitle", root.transform, 16, FontStyles.Normal); subtitleText.text = subtitle; subtitleText.color = unlocked ? new Color(.42f, .34f, .26f) : new Color(.92f, .92f, .88f); subtitleText.rectTransform.anchorMin = new Vector2(.10f, .06f); subtitleText.rectTransform.anchorMax = new Vector2(.90f, .22f); subtitleText.rectTransform.offsetMin = subtitleText.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private Sprite LoadPortrait(CustomerCollectionEntry entry) => Managers.Resource.LoadSprite(entry.SpritePath, 0);
    private void Close()
    {
        if (transform.parent == null) Destroy(gameObject);
        else Managers.UI.CloseUI(false);
    }
    private void Bind(Button button, UnityEngine.Events.UnityAction action) { if (button == null) return; button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
    private Button FindButton(string name) => Util.Find<Button>(gameObject, name, true);
    private GameObject FindObject(string name) => Util.FindObject(gameObject, name, true);
    private TextMeshProUGUI FindText(string name) => Util.Find<TextMeshProUGUI>(gameObject, name, true);
    private void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject); }
    private void SetTabButtonsVisible(bool visible) { if (customerTabButton != null) customerTabButton.gameObject.SetActive(visible); if (storyTabButton != null) storyTabButton.gameObject.SetActive(visible); }
    private void SetTabVisual(Button button, bool active) { Image image = button.GetComponent<Image>(); if (image != null) image.color = active ? new Color(.12f, .31f, .32f) : new Color(.83f, .70f, .47f); }
    private static Image CreateImage(string name, Transform parent, Sprite sprite) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; return image; }
    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset; text.fontSize = size; text.fontStyle = style; text.color = new Color(.16f, .16f, .14f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text; }
}
