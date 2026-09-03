#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CustomerStoryDebugMenu
{
    [MenuItem("Bungeoppang/손님 이야기/정현 진행도 초기화")]
    private static void ResetJeongHyunStory()
    {
        CustomerStoryProgress.ResetForDebug();
    }

    [MenuItem("Bungeoppang/손님 이야기/테스트/정현 대화 처음부터 준비 (다음 손님)")]
    private static void PrepareJeongHyunTalkTest()
    {
        CustomerStoryProgress.PrepareTalkTest();
    }

    [MenuItem("Bungeoppang/손님 이야기/테스트/정현 대화 처음부터 준비 (다음 손님)", true)]
    private static bool ValidatePrepareJeongHyunTalkTest() => EditorApplication.isPlaying;

    [MenuItem("Bungeoppang/손님 이야기/테스트/정현 특별 주문 바로 시작")]
    private static void StartJeongHyunSpecialOrderTest()
    {
        CustomerStoryProgress.PrepareSpecialOrderTest();
        CustomerController controller = Object.FindFirstObjectByType<CustomerController>();
        if (controller == null)
        {
            Debug.LogWarning("[손님 이야기] 특별 주문 테스트를 시작할 손님 컨트롤러가 없습니다.");
            return;
        }

        if (CustomerStoryProgress.ActiveStory == null)
        {
            Debug.LogWarning("[손님 이야기] 특별 주문 테스트를 시작할 활성 이야기가 없습니다.");
            return;
        }

        CustomerStoryProgress.BeginSpecialOrder();
        controller.BeginSpecialOrder(CustomerStoryProgress.ActiveStory);
    }

    [MenuItem("Bungeoppang/손님 이야기/테스트/정현 특별 주문 바로 시작", true)]
    private static bool ValidateStartJeongHyunSpecialOrderTest() => EditorApplication.isPlaying;
}
#endif
