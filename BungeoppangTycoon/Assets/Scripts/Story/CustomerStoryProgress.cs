using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
internal sealed class CustomerStorySaveData
{
    public bool hasStartedStoryGame;
    public int lastTalkDay;
    public List<int> completedTopicIndexes = new();
    public int specialOrderDueDay = -1;
    public int retryAvailableDay = -1;
    public bool storyCompleted;
}

/// <summary>PlayerPrefs에 저장되는 정현 이야기 진행 상태와 특별 주문 예약을 관리합니다.</summary>
public static class CustomerStoryProgress
{
    private const string SaveKey = "customer_story_jeonghyun_v1";
    private static CustomerStorySaveData save;
    private static bool guaranteedCustomerSpawned;

    private static CustomerStorySaveData SaveData
    {
        get
        {
            if (save != null) return save;
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            save = string.IsNullOrWhiteSpace(json) ? new CustomerStorySaveData() : JsonUtility.FromJson<CustomerStorySaveData>(json) ?? new CustomerStorySaveData();
            save.completedTopicIndexes ??= new List<int>();
            return save;
        }
    }

    public static CustomerStoryData ActiveStory { get; private set; }
    public static bool IsSpecialOrderActive { get; private set; }
    public static bool IsStoryCompleted => SaveData.storyCompleted;
    public static int SpecialOrderDueDay => SaveData.specialOrderDueDay;
    public static int RetryAvailableDay => SaveData.retryAvailableDay;

    public static void InitializeGame()
    {
        guaranteedCustomerSpawned = false;
        IsSpecialOrderActive = false;
        int previousLastTalkDay = SaveData.lastTalkDay;

        // 게임 날짜는 새 게임마다 다시 시작하므로 이전 플레이의 날짜와 비교하면 안 됩니다.
        // 완료한 대화 주제는 유지하고, "하루 한 번" 제한에 쓰는 날짜만 새 플레이에 맞게 초기화합니다.
        SaveData.lastTalkDay = -1;

        CustomerStoryData jeongHyun = CustomerStoryCatalog.Get(CustomerType.JeongHyun);
        bool fillingAvailable = IsFillingAvailable(jeongHyun.RequiredFilling);
        ActiveStory = SaveData.storyCompleted || !fillingAvailable ? null : jeongHyun;
        SaveData.hasStartedStoryGame = true;
        Persist();
        Debug.Log(
            $"[손님 이야기] 게임 이야기 상태 초기화 | 현재 날짜={Managers.Game.Day}일차" +
            $" | 활성 이야기={(ActiveStory != null ? ActiveStory.DisplayName : "없음")}" +
            $" | 이야기 완료={(SaveData.storyCompleted ? "예" : "아니요")}" +
            $" | 필요한 맛={Define.FillingText[(int)jeongHyun.RequiredFilling]}" +
            $" | 필요한 맛 사용 가능={(fillingAvailable ? "예" : "아니요")}" +
            $" | 이전 플레이 마지막 대화 날짜={(previousLastTalkDay > 0 ? previousLastTalkDay + "일차" : "없음")}" +
            $" | 새 게임 대화 제한 초기화=예" +
            $" | 완료한 대화={SaveData.completedTopicIndexes.Count}/{jeongHyun.Topics.Length}" +
            $" | 특별 주문 예정일={(SaveData.specialOrderDueDay > 0 ? SaveData.specialOrderDueDay + "일차" : "없음")}" +
            $" | 재도전 가능일={(SaveData.retryAvailableDay > 0 ? SaveData.retryAvailableDay + "일차" : "없음")}");
    }

    public static void BeginDay(int day)
    {
        if (SaveData.lastTalkDay != day)
            Persist();
    }

    public static bool TryGetGuaranteedCustomer(int totalCustomers, out CustomerType customerType)
    {
        customerType = default;
        if (ActiveStory == null || guaranteedCustomerSpawned || totalCustomers >= 3)
        {
            Debug.Log(
                $"[손님 이야기] 이야기 손님 우선 등장 생략" +
                $" | 활성 이야기 존재={(ActiveStory != null ? "예" : "아니요")}" +
                $" | 이미 우선 등장함={(guaranteedCustomerSpawned ? "예" : "아니요")}" +
                $" | 현재까지 등장한 손님 수={totalCustomers}명");
            return false;
        }
        guaranteedCustomerSpawned = true;
        customerType = ActiveStory.CustomerType;
        Debug.Log(
            $"[손님 이야기] 특별 대화 대상 손님으로 {ActiveStory.DisplayName} 선택" +
            $" | 현재까지 등장한 손님 수={totalCustomers}명");
        return true;
    }

    public static bool IsTalkTarget(CustomerType customerType) => ActiveStory != null && !SaveData.storyCompleted && ActiveStory.CustomerType == customerType;
    public static bool CanTalkToday(CustomerType customerType) => IsTalkTarget(customerType) && SaveData.lastTalkDay != Managers.Game.Day;
    public static IReadOnlyCollection<int> CompletedTopics => SaveData.completedTopicIndexes;

