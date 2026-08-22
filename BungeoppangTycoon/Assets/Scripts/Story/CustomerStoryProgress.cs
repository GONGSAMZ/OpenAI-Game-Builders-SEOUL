using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>계정별 SaveService 데이터를 기준으로 손님 이야기 진행과 특별 주문 상태를 관리합니다.</summary>
public static class CustomerStoryProgress
{
    private static bool guaranteedCustomerSpawned;

    // SaveService.Data는 서비스 생성 전 첫 프레임에도 런타임 인스턴스를 준비합니다.
    // 기존 GetCustomer 경로는 계정 목록이 아직 초기화되지 않은 경우의 안전망입니다.
    private static CustomerProgressData SaveData => SaveService.Data.account.customers.Find(
        value => value.customerId == SaveIds.Customer(CustomerType.JeongHyun))
        ?? SaveService.Instance.GetCustomer(CustomerType.JeongHyun);

    // 당일 대화·특별 주문 날짜는 계정 수집 기록이 아니라 현재 영업 회차에 속한다.
    private static CustomerStoryRunState RunState =>
        SaveService.Instance.GetCustomerStoryRunState(CustomerType.JeongHyun);

    public static event Action Changed
    {
        add
        {
            SaveService.Instance.DataChanged += value;
        }
        remove
        {
            if (SaveService.Instance != null)
                SaveService.Instance.DataChanged -= value;
        }
    }

    public static CustomerStoryData ActiveStory { get; private set; }
    public static bool IsSpecialOrderActive { get; private set; }
    public static bool IsStoryCompleted => IsStoryCompletedFor(CustomerType.JeongHyun);
    public static int SpecialOrderDueDay => RunState.nextSpecialOrderDay;
    public static int RetryAvailableDay => RunState.nextSpecialOrderDay;
    public static IReadOnlyCollection<int> CompletedTopics =>
        CompletedTopicsFor(CustomerType.JeongHyun);

    /// <summary>해당 손님에게 아직 처음 듣지 않은 유효한 대화 주제가 남아 있는지 확인합니다.</summary>
    public static bool HasRemainingTopics(CustomerType customerType)
    {
        CustomerStoryData story = CustomerStoryCatalog.Get(customerType);
        if (story?.Topics == null || story.Topics.Length == 0)
            return false;

        return CompletedTopicsFor(customerType).Count < story.Topics.Length;
    }

    public static bool IsStoryCompletedFor(CustomerType customerType) =>
        SaveService.Instance.GetCustomer(customerType).storyCompleted;

    public static IReadOnlyCollection<int> CompletedTopicsFor(CustomerType customerType)
    {
        CustomerStoryData story = CustomerStoryCatalog.Get(customerType);
        CustomerProgressData progress = SaveService.Instance.GetCustomer(customerType);
        if (story == null || progress.completedTopicIds == null)
            return Array.Empty<int>();

        List<int> completed = new();
        for (int index = 0; index < story.Topics.Length; index++)
            if (progress.completedTopicIds.Contains(SaveIds.Topic(index)))
                completed.Add(index);
        return completed;
    }

    public static void InitializeGame()
    {
        guaranteedCustomerSpawned = false;
        IsSpecialOrderActive = false;
        int previousLastTalkDay = RunState.lastTalkDay;

        CustomerStoryData jeongHyun = CustomerStoryCatalog.Get(CustomerType.JeongHyun);
        bool fillingAvailable = jeongHyun != null && IsFillingAvailable(jeongHyun.RequiredFilling);
        RefreshActiveStory();
        Persist();

        Debug.Log(
            $"[손님 이야기] 게임 이야기 상태 초기화 | 현재 날짜={Managers.Game.Day}일차" +
            $" | 활성 이야기={(ActiveStory != null ? ActiveStory.DisplayName : "없음")}" +
            $" | 이야기 완료={(SaveData.storyCompleted ? "예" : "아니요")}" +
            $" | 필요한 맛={(jeongHyun != null ? Define.FillingText[(int)jeongHyun.RequiredFilling] : "없음")}" +
            $" | 필요한 맛 사용 가능={(fillingAvailable ? "예" : "아니요")}" +
            $" | 이전 플레이 마지막 대화 날짜={(previousLastTalkDay > 0 ? previousLastTalkDay + "일차" : "없음")}" +
            $" | 같은 날 재접속 대화 제한 유지=예" +
            $" | 완료한 대화={CompletedTopics.Count}/{(jeongHyun != null ? jeongHyun.Topics.Length : 0)}" +
            $" | 특별 주문 예정일={(RunState.nextSpecialOrderDay > 0 ? RunState.nextSpecialOrderDay + "일차" : "없음")}");
    }

