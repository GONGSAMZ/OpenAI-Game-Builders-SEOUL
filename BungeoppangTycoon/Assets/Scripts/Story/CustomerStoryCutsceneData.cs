using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CustomerStoryCutsceneData
{
    public string Title;
    public string ResourcePath;
    public CustomerStoryCutsceneLine[] Lines;
    public bool IsUnlock;
}

[Serializable]
public sealed class CustomerStoryCutsceneLine
{
    public string Speaker;
    [TextArea] public string Text;

    public CustomerStoryCutsceneLine(string speaker, string text)
    {
        Speaker = speaker;
        Text = text;
    }
}

/// <summary>Figma 원본 컷씬 데이터입니다. 각 장면은 한 번의 입력으로 다음 그림으로 넘어갑니다.</summary>
public static class CustomerStoryCutsceneCatalog
{
    public static readonly CustomerStoryCutsceneData[] JeongHyunScenes =
    {
        Scene("삭제된 저녁", "StoryCutscenes/jeonghyeon/story-01", "정현의 평일", "퇴근 뒤 영화를 보려던 계획은 또 지워졌다.\n정현: “오늘만 끝내면 된다고 했는데…”"),
        Scene("끝나지 않는 자리", "StoryCutscenes/jeonghyeon/story-02", "정현", "모두가 떠난 사무실에서 정현만 다시 자리에 앉았다.\n휴대전화 알림은 계속 쌓였다."),
        Scene("슈크림붕의 각성", "StoryCutscenes/jeonghyeon/story-03", "정현 · 슈크림붕 영혼", "영혼은 휴대전화를 엎어 놓았다.\n“쉬는 시간도 네 일정이야. 먼저 지켜!”"),
        Scene("한 줄의 경계", "StoryCutscenes/jeonghyeon/story-04", "정현", "정현은 짧은 거절을 보내고 휴대전화를 가방에 넣었다.\n오늘의 한 시간을 처음으로 남겼다."),
        Scene("되찾은 한 시간", "StoryCutscenes/jeonghyeon/story-05", "정현의 다음 날", "정현은 동네 서점에서 우주 소설을 펼쳤다.\n내일 점심에도 읽을 책갈피를 꽂았다."),
        new()
        {
            Title = "되찾은 한 시간",
            ResourcePath = "StoryUnlocks/jeonghyeon/unlock",
            IsUnlock = true,
            Lines = new[] { new CustomerStoryCutsceneLine("", "칼퇴대장 크림붕 · 슈크림 + 바삭\n정현: “오늘 한 시간은 제가 쓰겠습니다.”") },
        },
    };

