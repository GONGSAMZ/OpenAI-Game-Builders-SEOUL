using UnityEngine;

public class Managers : MonoBehaviour
{
    // WebGL 백그라운드 탭은 브라우저에 의해 약 1Hz로 제한될 수 있다.
    // Unity가 그 간격을 과도하게 잘라내지 않도록 1초보다 조금 큰 상한을 사용한다.
    public const float BackgroundMaximumDeltaTime = 1.25f;

    //싱글톤
    static Managers _instance;
    static GameManagerEx gameManager = new GameManagerEx();
    static ResourceManager resourceManager = new ResourceManager();
    static UIManager uiManager = new UIManager();


    static public Managers Instance { get { Init();  return _instance; } }
    static public GameManagerEx Game { get { return gameManager;  } }
    static public ResourceManager Resource { get { return resourceManager;} }
    static public UIManager UI { get { return uiManager; } }


    #region 조작
    public float _gameSpeed = 1.8f; //게임 속도
    public int reactionDelayTime = 1; //반응하는 속도
    public int money = 5000; //돈
    public int day = 3; 


    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ConfigureBackgroundExecution()
    {
        Application.runInBackground = true;
        Time.maximumDeltaTime = Mathf.Max(Time.maximumDeltaTime, BackgroundMaximumDeltaTime);
    }

    void Awake()
    {
        // PlayerSettings와 런타임 양쪽에서 보장해 플랫폼별 설정 누락을 방지한다.
        ConfigureBackgroundExecution();

        // PC 단축키와 모바일 스와이프를 한 곳에서 받습니다.
        // 씬에 컴포넌트를 수동으로 연결하지 않아도 기존 @Managers에서 함께 동작합니다.
        Util.GetOrAddComponent<InputManager>(gameObject);
    }

    void Start()
    {
        Game.InitGame();
    }

    void Update()
    {
        Game.OnUpdate();
    }

    static void Init()
    {
        if(_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            _instance = Util.GetOrAddComponent<Managers>(go);

        }

        //DontDestroyOnLoad(Instance);
    }

}
