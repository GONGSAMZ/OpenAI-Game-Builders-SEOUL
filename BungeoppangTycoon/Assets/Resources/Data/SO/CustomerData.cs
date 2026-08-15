using UnityEngine;

[CreateAssetMenu(fileName = "Customer", menuName = "Scriptable Object/Customer Data", order = int.MaxValue)]
public class CustomerData : ScriptableObject
{

    [SerializeField]
    private CustomerType _customer; //손님 종류
    public CustomerType _Customer { get { return _customer; } }

/*    [SerializeField]
    private Sprite image; //기본 외형 스프라이트
    public Sprite Image { 
        get {  } }*/
    
    public Sprite GetImage(int index = 0)
    {
        return Managers.Resource.LoadSprite($"Customers/{_Customer}", index);
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