    private static readonly Dictionary<CustomerType, CustomerStoryCutsceneData[]> Scenes = new()
    {
        { CustomerType.JeongHyun, JeongHyunScenes },
        { CustomerType.HaYoung, Story("hajin",
            Scene("40초 안의 이야기", "중학교 1학년의 하진", "눈, 신호등, 우산, 붕어빵 김.\n하진은 평범한 길을 이어 40초의 이야기를 만들었다."),
            Scene("문제집 사이 스토리보드", "하진", "일정표에는 빈 시간이 없었다.\n하진의 장면은 문제집 사이에만 숨어 있었다."),
            Scene("다음 장면", "하진 · 민트붕 영혼", "영혼: “결말 말고, 다음 장면부터 네가 찍어!”\n하진은 먼저 자기 영상을 보여 주기로 했다."),
            Scene("첫 상영", "하진과 부모님", "하진: “40초만 끝까지 봐 주세요.”\n부모의 대답은 아직 미완성이지만 처음으로 작품을 보았다."),
            Scene("첫 번째 컷", "영화 동아리의 하진", "초점은 나갔고 모두 웃었다.\n하진은 실패를 지우지 않고 다시 “컷!”을 외쳤다."),
            Unlock("교과서 밖의 첫 장면", "샛길요정 민트붕 · 민트 + 말랑\n하진: “이번 장면은 제가 정해 볼래요.”")) },
        { CustomerType.MiJu, Story("miju",
            Scene("보내지 못한 한 표", "대학교 새내기 미주", "미주는 피자와 오래된 노래를 적었다가 지웠다.\n단체방에는 하트 하나만 남았다."),
            Scene("혼자만의 재생 목록", "미주", "작은 물고기 아이콘의 재생 목록은 미주만 알고 있었다.\n“아무도 모르면 무슨 표정을 짓지?”"),
            Scene("네 차례", "미주 · 피자붕 영혼", "영혼: “같이 있는 것과 똑같아지는 건 달라!”\n미주는 자기 메뉴부터 말하기로 했다."),
            Scene("첫 예약곡", "노래방의 미주", "미주: “이번에는 내가 먼저 고를래.”\n낯선 전주 속에서 떨리는 첫 소절이 시작됐다."),
            Scene("다른 후렴", "미주와 친구들", "모두의 취향은 같아지지 않았다.\n그래도 미주의 자리는 그대로 남았다."),
            Unlock("내가 먼저 고른 노래", "취향선언 피자붕 · 피자 + 바삭\n미주: “오늘은 제가 고른 걸 먹을래요.”")) },
        { CustomerType.Sunja, Story("sunja",
            Scene("겨울의 기억", "겨울의 기억", "남편: “그 천으로는 뭘 만들 거에요?”\n선자: “우리 둘이 같이 두를 긴 목도리요.”"),
            Scene("멈춘 바느질", "선자", "“고칠 곳이 남아 있는 동안에는…… 아직 끝난 게 아니니까.”\n그 사람이 떠난 뒤, 선자는 새 천을 펴지 않았다."),
            Scene("말차붕의 각성", "선자 · 말차붕 영혼", "선자: “이 맛…… 그날 사 두었던 천이 생각나네요.”\n영혼: “추억은 멈춰 서는 자리가 아니라, 오늘 들고 나갈 주머니야.”"),
            Scene("새로운 바느질", "선자 · 말차붕 영혼", "선자: “이 정도면…… 그 사람도 섭섭해하지 않겠지요.”\n영혼: “그럼! 이제 밖으로 출발!”"),
            Scene("앞으로 걷기", "현재의 선자", "이웃: “선자 할머니,오늘은 좀 같이 걸으실래요?”\n선자: “네. 오늘은 좀 멀리 가 봐요.”"),
            Unlock("주머니에 담아 가는 겨울", "오늘출발 말차붕 · 녹차 + 바삭\n선자: “오늘은 새 천을 꺼내 봐야겠어요.”")) },
        { CustomerType.Geonwoo, Story("geonwoo",
            Scene("냉장고 할 일 표", "의젓한 건우", "건우는 밥, 숙제, 문단속에 체크했다.\n걱정시키지 않으려고 전화 그림만 숨겼다."),
            Scene("뜯지 않은 놀이", "건우", "물고기 카드 게임은 포장된 채 교과서 밑에 들어갔다.\n창밖에서는 친구들이 손을 흔들었다."),
            Scene("규칙 없는 첫 판", "건우 · 초코붕 영혼", "영혼: “같이 웃으면 그게 놀이야!”\n규칙에 없던 엉터리 물고기 카드가 달아났다."),
            Scene("놀아도 되냐는 메시지", "놀이터 앞의 건우", "건우는 “바빠”라는 말을 멈췄다.\n30분 놀고 전화하겠다는 약속을 직접 보냈다."),
            Scene("져도 남는 카드", "건우와 친구들", "첫 판에 졌지만 카드는 다음 판의 특별 카드가 됐다.\n건우: “이 숙제는 같이 봐줄래?”"),
            Unlock("오늘만은 어린이", "놀자대장 초코붕 · 초코 + 말랑\n건우: “하나는 같이 노는 친구 거예요.”")) },
        { CustomerType.Taesu, Story("taesu",
            Scene("비어 버린 자리", "과거의 수리점", "딸이 도와주겠다고 나섰지만 태수는 손이 느리다며 핀잔부터 줬다.\n그 뒤로 딸이 앉던 의자는 계속 비어 있었다."),
            Scene("지웠다 쓴 문장", "태수", "미안하다는 문장을 썼다가 지웠다.\n길게 설명할수록 변명처럼 보여 휴대전화만 내려놓았다."),
            Scene("먼저 건넬 말", "태수 · 치즈붕 영혼", "따뜻한 속이 늘어나듯, 먼저 건넬 말 하나가 남았다.\n“그때 내가 너무 심했다. 미안하다.”"),
            Scene("보내기 버튼", "태수의 메시지", "태수는 고치지도 덧붙이지도 않고 짧은 문장을 보냈다.\n읽음 표시 뒤에는 한동안 답이 없었다."),
            Scene("다시 온 주말", "태수와 딸", "주말 저녁, 딸이 수리점 문을 조심스럽게 열었다.\n태수는 공구를 내려놓고 빈 의자를 먼저 당겨 주었다."),
            Unlock("먼저 건넨 말", "먼저말해 치즈붕 · 크림치즈 + 말랑\n태수: “오늘은 두 개 포장해 주시죠.”")) },
        { CustomerType.Nari, Story("nari",
            Scene("한 건만 더", "끝나지 않는 배달", "배달 앱은 ‘한 건만 더’를 계속 띄웠다.\n나리는 저녁 약속 시간을 확인하고도 다음 주문을 잡았다."),
            Scene("닫힌 저녁", "나리", "식당의 빈 의자 사진이 도착했을 때도 배달 알림은 울렸다.\n미안하다는 답장은 신호 대기 중에 짧게 끝났다."),
            Scene("오늘의 종료 시간", "나리 · 고구붕 영혼", "붕어빵을 가르자 고구붕 영혼이 작은 종료 시계를 내밀었다.\n“오늘의 일은 오늘 네가 끝내도 돼.”"),
            Scene("앱을 끄는 손", "오늘의 나리", "나리는 보너스 주문을 넘기고 앱의 종료 버튼을 눌렀다.\n처음으로 약속 시간보다 먼저 가게 앞에 도착했다."),
            Scene("약속에 맞춘 도착", "며칠 뒤의 나리", "모든 저녁이 달라지진 않았다.\n그래도 약속한 날만큼은 헬멧을 벗고 제시간에 자리에 앉았다."),
            Unlock("저녁을 지킨 날", "저녁지킴 고구붕 · 고구마 + 노릇\n나리: “오늘은 여기서 먹고 갈게요.”")) },
        { CustomerType.Junho, Story("junho",
            Scene("반으로 나눠 먹던 날", "어린 준호와 친구", "둘은 어릴 때부터 같은 링크를 돌았다.\n붕어빵 하나도 반으로 나눠 먹으며 함께 대표가 되자고 말했다."),
            Scene("한 사람만 남은 링크", "링크 밖의 준호", "부상 뒤 준호만 링크 밖에 남았고, 친구의 첫 국가대표 선발 소식이 떴다.\n축하해야 한다는 생각과 먼저 들킨 질투가 함께 올라왔다."),
            Scene("두 마음이 나온 붕어빵", "준호 · 팥붕 영혼", "붕어빵을 가르자 팥붕 영혼이 두 색 목도리를 내밀었다.\n기쁜 마음과 서운한 마음은 한꺼번에 있어도 된다고 했다."),
            Scene("늦게 받은 전화", "준호의 전화", "친구의 전화가 다시 울렸을 때 준호는 이번에는 받았다.\n“축하해. 근데… 솔직히 좀 배 아프다.”"),
            Scene("다시 반으로", "며칠 뒤의 두 사람", "둘은 말없이 붕어빵을 반으로 나눴다.\n예전처럼 완벽하진 않았지만, 대화는 다시 이어졌다."),
            Unlock("같이 남은 마음", "두마음 팥붕 · 팥 + 말랑\n준호: “이건 반으로 잘라 주세요.”")) },
    };

