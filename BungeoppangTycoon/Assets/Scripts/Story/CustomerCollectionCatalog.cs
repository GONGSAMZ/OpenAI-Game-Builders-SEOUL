using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>도감 화면에 필요한 손님 표시 정보를 한곳에서 제공합니다.</summary>
public sealed class CustomerCollectionEntry
{
    public CustomerType CustomerType;
    public string DisplayName;
    public int Age;
    public string Job;
    public string PlayerIntroduction;
    public string SpritePath;
    public string StoryTitle;
    public string StorySoulName;
    public string StoryFolder;
}

public static class CustomerCollectionCatalog
{
    private static readonly CustomerCollectionEntry[] entries =
    {
        Entry(CustomerType.JeongHyun, "정현", 32, "회사원", "퇴근길마다 들르는 정현 씨예요. 오늘도 자기 몫의 시간을 조금 남길 수 있을까요?", "Customers/01_JeongHyun", "내일의 나에게 남겨 두기", "칼퇴대장 크림붕", "jeonghyeon"),
        Entry(CustomerType.HaYoung, "하진", 15, "중학생·영화감독 지망", "카메라와 스토리보드 노트를 챙겨 오는 하진이에요. 오늘은 어떤 장면을 상상하고 있을까요?", "Customers/02_HaYoung", "교과서 밖의 첫 장면", "샛길요정 민트붕", "hajin"),
        Entry(CustomerType.MiJu, "미주", 21, "대학생", "이어폰을 낀 미주가 붕어빵을 고르고 있어요. 미주가 정말 좋아하는 건 무엇일까요?", "Customers/03_MiJu", "내가 먼저 고른 노래", "취향선언 피자붕", "miju"),
        Entry(CustomerType.Sunja, "선자", 68, "은퇴한 재봉사", "수선함을 든 선자 씨예요. 오래된 물건을 고치듯, 어떤 시간을 붙잡고 있을까요?", "Customers/04_Sunja", "주머니에 담아 가는 겨울", "오늘출발 말차붕", "sunja"),
        Entry(CustomerType.Geonwoo, "건우", 11, "초등학생", "혼자서도 잘한다고 말하는 건우예요. 가방 속에 좋아하는 놀이가 숨어 있을지도 몰라요.", "Customers/05_Geonwoo", "오늘만은 어린이", "놀자대장 초코붕", "geonwoo"),
        Entry(CustomerType.Taesu, "태수", 47, "수리점 운영자", "공구 가방을 든 태수 아저씨예요. 보내지 못한 말이 있는 듯해요.", "Customers/06_Taesu", "고친 라디오에 남은 말", "먼저안아 치즈붕", "taesu"),
        Entry(CustomerType.Nari, "나리", 27, "배달 라이더", "헬멧을 든 나리가 잠시 멈춰 섰어요. 오늘 저녁 약속에는 갈 수 있을까요?", "Customers/07_Nari", "다시 돌아올 주소", "한자리 고구붕", "nari"),
        Entry(CustomerType.Junho, "준호", 24, "전직 스피드 스케이팅 선수", "운동 재킷 차림의 준호예요. 누군가의 좋은 소식을 쉽게 꺼내지 못하는 것 같아요.", "Customers/08_Junho", "축하한다고 말하기까지", "두마음 팥붕", "junho"),
    };

    public static IReadOnlyList<CustomerCollectionEntry> Entries => entries;

    public static CustomerCollectionEntry Get(CustomerType customerType)
    {
        foreach (CustomerCollectionEntry entry in entries)
            if (entry.CustomerType == customerType) return entry;
        return null;
    }

    private static CustomerCollectionEntry Entry(CustomerType type, string name, int age, string job, string introduction, string spritePath, string storyTitle, string soulName, string storyFolder)
    {
        return new CustomerCollectionEntry
        {
            CustomerType = type,
            DisplayName = name,
            Age = age,
            Job = job,
            PlayerIntroduction = introduction,
            SpritePath = spritePath,
            StoryTitle = storyTitle,
            StorySoulName = soulName,
            StoryFolder = storyFolder,
        };
    }
}

/// <summary>손님이 실제로 등장한 뒤에만 상세 정보를 보여 주기 위한 작은 저장소입니다.</summary>
public static class CustomerCollectionProgress
{
    public static event Action Changed
    {
        add
        {
            SaveService.Service.DataChanged += value;
        }
        remove
        {
            if (SaveService.Instance != null)
                SaveService.Instance.DataChanged -= value;
        }
    }

    public static void MarkMet(CustomerType customerType) => SaveService.Service.MarkCustomerMet(customerType);

    public static bool HasMet(CustomerType customerType) => SaveService.Service.HasMet(customerType);
}
