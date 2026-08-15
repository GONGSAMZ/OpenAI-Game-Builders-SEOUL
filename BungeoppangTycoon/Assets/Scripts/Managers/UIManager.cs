using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager 
{
    GameObject scene;
    Stack<GameObject> popup = new Stack<GameObject>();
    int popup_order = 10; //팝업 순서 조절 용

    //UI오브젝트들의 부모가 되는 게임 오브젝트UIRoot
    GameObject UIRoot
    {
        get
        {
            //탐색 해서 할당
            GameObject root = GameObject.Find("@UI");

            //없으면 생성하고 할당
            if (root == null)
                root = new GameObject { name = "@UI" };

            //반환
            return root;
        }
    }


    //UI프리팹을 생성하는 메서드
    public T ShowUI<T>(bool isScene = true, string name = null,  Transform parent = null)
        where T : UI_Base
    {
        //이름이 null이면 타입 이름 채택
        if(string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        //생성
        GameObject prefab = Managers.Resource.Instantiate($"Prefabs/UI/{name}");
        //하이어라키 위치 조정
        if (parent == null)
            prefab.transform.SetParent(UIRoot.transform);
        else
            prefab.transform.SetParent(parent);

        //Scene or Popup 저장
        if (isScene == true)
        {
            scene = prefab;
        }
        else
        {
            //스택
            popup.Push(prefab);
            prefab.GetComponent<Canvas>().sortingOrder
                = ++popup_order;

        }

        return prefab.GetOrAddComponent<T>();
    }

    public void CloseUI(bool isScene = true)
    {
        if(isScene == true)
        {
            Object.Destroy(scene);
        }
        else
        {

            Object.Destroy(popup.Peek());
            --popup_order;
            popup.Pop();

        }
    }
}
