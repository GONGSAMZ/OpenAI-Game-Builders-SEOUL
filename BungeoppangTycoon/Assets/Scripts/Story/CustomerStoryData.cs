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

/// <summary>8명 손님의 낮 대화와 특별 주문 정답을 제공한다.</summary>
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

    private static readonly CustomerStoryData HaJin = Story(CustomerType.HaYoung, "하진", FillingType.mint, QualityStatus.soft,
        "시험 범위는 정해져 있는데, 제가 찍고 싶은 장면은 어디에도 적을 칸이 없어요.\n오늘은 고르지 않을게요. 평소 먹던 걸로 주세요.",
        "샛길요정 민트붕이 말했어요.\n‘샛길은 틀린 길이 아니라 아직 네가 표시하지 않은 길이야.’\n하진은 가게의 김과 골목 불빛을 찍어 첫 영화를 만들었어요.",
        "계획을 바꿔도 괜찮다고 말해 줄 조금 더 느긋한 친구가 필요해요.", "다음에 다시 와서 장면을 고를게요.",
        ("공원 촬영은요?", "사람들이 지나가는 것도 다 장면 같아요. 그런데 학원 시간이 되면 카메라를 넣어야 해요.", "오늘은 빛이 좋았는데, 다음에 찍어야겠어요."),
        ("학원 가는 길이 싫어요?", "공부가 싫은 건 아니에요. 다만 제가 좋아하는 건 늘 나중으로 밀려요.", "오늘도 콘티는 가방 맨 아래예요."),
        ("작은 수첩엔 뭐가 있어요?", "영화 콘티요. 문제집 사이에 숨기면 조금 구겨져도 아무도 못 봐요.", "아직 마지막 장면은 비워 뒀어요."));

    private static readonly CustomerStoryData MiJu = Story(CustomerType.MiJu, "미주", FillingType.pizza, QualityStatus.crisp,
        "친구들이 제가 좋아할 것까지 먼저 정해 줘요.\n제가 뭘 좋아했는지는 자꾸 잊어버려요. 오늘도 다들 좋아하는 걸로 주세요.",
        "취향선언 피자붕이 말했어요.\n‘같이 있는 것과 똑같아지는 건 다른 일이야. 네 메뉴부터 말해!’\n미주는 자기 노래와 간식을 먼저 골랐어요.",
        "제가 좋아하는 걸 고르는 맛은 맞는 것 같아요. 다음에는 조금 더 단호하게 말해 보고 싶어요.", "다음에 다시 제 취향을 찾아볼게요.",
        ("단체 주문할 때는요?", "다들 괜찮다는데, 저는 제가 뭘 원하는지 물어보지도 못했어요.", "오늘도 친구들 메뉴를 먼저 떠올렸어요."),
        ("미주 씨는 뭘 좋아해요?", "글쎄요… 초코도 좋고 다른 것도 좋아요. 너무 오래 생각하면 분위기가 이상해질까 봐요.", "다음에는 하나쯤 먼저 말해 볼까요."),
        ("아까 흥얼거리던 노래는요?", "친구들이 별로래서 혼자 들을 때만 들어요. 이상하게 들릴까 봐요.", "그래도 들으면 기분은 좋아져요."));

    private static readonly CustomerStoryData Sunja = Story(CustomerType.Sunja, "선자", FillingType.greenTea, QualityStatus.crisp,
        "이 코트를 다 고치면 정말 보낼 곳이 없어질 것 같아서요.\n새 천을 꺼내면 그 사람을 잊는 것 같고요.",
        "오늘출발 말차붕이 말했어요.\n‘추억은 멈춰 서는 자리가 아니라, 오늘 들고 나갈 주머니야.’\n선자는 새 천으로 자기 목도리를 만들기 시작했어요.",
        "기억을 안 잊고도 새로 시작하는 맛이 조금 더 필요하겠어요.", "오늘은 이 코트와 함께 다시 생각해 볼게요.",
        ("늘 두 개씩 사는 이유가 있나요?", "예전엔 둘이 나눠 먹었지요. 하나만 사면 손이 자꾸 비어요.", "오늘도 습관처럼 두 개를 보게 되네요."),
        ("소매에 붙은 실은요?", "오래된 실은 쉽게 놓이지 않아요. 그래서 자꾸 다시 꿰매게 돼요.", "손볼 곳이 남아 있으면 마음이 놓여요."),
        ("목에 건 줄자는 오래 쓰셨어요?", "수선 일을 할 때부터요. 새 천은 아직 꺼낼 이유가 없네요.", "줄자는 오늘도 길이를 재고 있어요."));

    private static readonly CustomerStoryData Geonwoo = Story(CustomerType.Geonwoo, "건우", FillingType.nutella, QualityStatus.soft,
        "혼자서도 잘할 수 있는데… 가끔은 그냥 놀고 싶다고 말해도 될까요?\n오늘은 달콤한 걸로 하나 주세요.",
        "놀자대장 초코붕이 말했어요.\n‘도움받는 건 못하는 게 아니라 같이 노는 방법이야!’\n건우는 친구에게 먼저 카드 놀이를 하자고 말했어요.",
        "조금 더 마음 편하게 놀 수 있는 친구가 필요해요.", "다음에는 같이 노는 법을 더 배워 볼게요.",
        ("가방이 무겁지 않아요?", "괜찮아요. 제가 들어야 동생도 편하니까요.", "오늘도 혼자 들 수 있어요."),
        ("쉬는 시간엔 뭐 해요?", "숙제요. 친구들이 놀자고 해도 나중에 한다고 했어요.", "놀이는 나중에도 할 수 있잖아요."),
        ("도와달라고 해 본 적 있어요?", "제가 하면 빨라요. 부탁하면 귀찮게 하는 것 같아요.", "그래도 가끔은 손이 모자라요."));

    private static readonly CustomerStoryData Taesu = Story(CustomerType.Taesu, "태수", FillingType.creamCheese, QualityStatus.soft,
        "망가진 건 부품을 바꾸면 되는데, 사람 마음은 부품이 없더군요.\n전해 줄 말이 아직 고장 난 것 같습니다.",
        "먼저안아 치즈붕이 말했어요.\n‘마음은 새는 게 아니라 건네는 거야. 뚜껑부터 열어.’\n태수는 변명 없이 먼저 미안하다고 말했어요.",
        "말을 조금 더 부드럽게 꺼낼 용기가 필요하겠군요.", "다음에는 고치기 전에 먼저 말해 보겠습니다.",
        ("라디오는 다 고치셨어요?", "안테나까지 다 봤지. 그런데 포장을 묶었다 풀었다만 했네.", "물건은 고치면 되는데 말은 어렵군."),
        ("누구에게 전할 물건이에요?", "딸애가 쓰던 거야. 전해 줄 게 있어서 고친 건데.", "쓸데없는 말은 안 하는 편이라서."),
        ("쪽지는 왜 지우셨어요?", "제대로 정리해서 말해야 한다고 생각했지. 그래서 한 줄도 못 보냈어.", "오늘도 시작만 고쳤네."));

    private static readonly CustomerStoryData Nari = Story(CustomerType.Nari, "나리", FillingType.sweetPotato, QualityStatus.perfect,
        "지도에 저장한 곳은 많은데, 다시 가는 곳은 없네요.\n먼저 떠나면 기다릴 일도 없어서 편하거든요.",
        "한자리 고구붕이 말했어요.\n‘머무는 건 갇히는 게 아니야. 돌아왔을 때 불이 켜진 곳을 만드는 거야.’\n나리는 다음 계절까지 돌아올 주소를 남겼어요.",
        "오늘의 약속을 버리지 않을 만큼 따뜻한 친구가 필요해요.", "다음에는 같은 길로 다시 와 볼게요.",
        ("오늘 저녁 약속은요?", "갈까 말까 하다가 다른 지역 구인 공고를 보고 있었어요.", "좋아지기 전에 옮기면 편하잖아요."),
        ("헬멧 지도 스티커는요?", "간 곳마다 붙였는데 다시 간 곳은 별로 없네요.", "떼려고 하다가 그냥 뒀어요."),
        ("집에는 잘 쉬고 있어요?", "상자는 아직 책상으로 쓰고 있어요. 금방 또 옮길 수도 있으니까요.", "짐을 풀면 괜히 정이 들까 봐요."));

    private static readonly CustomerStoryData Junho = Story(CustomerType.Junho, "준호", FillingType.redBean, QualityStatus.soft,
        "오늘은 축하 전화를 해야 하는데 자꾸 미루게 되네요.\n바삭한 것보다 좀 부드러운 걸로 부탁할게요.",
        "두마음 팥붕이 말했어요.\n‘축하하는 마음도 네 거고, 보기 싫은 마음도 네 거야.’\n준호는 친구의 전화를 받고 솔직한 축하를 건넸어요.",
        "두 마음을 같이 들고 갈 만큼 부드러운 친구가 필요해요.", "다음에는 전화를 피하지 않고 받아 볼게요.",
        ("경기 결과는 봤어요?", "결과는 봤는데 영상은 아직 못 봤어요. 이상하게 손이 안 가요.", "다음 경기 소식도 알림으로만 봤어요."),
        ("전화 안 받아도 괜찮아요?", "받아야 하는 걸 아는데, 지금은 무슨 표정을 해야 할지 모르겠어요.", "진동만 꺼 놨어요."),
        ("집에 바로 안 가세요?", "조금 더 걷다가 가려고요. 링크 앞을 지나면 생각이 많아져서요.", "오늘도 발걸음이 늦네요."));

    private static readonly CustomerStoryData[] All = { JeongHyun, HaJin, MiJu, Sunja, Geonwoo, Taesu, Nari, Junho };

    public static CustomerStoryData Get(CustomerType customerType)
    {
        foreach (CustomerStoryData story in All)
            if (story.CustomerType == customerType) return story;
        return null;
    }

    public static System.Collections.Generic.IReadOnlyList<CustomerStoryData> AllStories => All;

    private static CustomerStoryData Story(CustomerType type, string name, FillingType filling, QualityStatus bake, string intro, string success, string nearMiss, string failure, params (string choice, string first, string repeat)[] topics)
    {
        StoryTalkTopic[] mapped = new StoryTalkTopic[topics.Length];
        for (int i = 0; i < topics.Length; i++)
            mapped[i] = new StoryTalkTopic { Choice = topics[i].choice, FirstReply = topics[i].first, RepeatReply = topics[i].repeat };
        return new CustomerStoryData { CustomerType = type, DisplayName = name, RequiredFilling = filling, RequiredBake = bake, SpecialIntro = intro, SuccessMessage = success, NearMissMessage = nearMiss, FailureMessage = failure, Topics = mapped };
    }
}
