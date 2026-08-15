public enum DayState
{
    Waiting,
    Opening,
    Running,
    Closing,
    //Finalized,
}

public enum CookingState
{
    None,
    bottomBatter, //밑반죽 부음
    filled, //속재료 넣음
    topBatter, //윗반죽 부음
    cooking, //굽기 1단계
    cooked, //굽기 2단계(완성됨)
}

public enum FillingType
{
    //붕어빵 맛 종류
    redBean,
    custard,
    nutella,
    creamCheese,
    pizza,
    mint,
    sweetPotato,
    greenTea,
}

public enum QualityStatus
{
    //조리 정도에 대한 판정 기준 종류
    None,
    insufficient, //부족
    perfect, //완벽
    excessive, //과함
}

public enum CustomerType
{
    //손님 종류
    JeongHyun,
    HaYoung,
    MiJu,
}

public enum EndingType
{
    //엔딩 종류
    Over, 
    Normal,
    Clear,
}

public class Define 
{


    public static string BatterString = "Batter";
    public static string FillingString = "Filling";

    public static int BatterCost = 100;
    public static float FillingCostRate = 0.2f;

    public static int[] FillingPrice=
    {
        500,
        500,
        700,
        800,
        900,
        1000,
        1100,
        1200,


    };
    public static string[] FillingText =
    {
        "팥 붕어빵",
        "슈크림 붕어빵",
        "초코 붕어빵",
        "크림치즈 붕어빵",
        "피자 붕어빵",
        "민트 붕어빵",
        "고구마 붕어빵",
        "녹차 붕어빵",
    };

    public static string[] UI_DayEndText =
    {
        "오늘 판매한 붕어빵",
        "오늘 누적 손님 수",
        "오늘의 매출",
        "소모된 재료 비용",
        "오늘의 영업이익",
    };
    public static string[] UI_StoreText =
    {
        "재료",
        "스킬",
        "도구",
        "홍보",
    };

    public static string[] UI_Settings =
    {
        "나가기",
        "돌아가기",

    };

    public static string[] EndingImagePath =
    {
        "OverEndingScene",
        "NormalEndingScene",
        "ClearEndingScene",
    };

    public static string[] OverEndingText =
    {
        "텅 빈 틀만 남은 가게.",
        "재료도, 손님도, 희망도 사라졌다.",
        "하루하루 버티던 영업은 결국 멈췄다.",
        "이대로 접는 수밖에 없을까.",
        "겨울은 계속되지만, 이 가게는 멈춘다.",
        "Game Over: [파산 엔딩]"
    };

    public static string[] NormalEndingText =
    {
        "늦은 밤, 익숙한 거리에 문을 닫는다.",
        "적당히 팔고, 적당히 벌고, 적당히 웃었다.",
        "크게 성공하지도, 크게 실패하지도 않았다.",
        "잠시 멈춰서, 다음 겨울을 기다려본다.",
        "그저 그런 하루들의 끝.",
        "Ending: [평범한 마무리]"
    };

    public static string[] ClearEndingText =
    {
        "매서운 겨울바람 속에서도 사람들이 줄을 섰다.",
        "당신의 붕어빵은 누군가에겐 하루의 위로였다.",
        "매일같이 완판, 매출 최고치 경신.",
        "작은 가게는 이 거리에 없어선 안 될 곳이 되었다.",
        "이 겨울, 가장 뜨거웠던 가게.",
        "Congratulations! [인기 가게 엔딩]"
    };
}