    public static bool CompleteTalkTopic(CustomerType customerType, int topicIndex)
    {
        if (!CanTalkToday(customerType) || ActiveStory == null || topicIndex < 0 || topicIndex >= ActiveStory.Topics.Length)
        {
            Debug.LogWarning(
                $"[손님 이야기] 대화 주제 완료 처리 차단 | 선택지 번호={topicIndex + 1}" +
                $" | {GetTalkDebugState(customerType)}");
            return false;
        }
        bool isNew = !SaveData.completedTopicIndexes.Contains(topicIndex);
        SaveData.lastTalkDay = Managers.Game.Day;
        if (isNew) SaveData.completedTopicIndexes.Add(topicIndex);
        if (SaveData.completedTopicIndexes.Count >= ActiveStory.Topics.Length && SaveData.specialOrderDueDay < 0 && IsFillingAvailable(ActiveStory.RequiredFilling))
            SaveData.specialOrderDueDay = Managers.Game.Day + 1;
        Persist();
        Debug.Log(
            $"[손님 이야기] 대화 주제 완료 | 손님={ActiveStory.DisplayName} | 선택지 번호={topicIndex + 1}" +
            $" | 처음 들은 주제={(isNew ? "예" : "아니요")}" +
            $" | 완료한 대화={SaveData.completedTopicIndexes.Count}/{ActiveStory.Topics.Length}" +
            $" | 마지막 대화 날짜={SaveData.lastTalkDay}일차" +
            $" | 특별 주문 예정일={(SaveData.specialOrderDueDay > 0 ? SaveData.specialOrderDueDay + "일차" : "없음")}");
        return isNew;
    }

    public static string GetTalkDebugState(CustomerType customerType)
    {
        bool hasActiveStory = ActiveStory != null;
        bool isTarget = hasActiveStory && !SaveData.storyCompleted && ActiveStory.CustomerType == customerType;
        bool alreadyTalkedToday = SaveData.lastTalkDay == Managers.Game.Day;
        bool canTalk = isTarget && !alreadyTalkedToday;
        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        return $"손님={customerName} | 현재 날짜={Managers.Game.Day}일차" +
            $" | 활성 이야기={(hasActiveStory ? ActiveStory.DisplayName : "없음")}" +
            $" | 이야기 완료={(SaveData.storyCompleted ? "예" : "아니요")}" +
            $" | 이야기 대상 손님={(isTarget ? "예" : "아니요")}" +
            $" | 마지막 대화 날짜={(SaveData.lastTalkDay > 0 ? SaveData.lastTalkDay + "일차" : "없음")}" +
            $" | 오늘 이미 대화함={(alreadyTalkedToday ? "예" : "아니요")}" +
            $" | 지금 대화 가능={(canTalk ? "예" : "아니요")}";
    }

    public static bool IsSpecialOrderDue()
    {
        return ActiveStory != null && !SaveData.storyCompleted && !IsSpecialOrderActive && SaveData.specialOrderDueDay == Managers.Game.Day &&
            (SaveData.retryAvailableDay < 0 || Managers.Game.Day >= SaveData.retryAvailableDay);
    }

    public static void BeginSpecialOrder() => IsSpecialOrderActive = true;

    /// <summary>특별 주문 결과를 저장하고, 성공 여부를 호출자에게 돌려줍니다.</summary>
    public static bool ResolveSpecialOrder(FillingType filling, QualityStatus bake)
    {
        if (ActiveStory == null) return false;
        bool fillingMatch = filling == ActiveStory.RequiredFilling;
        bool bakeMatch = bake == ActiveStory.RequiredBake;
        if (fillingMatch && bakeMatch)
        {
            SaveData.storyCompleted = true;
            SaveData.specialOrderDueDay = -1;
            SaveData.retryAvailableDay = -1;
            Persist();
            return true;
        }
        else
        {
            SaveData.specialOrderDueDay = -1;
            SaveData.retryAvailableDay = Managers.Game.Day + 2;
            CustomerStoryOverlay.ShowResult(ActiveStory.DisplayName, fillingMatch || bakeMatch ? ActiveStory.NearMissMessage : ActiveStory.FailureMessage, false);
        }
        Persist();
        return false;
    }

    /// <summary>손님이 화면에서 완전히 퇴장한 뒤에만 특별 주문 상태를 끝냅니다.</summary>
    public static void CompleteSpecialOrderSession()
    {
        IsSpecialOrderActive = false;
    }

    /// <summary>에디터에서 특별 주문 흐름을 처음부터 시험할 때만 사용합니다.</summary>
    public static void ResetForDebug()
    {
        save = new CustomerStorySaveData();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        ActiveStory = null;
        IsSpecialOrderActive = false;
        guaranteedCustomerSpawned = false;
        Debug.Log("[손님 이야기] 정현 이야기 진행도를 초기화했습니다.");
    }

    private static bool IsFillingAvailable(FillingType filling) => (int)filling < Managers.Game.NumOfFilling;
    private static void Persist() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(SaveData)); PlayerPrefs.Save(); }
}
