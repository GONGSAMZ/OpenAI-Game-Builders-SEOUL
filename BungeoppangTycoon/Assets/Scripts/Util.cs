using System; //타입 정보(Type)
using UnityEngine;

public static class Util
{
    //열거형 크기 반환 메서드
    public static int GetEnumSize(Type enumName)
    {
        return Enum.GetNames(enumName).Length;
    }

    //디버깅용 메서드
    public static bool checkNull(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"NULL");
            return true;
        }

        return false;
    }

    //GetComponent 랩핑한 메서드: 컴포넌트 없는 경우 부착시킴
    public static T GetOrAddComponent<T>(GameObject go)
        where T : Component
    {
        T component = go.GetComponent<T>();

        //없다면 컴포넌트 부착하고 반환
        if (component == null)
            component = go.AddComponent<T>();

        return component;
    }

    //update에서 한 번만 실행
    public static void ExecuteOnce(Action action, ref bool boolVar, bool condition = true)
    {
        if( boolVar == condition)
        {
            action();
            boolVar = !boolVar;
            Debug.Log($"한번만 실행: {condition} != {boolVar}");

        }
    }
    #region Find계열함수



    //T타입의 target를 반환하는 Find랩핑 메서드(컴포넌트 용)
    public static T Find<T>(GameObject parent, string target, bool includeInactive = false)
        where T : UnityEngine.Object
    {
        T result = null;

        //_parent의 T타입 자식을 T배열에 다 저장
        T[] childs = parent.GetComponentsInChildren<T>(includeInactive);
        foreach(T child in childs)
        {
            if (child.name == target)
            {
                result = child;
                break;
            }
        }

        return result;
    }

    //게임 오브젝트를 찾아서 반환하는 메서드
    public static GameObject FindObject(GameObject parent, string target, bool includeInactive = false)
    {
        Transform result = Find<Transform>(parent, target, includeInactive);
        return result.gameObject;
    }

    #endregion


}
