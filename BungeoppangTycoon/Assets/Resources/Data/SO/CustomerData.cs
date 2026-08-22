using UnityEngine;

[CreateAssetMenu(fileName = "Customer", menuName = "Scriptable Object/Customer Data", order = int.MaxValue)]
public class CustomerData : ScriptableObject
{

    [SerializeField]
    private CustomerType _customer; //손님 종류
    public CustomerType _Customer { get { return _customer; } }

    [SerializeField]
    private string spriteSheetPath;

/*    [SerializeField]
    private Sprite image; //기본 외형 스프라이트
    public Sprite Image { 
        get {  } }*/
    
    /// <summary>
    /// 스프라이트 시트의 배열 순서가 아니라 Inspector에서 정한 표정 이름으로 이미지를 찾습니다.
    /// </summary>
    public Sprite GetImage(CustomerExpression expression = CustomerExpression.Default)
    {
        if (string.IsNullOrWhiteSpace(spriteSheetPath))
        {
            Debug.LogError($"{name}({_Customer})의 손님 스프라이트 경로가 비어 있습니다.", this);
            return null;
        }

        return Managers.Resource.LoadSpriteBySuffix(spriteSheetPath, $"_{expression}");
    }
/*    [SerializeField]
    private Sprite image; //외형 스프라이트
    public Sprite Image { get { return image; } }

    [SerializeField]
    private Sprite disappoint; //실망한 스프라이트
    public Sprite Image { get { return disappoint; } }*/

    [SerializeField]
    private FillingType flavor; //선호하는 붕어빵 종류
    public FillingType Flavor  { get { return flavor; } }

    [SerializeField]
    private string[] greetingText; //인삿말 저장
    public string[] GreetingText { get { return greetingText; } }

    /*    [SerializeField]
        private string[] greetingText; //인삿말 저장
        public string[] GreetingText { get { return greetingText; } }*/

    [SerializeField]
    private string[] disappointingText; // 주문에 대해 실망했을 때, 할 말 저장
    public string[] DisappointingText { get { return disappointingText; } }
}

/// <summary>손님 스프라이트 시트에 저장된 표정 이름입니다.</summary>
public enum CustomerExpression
{
    Default,
    Joy,
    Disappointed
}

