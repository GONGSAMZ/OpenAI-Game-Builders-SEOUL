using System;
using UnityEngine;

[Serializable]
public sealed class CustomerStoryCutsceneData
{
    public string Title;
    public string ResourcePath;
    public CustomerStoryCutsceneLine[] Lines;
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

/// <summary>정현 컷씬의 그림 경로와 클릭 대사를 제공합니다.</summary>
public static class CustomerStoryCutsceneCatalog
{
    public static readonly CustomerStoryCutsceneData[] JeongHyunScenes =
    {
        new()
        {
            Title = "삭제된 저녁",
            ResourcePath = "StoryCutscenes/jeonghyeon/story-01",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("", "정현의 약속은 늘 다른 사람의 급한 일보다 먼저 지워졌다."),
                new CustomerStoryCutsceneLine("정현", "이번 한 번만 도와주면 되니까."),
            },
        },
        new()
        {
            Title = "끝나지 않는 자리",
            ResourcePath = "StoryCutscenes/jeonghyeon/story-02",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("정현", "제가 안 하면 누군가는 곤란해질 텐데……"),
                new CustomerStoryCutsceneLine("", "그는 부탁받지 않은 책임까지 자기 자리 위에 올려놓았다."),
            },
        },
        new()
        {
            Title = "슈크림붕의 각성",
            ResourcePath = "StoryCutscenes/jeonghyeon/story-03",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("칼퇴대장 크림붕", "끝나지 않는 일은 두고 가면 돼. 넌 끝날 시간이 있잖아!"),
                new CustomerStoryCutsceneLine("정현", "두고 가도…… 되는 일이었군요."),
                new CustomerStoryCutsceneLine("", "따뜻한 빛이 회색 알림창을 밀어내고, 정현의 손에 짧은 틈을 만들었다."),
            },
        },
        new()
        {
            Title = "한 줄의 경계",
            ResourcePath = "StoryCutscenes/jeonghyeon/story-04",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("정현", "내일의 저에게…… 일을 하나 남겨 보겠습니다."),
                new CustomerStoryCutsceneLine("", "정현은 ‘오늘은 퇴근했습니다. 내일 오전에 확인할게요.’라고 보내고 휴대폰을 가방에 넣었다."),
            },
        },
        new()
        {
            Title = "되찾은 한 시간",
            ResourcePath = "StoryCutscenes/jeonghyeon/story-05",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("", "정현은 늦게까지 여는 동네 서점에서 오래전 좋아했던 우주 소설을 골랐다."),
                new CustomerStoryCutsceneLine("", "그날 정현이 되찾은 것은 거창한 휴가가 아니라, 누구에게도 빌려주지 않은 한 시간이었다."),
            },
        },
        new()
        {
            Title = "이야기 해금",
            ResourcePath = "StoryUnlocks/jeonghyeon/unlock",
            Lines = new[]
            {
                new CustomerStoryCutsceneLine("", "정현의 이야기 해금"),
                new CustomerStoryCutsceneLine("", "내일의 나에게 남겨 두기"),
                new CustomerStoryCutsceneLine("", "칼퇴대장 크림붕 · 슈크림 + 바삭"),
                new CustomerStoryCutsceneLine("정현", "오늘은 알림을 끄고 먹으려고요."),
            },
        },
    };
}
