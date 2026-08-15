
using System;
using UnityEngine;

//확장 함수, 기존 함수 랩핑함
public static class Extension 
{
/*    public static T FindComponent<T>(this T go, Object parent = null)
        where T : Object
    {
        return Util.FindComponent<T>(go.name, parent);
    }

    public static GameObject FindObject(this GameObject go, Object parent = null)
    {
        return Util.FindObject(go.name, parent);
    }*/

    public static void AddEvent(this GameObject go, Action action)
    {
        UI_Base.AddEvent(go, action);
    }


}
