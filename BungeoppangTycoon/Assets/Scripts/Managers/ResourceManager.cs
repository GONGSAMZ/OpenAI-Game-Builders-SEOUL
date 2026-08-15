using UnityEngine;
using UnityEngine.UI;

public class ResourceManager 
{
    public T Load<T>(string name)
        where T : UnityEngine.Object
    {
        return Resources.Load<T>(name);
    }

    //프리팹 생성&반환 메서드
    public GameObject Instantiate(string path)
    {
        GameObject prefab = Load<GameObject>($"{path}");

        if (prefab == null)
        {
            Debug.Log($"{path}이 null");
            prefab = Load<GameObject>($"nullPrefab");
        }

        //원래 쓰던 Instantiate는 Object클래스 산하 메서드라서 동명메서드로 인한 재귀 방지
        return Object.Instantiate(prefab); 
    }

    //Sprite 반환 메서드
    public Sprite LoadSprite(string path, int? index = null)
    {
        //단일 스프라이트만 원하는 경우
        if(index == null)
        {
            Sprite sprite = Load<Sprite>($"Sprites/{path}");
            return sprite;
        }
        //slice한 스프라이트 시트에서 꺼내오는 경우
        else
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>($"Sprites/{path}");

            if (sprites == null || sprites.Length == 0)
                Debug.Log($"스프라이트시트 X");

            return sprites[(int)index];
        }
        
    }


    public CustomerData LoadCustomerSO(CustomerType customer)
    {
        return Resources.Load<CustomerData>($"Data/So/{customer}");

    }
}
