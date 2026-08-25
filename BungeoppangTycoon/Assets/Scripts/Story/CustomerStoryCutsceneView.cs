using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>손님 스토리 컷씬 프리팹의 화면 요소 참조입니다.</summary>
public sealed class CustomerStoryCutsceneView : MonoBehaviour
{
    [field: SerializeField] public Image ArtImage { get; private set; }
    [field: SerializeField] public TextMeshProUGUI ProgressText { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TitleText { get; private set; }
    [field: SerializeField] public TextMeshProUGUI SpeakerText { get; private set; }
    [field: SerializeField] public TextMeshProUGUI BodyText { get; private set; }
    [field: SerializeField] public TextMeshProUGUI HintText { get; private set; }

    public void Bind(Image art, TextMeshProUGUI progress, TextMeshProUGUI title, TextMeshProUGUI speaker, TextMeshProUGUI body, TextMeshProUGUI hint)
    {
        ArtImage = art; ProgressText = progress; TitleText = title;
        SpeakerText = speaker; BodyText = body; HintText = hint;
    }
}