    public static void BeginDay(int day)
    {
        // 이야기 손님 우선 등장은 게임 실행 전체가 아니라 하루마다 한 번 판정해야 한다.
        guaranteedCustomerSpawned = false;
    }

    public static bool TryGetGuaranteedCustomer(int totalCustomers, out CustomerType customerType)
    {
        customerType = default;
        bool hasRemainingTopics = ActiveStory != null && HasRemainingTopics(ActiveStory.CustomerType);
        bool specialOrderScheduled = RunState.nextSpecialOrderDay >= 0;
        if (!hasRemainingTopics || specialOrderScheduled || guaranteedCustomerSpawned || totalCustomers >= 3)
        {
            Debug.Log(
                $"[손님 이야기] 이야기 손님 우선 등장 생략" +
                $" | 활성 이야기 존재={(ActiveStory != null ? "예" : "아니요")}" +
                $" | 남은 대화 존재={(hasRemainingTopics ? "예" : "아니요")}" +
                $" | 특별 주문 예약됨={(specialOrderScheduled ? "예" : "아니요")}" +
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

    public static bool IsTalkTarget(CustomerType customerType) =>
        ActiveStory != null && !SaveData.storyCompleted && ActiveStory.CustomerType == customerType;

    public static bool CanTalkToday(CustomerType customerType) =>
        IsTalkTarget(customerType) &&
        HasRemainingTopics(customerType) &&
        RunState.nextSpecialOrderDay < 0 &&
        RunState.lastTalkDay != Managers.Game.Day;

    public static bool CompleteTalkTopic(CustomerType customerType, int topicIndex)
    {
        if (!CanTalkToday(customerType) || ActiveStory == null || topicIndex < 0 || topicIndex >= ActiveStory.Topics.Length)
        {
            Debug.LogWarning(
                $"[손님 이야기] 대화 주제 완료 처리 차단 | 선택지 번호={topicIndex + 1}" +
                $" | {GetTalkDebugState(customerType)}");
            return false;
        }

        string topicId = SaveIds.Topic(topicIndex);
        bool isNew = !SaveData.completedTopicIds.Contains(topicId);
        RunState.lastTalkDay = Managers.Game.Day;
        if (isNew)
            SaveData.completedTopicIds.Add(topicId);
        int validCompletedTopicCount = CompletedTopicsFor(customerType).Count;
        if (validCompletedTopicCount >= ActiveStory.Topics.Length &&
            RunState.nextSpecialOrderDay < 0 &&
            IsFillingAvailable(ActiveStory.RequiredFilling))
            RunState.nextSpecialOrderDay = Managers.Game.Day + 1;

        Persist();
        Debug.Log(
            $"[손님 이야기] 대화 주제 완료 | 손님={ActiveStory.DisplayName} | 선택지 번호={topicIndex + 1}" +
            $" | 처음 들은 주제={(isNew ? "예" : "아니요")}" +
            $" | 완료한 대화={validCompletedTopicCount}/{ActiveStory.Topics.Length}" +
            $" | 마지막 대화 날짜={RunState.lastTalkDay}일차" +
            $" | 특별 주문 예정일={(RunState.nextSpecialOrderDay > 0 ? RunState.nextSpecialOrderDay + "일차" : "없음")}");
        return isNew;
    }

    public static string GetTalkDebugState(CustomerType customerType)
    {
        bool hasActiveStory = ActiveStory != null;
        bool isTarget = hasActiveStory && !SaveData.storyCompleted && ActiveStory.CustomerType == customerType;
        bool hasRemainingTopics = isTarget && HasRemainingTopics(customerType);
        bool specialOrderScheduled = RunState.nextSpecialOrderDay >= 0;
        bool alreadyTalkedToday = RunState.lastTalkDay == Managers.Game.Day;
        bool canTalk = isTarget && hasRemainingTopics && !specialOrderScheduled && !alreadyTalkedToday;
        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        return $"손님={customerName} | 현재 날짜={Managers.Game.Day}일차" +
            $" | 활성 이야기={(hasActiveStory ? ActiveStory.DisplayName : "없음")}" +
            $" | 이야기 완료={(SaveData.storyCompleted ? "예" : "아니요")}" +
            $" | 이야기 대상 손님={(isTarget ? "예" : "아니요")}" +
            $" | 남은 대화 존재={(hasRemainingTopics ? "예" : "아니요")}" +
            $" | 특별 주문 예약됨={(specialOrderScheduled ? "예" : "아니요")}" +
            $" | 마지막 대화 날짜={(RunState.lastTalkDay > 0 ? RunState.lastTalkDay + "일차" : "없음")}" +
            $" | 오늘 이미 대화함={(alreadyTalkedToday ? "예" : "아니요")}" +
            $" | 지금 대화 가능={(canTalk ? "예" : "아니요")}";
    }

    public static bool IsSpecialOrderDue()
    {
        return ActiveStory != null &&
            !SaveData.storyCompleted &&
            !IsSpecialOrderActive &&
            CustomerStorySchedule.IsOrderDue(RunState.nextSpecialOrderDay, Managers.Game.Day);
    }

    public static void BeginSpecialOrder() => IsSpecialOrderActive = true;

    /// <summary>특별 주문 결과를 저장하고 성공 여부를 호출자에게 돌려줍니다.</summary>
    public static bool ResolveSpecialOrder(FillingType filling, QualityStatus bake)
    {
        if (ActiveStory == null)
            return false;

        bool fillingMatch = filling == ActiveStory.RequiredFilling;
        bool bakeMatch = bake == ActiveStory.RequiredBake;
        if (fillingMatch && bakeMatch)
        {
            SaveData.storyCompleted = true;
            RunState.nextSpecialOrderDay = -1;
            Persist();
            RefreshActiveStory();
            return true;
        }

        RunState.nextSpecialOrderDay = CustomerStorySchedule.RetryDayAfterFailure(Managers.Game.Day);
        CustomerStoryOverlay.ShowResult(
            ActiveStory.DisplayName,
            fillingMatch || bakeMatch ? ActiveStory.NearMissMessage : ActiveStory.FailureMessage,
            false);
        Persist();
        return false;
    }

    /// <summary>손님이 화면에서 완전히 퇴장한 뒤에만 특별 주문 상태를 끝냅니다.</summary>
    public static void CompleteSpecialOrderSession()
    {
        IsSpecialOrderActive = false;
    }

    /// <summary>에디터에서 정현 특별 주문 흐름을 처음부터 시험할 때만 사용합니다.</summary>
    public static void ResetForDebug()
    {
        CustomerProgressData state = SaveData;
        CustomerStoryRunState runState = RunState;
        runState.lastTalkDay = -1;
        state.completedTopicIds.Clear();
        runState.nextSpecialOrderDay = -1;
        state.storyCompleted = false;
        ActiveStory = null;
        IsSpecialOrderActive = false;
        guaranteedCustomerSpawned = false;
        Persist();
        Debug.Log("[손님 이야기] 정현 이야기 진행도를 초기화했습니다.");
    }

    private static bool IsFillingAvailable(FillingType filling) =>
        SaveService.Instance.IsFillingUnlocked(filling);

    private static void RefreshActiveStory()
    {
        CustomerStoryData story = CustomerStoryCatalog.Get(CustomerType.JeongHyun);
        ActiveStory = SaveData.storyCompleted || story == null || !IsFillingAvailable(story.RequiredFilling)
            ? null
            : story;
    }

    private static void Persist()
    {
        SaveService.Instance.SaveStoryProgress();
    }
}
