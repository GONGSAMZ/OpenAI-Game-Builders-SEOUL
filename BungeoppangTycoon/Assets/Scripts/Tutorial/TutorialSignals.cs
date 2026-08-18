using System;
using UnityEngine;

public enum TutorialEvent
{
    CustomerOrderAccepted,
    ToolSelected,
    MoldFilled,
    FillingAdded,
    TopBatterAdded,
    BakeStageAdvanced,
    Cooked,
    FishBunDisplayed,
    FishBunServed,
}

/// <summary>
/// 실제 조리 성공 지점을 UI 튜토리얼에 전달하는 작은 신호 통로입니다.
/// 게임 규칙을 바꾸지 않고, 성공한 행동만 UI가 구독하도록 분리합니다.
/// </summary>
public static class TutorialSignals
{
    public static event Action<TutorialEvent, GameObject> Raised;

    public static void Raise(TutorialEvent tutorialEvent, GameObject source)
    {
        Raised?.Invoke(tutorialEvent, source);
    }
}
