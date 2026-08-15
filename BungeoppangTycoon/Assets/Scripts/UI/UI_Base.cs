using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public abstract class UI_Base : MonoBehaviour
{
    // GameObject를 Type별로 관리
    protected Dictionary<Type, UnityEngine.Object[]> dic = new();

    protected void Start()
    {
        Init();
    }

    protected abstract void Init();

    //특정 UI오브젝트에 상호작용 기능 붙이는 메서드
    public static void AddEvent(GameObject go, Action action)
    {
        UI_EventHandler evt = Util.GetOrAddComponent<UI_EventHandler>(go);

        evt.OnClickHandler -= action; //액션 중복 발생 방지
        evt.OnClickHandler += action; //액션 등록
    }

    protected void SetWorldUI()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingLayerName = "UI";
    }

    #region Bind계열 메서드
    //기본 Bind 메서드
    protected void Bind<T>(Type enumT)
        where T : UnityEngine.Object
    {

        //열거형 내용 다 string 배열로 추출
        string[] names = Enum.GetNames(enumT);

        //임시 배열 생성
        int arrSize = name.Length; 
        UnityEngine.Object[] objs = new UnityEngine.Object[arrSize];

        //배열 채우기
        for (int i = 0; i < names.Length; ++i)
        {
            objs[i] = Util.Find<T>(gameObject, names[i]);
        }

        //전역에 있는 딕셔너리에 값(타입-컴포넌트 배열) 채우기
        dic.Add(typeof(T), objs);
    }

    /* 특정 부모 오브젝트 산하의 같은 타입의 자식obj들을 바인딩
     * 이 형제 obj들은 일관된 구조로 자식이 있어서,,
     * BindChilds와 연계해서 사용
     */
    protected void BindParents<T>(Type enumName, string parentName)
        where T : UnityEngine.Object
    {
        //하이어라키에서 부모를 이름으로 찾기
        GameObject parent = Util.FindObject(gameObject,$"{parentName}");

        //찾을 자식 객체 이름을 string배열로 저장
        string[] names = Enum.GetNames(enumName);

        //자식 객체 저장용 배열 생성
        T[] obj = new T[names.Length]; 

        //자식 객체를 부모 산하에서 탐색해서 배열에 저장
        for(int i = 0; i < names.Length; i++) 
        {
            if(typeof(T) == typeof(GameObject))
            {
                obj[i] = Util.FindObject(parent, names[i]) as T;

            }
            else
            {
                obj[i] = Util.Find<T>(parent, names[i]);

            }
        }

        AddDictionaryValue<T>(obj);

    }

    //첫 번째 인수 parentEnum은 이미 바인딩 되어야, 산하의 자식 바인딩 할 수 있음
    // 수정??
    protected void BindChilds<parentT, childT>(Type parentEnum, string childName)
        where parentT : UnityEngine.Object
        where childT : UnityEngine.Object
    {
        
        childT[] value = new childT[Util.GetEnumSize(parentEnum)];

        //부모 오브젝트가 바인딩되어 있지 않은 경우 체크
        if (dic.ContainsKey(typeof(parentT)) == false)
        {
            Debug.LogWarning($"{typeof(parentT)}타입이 바인딩 되지 않았음");
            return;
        }

        //각 부모의 자식들 탐색
        for (int i = 0; i < Util.GetEnumSize(parentEnum); ++i)
        {
            GameObject parent;
            //바인딩된 부모 찾기
            if (typeof(parentT) == typeof(GameObject))
                parent = dic[typeof(GameObject)][i] as GameObject;
            else
                parent = (dic[typeof(parentT)][i] as Component).gameObject; 

            Util.checkNull(parent);
            //Debug.LogWarning($"{parent.name}");

            childT child;

            //찾는 자식 오브젝트인 경우
            if (typeof(childT) == typeof(GameObject))
            {
                child = Util.FindObject(parent, childName) as childT;
            }
            else
            {
                child = Util.Find<childT>(parent, childName); //부모의 자식 찾기

            }

            value[i] = child; //배열 채우기

        }

        for (int i = 0; i < value.Length; ++i)
        {

            GameObject obj = value[i] as GameObject;
            if (obj != null)
            {
                Debug.Log($"{obj.name}");

            }

        }

        AddDictionaryValue<childT>(value);
    }

    #endregion

    #region Get계열 메서드

    //딕셔너리에서 특정 타입(T)의 값인 배열 인덱스에 접근해 게임 오브젝트 반환
    protected T Get<T>(int index) 
        where T : UnityEngine.Object //Unity에서 관리되는 오브젝트만 허용
    {

        if(dic.TryGetValue(typeof(T), out UnityEngine.Object[] value))
        {
            if (value[index] == null)
            {
                Debug.LogWarning($"{typeof(T)}타입 value[{index}] 널.");
                return null;
            }
            return value[index]
                as T;
            //주의: as 연산자는 참조타입에만 사용가능함
            //=> T를 참조가능 타입인 UnityEngine.Object으로 제약 걸어줘야 함
        }
        else
        {
            Debug.LogWarning($"{typeof(T)}타입 {index}번째 널이다.");
            return null;
        }


    }

    //자주 사용하는 타입에 대해서... 확장? => 편의성 증진
    protected Image GetImage(int index)
    {
        return Get<Image>(index);
    }
    protected Button GetButton(int index)
    {
        return Get<Button>(index);
    }
    protected TextMeshProUGUI GetTMP(int index)
    {
        //TextMeshProUGUI TMP = Get<TextMeshProUGUI>(index);
        if(Get<TextMeshProUGUI>(index) == null)
        {
            Debug.LogWarning($"NULL : TextMeshProUGUI[{index}]");
            return null;
        }
        return Get<TextMeshProUGUI>(index);
        //return TMP.GetComponent<TextMeshProUGUI>();
    }
    #endregion

    //딕셔너리에 T타입 값배열 저장하는 메서드
    protected void AddDictionaryValue<T>(T[] obj)
        where T : UnityEngine.Object
    {
        //딕셔너리에 추가 [키: 자식 타입, 값: 자식 배열]
        if (dic.ContainsKey(typeof(T)) == true)
        {
            //키가 이미 있기에, 기존 배열 deepCopy&값 추가한 후 배열 등록
            int arrSize = obj.Length + dic[typeof(T)].Length; //새 배열 크기 계산
            T[] newArr = new T[arrSize]; //새 배열 생성

            //배열 채우기
            int j = 0;
            for (int i = 0; i < arrSize; ++i)
            {
                T element;
                if (i < dic[typeof(T)].Length)
                    element = dic[typeof(T)].GetValue(i) as T;
                else
                    element = obj[j++];

                newArr[i] = element;
            }

            /*int 
                        for(int i = 0; i < dic[typeof(T)].Length; ++i)
                            newArr[i] = dic[typeof(T)].GetValue(i) as T;

                        for(int i = dic[typeof(T)].Length; i < arrSize; ++i)
                        {

                            newArr[i] = obj[++j];
                        }
            */
            //딕셔너리에 업데이트한 배열 등록
            dic[typeof(T)] = newArr;

        }
        else
        {
            //키와 값 신규 등록
            dic.Add(typeof(T), obj);
        }


    }

        /*        
                //디버깅용 코드
                Debug.Log($"딕셔너리 내용");
                for (int i = 0; i < dic[typeof(T)].Length; ++i)
                {
                    Debug.Log($"{typeof(T)} : {dic[typeof(T)].GetValue(i)}");
                }

         */


}