    public static CustomerStoryCutsceneData[] Get(CustomerType customerType) =>
        Scenes.TryGetValue(customerType, out CustomerStoryCutsceneData[] scenes)
            ? scenes
            : Array.Empty<CustomerStoryCutsceneData>();

    private static CustomerStoryCutsceneData Scene(string title, string resourcePath, string speaker, string text) => new()
    {
        Title = title,
        ResourcePath = resourcePath,
        Lines = new[] { new CustomerStoryCutsceneLine(speaker, text) },
    };


private static CustomerStoryCutsceneData[] Story(string folder, params CustomerStoryCutsceneData[] scenes)
    {
        for (int index = 0; index < scenes.Length; index++)
        {
            scenes[index].ResourcePath = index < 5
                ? $"StoryCutscenes/{folder}/story-{index + 1:00}"
                : $"StoryUnlocks/{folder}/unlock";
        }
        return scenes;
    }

    private static CustomerStoryCutsceneData Scene(string title, string speaker, string text) => new()
    {
        Title = title,
        Lines = new[] { new CustomerStoryCutsceneLine(speaker, text) },
    };

    private static CustomerStoryCutsceneData Unlock(string title, string text) => new()
    {
        Title = title,
        IsUnlock = true,
        Lines = new[] { new CustomerStoryCutsceneLine(string.Empty, text) },
    };
}
