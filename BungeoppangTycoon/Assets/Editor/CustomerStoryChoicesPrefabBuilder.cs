using TMPro;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 대화 UI를 실제 프리팹 자식으로 생성한다.
/// 게임 실행 중에는 UI를 만들지 않으므로, 이후에는 이 프리팹을 Inspector에서 바로 조정하면 된다.
/// </summary>
public static class CustomerStoryChoicesPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_CustomerStoryChoices.prefab";
    private const int PrefabBuildVersion = 3;
    private const string SessionBuildVersionKey = "Bungeoppang.CustomerStoryChoicesPrefabBuildVersion";

    [DidReloadScripts]
    private static void RebuildAfterScriptReload()
    {
        // CI의 WebGL 빌드는 저장된 프리팹을 사용한다. 여기서 프리팹을 다시 생성하면
        // 에디터 초기화 중 리소스가 변경되어 빌드가 불안정해질 수 있다.
        if (Application.isBatchMode)
            return;

        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetInt(SessionBuildVersionKey, 0) >= PrefabBuildVersion)
                return;

            BuildCustomerStoryChoicesPrefab();
            SessionState.SetInt(SessionBuildVersionKey, PrefabBuildVersion);
        };
    }

    [MenuItem("Tools/Bungeoppang/Build Customer Story Choices Prefab")]
    public static void BuildCustomerStoryChoicesPrefab()
    {
        GameObject root = new("UI_CustomerStoryChoices", typeof(RectTransform), typeof(UI_CustomerStoryChoices));
        try
        {
            Stretch(root.GetComponent<RectTransform>());

            GameObject focusRoot = CreateUiObject("ConversationFocus", root.transform);
            Stretch(focusRoot.GetComponent<RectTransform>());

            // 이 이미지는 저채도처럼 보이는 어두운 막이면서, 다른 손님의 클릭도 함께 차단한다.
            Image blocker = CreateImage("DesaturatedInteractionBlocker", focusRoot.transform, new Color(.07f, .09f, .14f, .76f), true);
            Stretch(blocker.rectTransform);

            Image focusedCustomer = CreateImage("FocusedCustomer", focusRoot.transform, Color.white, false);
            focusedCustomer.preserveAspect = true;

            GameObject choicePanel = CreateUiObject("ChoicePanel", focusRoot.transform);
            RectTransform choicePanelRect = choicePanel.GetComponent<RectTransform>();
            choicePanelRect.anchorMin = choicePanelRect.anchorMax = new Vector2(0f, .5f);
            choicePanelRect.pivot = new Vector2(0f, .5f);
            choicePanelRect.anchoredPosition = new Vector2(64f, 0f);
            choicePanelRect.sizeDelta = new Vector2(560f, 790f);

            Sprite balloon = Resources.Load<Sprite>("Sprites/UI/DialogueBallon");
            if (balloon == null)
                throw new System.InvalidOperationException("필수 말풍선 이미지를 찾지 못했습니다: Resources/Sprites/UI/DialogueBallon.png");
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
            Button[] buttons = new Button[3];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[3];
            float[] slotY = { 260f, 0f, -260f };

            for (int i = 0; i < buttons.Length; i++)
            {
                Image card = CreateImage($"Choice{i + 1}", choicePanel.transform, Color.white, true);
                card.sprite = balloon;
                card.type = Image.Type.Sliced;
                card.preserveAspect = false;
                RectTransform cardRect = card.rectTransform;
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
                cardRect.pivot = new Vector2(.5f, .5f);
                cardRect.anchoredPosition = new Vector2(0f, slotY[i]);
                // 말풍선 원본의 둥근 가장자리가 찌그러지지 않는 크기다.
                cardRect.sizeDelta = new Vector2(520f, 220f);

                Shadow shadow = card.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.12f, .08f, .05f, .3f);
                shadow.effectDistance = new Vector2(3f, -5f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(.9f, .97f, 1f, 1f);
                colors.selectedColor = new Color(1f, .94f, .72f, 1f);
                colors.pressedColor = new Color(.8f, .9f, .98f, 1f);
                colors.fadeDuration = .08f;
                button.colors = colors;
                buttons[i] = button;

                TextMeshProUGUI label = CreateText("Label", card.transform, font, 34f, TextAlignmentOptions.Center);
                label.enableAutoSizing = true;
                label.fontSizeMin = 25f;
                label.fontSizeMax = 34f;
                label.color = new Color(.22f, .15f, .11f, 1f);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.lineSpacing = -8f;
                label.characterSpacing = .5f;
                Stretch(label.rectTransform, 58f, 58f, 40f, 40f);
                labels[i] = label;
            }

            Image replyBubble = CreateImage("CustomerReplyBubble", focusRoot.transform, Color.white, false);
            replyBubble.sprite = balloon;
            replyBubble.type = Image.Type.Sliced;
            replyBubble.preserveAspect = false;
            RectTransform replyRect = replyBubble.rectTransform;
            replyRect.anchorMin = replyRect.anchorMax = new Vector2(.5f, .5f);
            replyRect.pivot = new Vector2(.5f, .5f);
            replyRect.sizeDelta = new Vector2(680f, 290f);

            TextMeshProUGUI replyText = CreateText("ReplyText", replyBubble.transform, font, 31f, TextAlignmentOptions.Center);
            replyText.enableAutoSizing = true;
            replyText.fontSizeMin = 24f;
            replyText.fontSizeMax = 31f;
            replyText.color = new Color(.22f, .15f, .11f, 1f);
            replyText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(replyText.rectTransform, 86f, 86f, 62f, 62f);

            focusRoot.SetActive(false);
            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            root.GetComponent<UI_CustomerStoryChoices>().SetPrefabReferences(
                focusRoot, choicePanel, replyBubble.gameObject, focusedCustomer, replyText, buttons, labels);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"낮 대화 UI 프리팹을 생성했습니다: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject target = new(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
    {
        GameObject target = CreateUiObject(objectName, parent);
        Image image = target.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject target = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
