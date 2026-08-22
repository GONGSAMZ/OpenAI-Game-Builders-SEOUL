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
            {
                Debug.LogError($"스프라이트 시트를 찾을 수 없습니다: Sprites/{path}");
                return null;
            }

            int spriteIndex = (int)index;
            if (spriteIndex < 0 || spriteIndex >= sprites.Length)
            {
                Debug.LogError($"스프라이트 인덱스가 범위를 벗어났습니다: Sprites/{path}, index={spriteIndex}, count={sprites.Length}");
                return null;
            }

            return sprites[spriteIndex];
        }
        
    }

    /// <summary>
    /// Multiple Sprite 시트에서 배열 인덱스 대신 Inspector 슬라이스 이름의 끝부분으로 스프라이트를 찾습니다.
    /// Resources.LoadAll의 반환 순서는 표정 의미를 보장하지 않으므로, 손님 표정에는 이 메서드를 사용합니다.
    /// </summary>
    public Sprite LoadSpriteBySuffix(string path, string suffix)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>($"Sprites/{path}");
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError($"스프라이트 시트를 찾을 수 없습니다: Sprites/{path}");
            return null;
        }

        foreach (Sprite sprite in sprites)
            if (sprite != null && sprite.name.EndsWith(suffix, System.StringComparison.Ordinal))
                return sprite;

        Debug.LogError($"스프라이트 시트에서 '{suffix}' 표정을 찾지 못했습니다: Sprites/{path}");
        return null;
    }


    public CustomerData LoadCustomerSO(CustomerType customer)
    {
        return Resources.Load<CustomerData>($"Data/So/{customer}");

    }
}
