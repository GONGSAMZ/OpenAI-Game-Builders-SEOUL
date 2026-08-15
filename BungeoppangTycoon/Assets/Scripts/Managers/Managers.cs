using UnityEngine;

public class Managers : MonoBehaviour
{
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
