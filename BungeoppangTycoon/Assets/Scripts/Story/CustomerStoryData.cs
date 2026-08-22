using System;
using UnityEngine;

/// <summary>손님 이야기의 콘텐츠와 특별 주문 정답을 담는 읽기 전용 데이터입니다.</summary>
[Serializable]
public sealed class CustomerStoryData
{
    public CustomerType CustomerType;
    public string DisplayName;
    public FillingType RequiredFilling;
    public QualityStatus RequiredBake;
    public StoryTalkTopic[] Topics;
    public string SpecialIntro;
    public string SuccessMessage;
    public string NearMissMessage;
    public string FailureMessage;
}

[Serializable]
public sealed class StoryTalkTopic
{
    public string Choice;
    [TextArea] public string FirstReply;
    [TextArea] public string RepeatReply;
}

/// <summary>v1은 정현만 제공하되, 다음 손님 데이터를 같은 형식으로 추가할 수 있습니다.</summary>
public static class CustomerStoryCatalog
{
    private static readonly CustomerStoryData JeongHyun = new()
    {
        CustomerType = CustomerType.JeongHyun,
        DisplayName = "정현",
        RequiredFilling = FillingType.custard,
        RequiredBake = QualityStatus.crisp,
        SpecialIntro = "오늘도 제 일이 아닌 것까지 다 끝냈는데…\n이상하게 제 하루는 하나도 안 남았네요.\n하… 일은 끝이 없네요. 붕어빵은 아무거나 하나 주세요.",
        SuccessMessage = "칼퇴대장 크림붕이 말했어요.\n‘끝나지 않는 일은 두고 가면 돼. 넌 끝날 시간이 있잖아.’\n정현은 휴대폰을 뒤집어 놓고, 오늘은 정시에 집에 가기로 했어요.",
        NearMissMessage = "조금 마음이 가벼워졌어요. 그런데 아직도 퇴근한다고 말할 용기가 부족한 것 같네요.",
        FailureMessage = "괜찮아요. 오늘은 조금 늦었네요. 다음에 다시 들를게요.",
        Topics = new[]
        {
            new StoryTalkTopic { Choice = "오늘도 야근이세요?", FirstReply = "네. 이걸로 저녁 때우고 들어가려고요. 자료 하나만 마저 보면 돼요.\n퇴근은 했는데, 제가 보는 게 제일 빠르거든요.", RepeatReply = "오늘도 팥 하나만 주세요. 집에 가서 볼 게 조금 남았어요." },
            new StoryTalkTopic { Choice = "몇 개 드릴까요?", FirstReply = "팥 셋, 슈크림 둘이요. 다른 팀에서도 같이 사 달라고 하네요.\n제가 내려온 김에 가져가면 되죠.", RepeatReply = "오늘도 주문이 늘었네요. 제가 온다고 괜히 말했나 봐요." },
            new StoryTalkTopic { Choice = "전화부터 받으세요.", FirstReply = "안 받아도 무슨 내용인지 알아요. 내일 할 일을 오늘 봐 달라는 전화예요.\n급한 일은 아닌데 결국 받게 되더라고요.", RepeatReply = "또 회사네요. 이번에는 붕어빵 받을 때까지만 안 받으려고요." },
        },
    };

    public static CustomerStoryData Get(CustomerType customerType) => customerType == CustomerType.JeongHyun ? JeongHyun : null;
}
