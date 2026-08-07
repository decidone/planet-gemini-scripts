using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemList : MonoBehaviour
{
    // 스크립트에서 아이템 사용 및 인벤토리 아이템 정렬에 사용
    // 아이템 사용을 위해 <string, Item>으로 Document 만들 필요가 있음
    public ItemListSO itemListSO;
    [HideInInspector]
    public List<Item> itemList = new List<Item>();
    public List<Item> uiItemList = new List<Item>();
    public Dictionary<string, Item> itemDic = new Dictionary<string, Item>();

    #region Singleton
    public static ItemList instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        instance = this;
        itemList = itemListSO.itemSOList;

        foreach (Item item in itemList)
        {
            itemDic.Add(item.name, item);
        }
    }
    #endregion

    public Item FindData(string getBuildName)
    {
        foreach (var itemData in uiItemList)
        {
            if (itemData.name == getBuildName)
                return itemData;
        }

        return null;
    }

    public Item FindDataGetLevel(string getBuildName, int level)
    {
        int findLevel = 1;
        foreach (var itemData in uiItemList)
        {
            if (itemData.name == getBuildName)
            {
                if(findLevel == level)
                {
                    return itemData;
                }
                findLevel ++;
            }
        }
        Debug.Log("Not found : " + getBuildName + ", Level : " + level);
        return null;
    }
}
