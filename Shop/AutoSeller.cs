using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class AutoSeller : Production
{
    [SerializeField]
    GameObject trUnit;
    TransportUnit transportUnit;
    NetworkVariable<bool> isUnitInStr = new NetworkVariable<bool>();
    bool isTransportable;
    [SerializeField]
    MerchandiseListSO toolShopMerchListSO;
    List<Merchandise> merchList = new List<Merchandise>();
    List<Item> merchItems = new List<Item>();

    int maxSendAmount;

    Dictionary<Item, int> invItemCheckDic = new Dictionary<Item, int>();

    protected override void Start()
    {
        base.Start();
        isRunning = true;
        maxFuel = DefaultMaxFuel;
        isStorageBuilding = true;
        if (IsServer)
            isUnitInStr.Value = (transportUnit == null);

        merchList = toolShopMerchListSO.MerchandiseSOList;
        foreach (var merch in merchList)
        {
            if (!merchItems.Contains(merch.item))
            {
                merchItems.Add(merch.item);
            }
        }

        inventory.onItemChangedCallback += TransportableCheck;
        inventory.invenAllSlotUpdate += TransportableCheck;
        TransportableCheck();
    }

    protected override void Update()
    {
        base.Update();

        if (isDestroying)
        {
            isDestroying = false;
            isRunning = false;
            RemoveFunc();
        }

        if (!isPreBuilding && isRunning)
        {
            if (isTransportable)
            {
                prodTimer += Time.deltaTime;
                if (prodTimer > cooldown)
                {
                    if (isUnitInStr.Value)
                    {
                        if (IsServer)
                        {
                            SendTransportItemDicCheck();
                        }
                        prodTimer = 0;
                    }
                }
            }
            else
            {
                prodTimer = 0;
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (inventory != null)
        {
            inventory.onItemChangedCallback -= TransportableCheck;
            inventory.invenAllSlotUpdate -= TransportableCheck;
        }
    }

    void TransportableCheck()
    {
        TransportableCheck(0);
    }

    public void TransportableCheck(int slotindex)
    {
        isTransportable = false;
        int totalPrice = 0;

        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);

            if (invenItem.item != null && merchItems.Contains(invenItem.item))
            {
                foreach (var merch in merchList)
                {
                    if (merch.item == invenItem.item)
                    {
                        totalPrice += (merch.sellPrice * invenItem.amount);
                        isTransportable = true;
                        break;
                    }
                }
            }
        }
        if (isUIOpened)
            sInvenManager.finance.SetFinance(totalPrice);
    }

    public override void OpenUI()
    {
        base.OpenUI();
        sInvenManager.SetInven(inventory, ui);
        sInvenManager.SetProd(this);
        sInvenManager.progressBar.SetMaxProgress(cooldown);
        sInvenManager.SetCooldownText(cooldown);
        sInvenManager.finance.gameObject.SetActive(true);

    }

    public override void CloseUI()
    {
        base.CloseUI();
        sInvenManager.finance.gameObject.SetActive(false);
        sInvenManager.finance.SetFinance(0);
        sInvenManager.ReleaseInven();
    }

    [ServerRpc]
    void OpenAnimServerRpc(string optionName)
    {
        OpenAnimClientRpc(optionName);
    }

    [ClientRpc]
    void OpenAnimClientRpc(string optionName)
    {
        animator.Play(optionName, -1, 0);
    }

    void SendTransportItemDicCheck()
    {
        maxSendAmount = MaxStackAmount;
        int Sendcalculate = 0;
        Dictionary<Item, int> tempInvItemCheckDic = new Dictionary<Item, int>();

        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);

            if (invenItem.item != null && merchItems.Contains(invenItem.item))
            {
                int availableAmount = Mathf.Min(invenItem.amount, maxSendAmount - Sendcalculate);

                if (!tempInvItemCheckDic.ContainsKey(invenItem.item))
                {
                    tempInvItemCheckDic.Add(invenItem.item, availableAmount);
                }
                else
                {
                    tempInvItemCheckDic[invenItem.item] += availableAmount;
                }

                Sendcalculate += availableAmount;

                if (Sendcalculate >= maxSendAmount)
                    break;
            }
        }

        this.invItemCheckDic = tempInvItemCheckDic;

        OpenAnimServerRpc("Open");
    }

    public void RemoveUnit(GameObject returnUnit)
    {
        if (IsServer)
            isUnitInStr.Value = true;
        transportUnit = null;
        returnUnit.GetComponent<TransportUnit>().DestroyFunc();
        OpenAnimServerRpc("ItemGetOpen");
    }

    public void UnitSendOpen()
    {
        if (IsServer && invItemCheckDic != null && invItemCheckDic.Count > 0)
        {
            GameObject unit = Instantiate(trUnit, transform.position, Quaternion.identity);
            unit.TryGetComponent(out NetworkObject netObj);
            if (!netObj.IsSpawned) netObj.Spawn(true);
            if (IsServer)
                isUnitInStr.Value = false;
            transportUnit = unit.GetComponent<TransportUnit>();
            transportUnit.SetUnitColorIndex(1);

            Vector3 portalPos;
            if (this.isInHostMap)
                portalPos = GameManager.instance.hostPlayerSpawnPos;
            else
                portalPos = GameManager.instance.clientPlayerSpawnPos;

            int totalPrice = 0;
            foreach (var dicData in invItemCheckDic)
            {
                foreach (var merch in merchList)
                {
                    if (merch.item == dicData.Key)
                    {
                        totalPrice += (merch.sellPrice * dicData.Value);
                        break;
                    }
                }
            }

            transportUnit.MovePosSet(this, portalPos, invItemCheckDic, totalPrice);
            foreach (var dicData in invItemCheckDic)
            {
                inventory.Sub(dicData.Key, dicData.Value);
            }
        }
    }

    public override bool CanTakeItem(Item item)
    {
        if (isInvenFull) return false;

        bool canTake;
        int containableAmount = inventory.SpaceCheck(item);

        if (1 <= containableAmount)
        {
            canTake = true;
        }
        else if (containableAmount != 0)
        {
            canTake = true;
        }
        else
        {
            canTake = false;
        }

        return canTake;
    }

    public override void OnFactoryItem(ItemProps itemProps)
    {
        if (IsServer)
            inventory.StorageAdd(itemProps.item, itemProps.amount);
        itemProps.ClientResetItemProps();
    }

    public override void OnFactoryItem(Item item)
    {
        if (IsServer)
            inventory.StorageAdd(item, 1);
    }

    public override void GetUIFunc()
    {
        InventoryList inventoryList = canvas.GetComponent<InventoryList>();

        foreach (GameObject list in inventoryList.StructureStorageArr)
        {
            if (list.name == "AutoSeller")
            {
                ui = list;
            }
        }
    }

    public void RemoveFunc()
    {
        if (transportUnit != null)
            transportUnit.MainTrBuildRemove();
    }

    public void TrUnitToHomelessDrone()
    {
        //건물이 파괴될 때 소유한 드론이 있는 경우 HomelessDroneManager에 인계
        if (transportUnit != null)
        {
            HomelessDroneManager.instance.AddDrone(transportUnit);
        }
    }

    public void UnitLoad(Vector3 spawnPos, Dictionary<int, int> itemDic)
    {
        GameObject unit = Instantiate(trUnit, spawnPos, Quaternion.identity);
        unit.TryGetComponent(out NetworkObject netObj);
        if (!netObj.IsSpawned) netObj.Spawn(true);

        Dictionary<Item, int> item = new Dictionary<Item, int>();
        foreach (var data in itemDic)
        {
            item.Add(GeminiNetworkManager.instance.GetItemSOFromIndex(data.Key), data.Value);
        }

        Vector3 portalPos;
        if (this.isInHostMap)
            portalPos = GameManager.instance.hostPlayerSpawnPos;
        else
            portalPos = GameManager.instance.clientPlayerSpawnPos;

        int totalPrice = 0;
        foreach (var dicData in item)
        {
            foreach (var merch in toolShopMerchListSO.MerchandiseSOList)
            {
                if (merch.item == dicData.Key)
                {
                    totalPrice += (merch.sellPrice * dicData.Value);
                    break;
                }
            }
        }

        TransportUnit unitScript = unit.GetComponent<TransportUnit>();
        transportUnit = unitScript;
        if (IsServer)
            isUnitInStr.Value = false;
        transportUnit.SetUnitColorIndex(1);
        unitScript.MovePosSet(this, portalPos, item, totalPrice);
        if (item.Count == 0)
            unitScript.TakeItemEnd(false);
    }

    public override StructureSaveData SaveData()
    {
        StructureSaveData data = base.SaveData();

        if (transportUnit != null)
        {
            SerializedVector3 vector3 = Vector3Extensions.FromVector3(transportUnit.transform.position);
            data.trUnitPosData.Add(vector3);
            Dictionary<int, int> itemSave = new Dictionary<int, int>();
            foreach (var itemData in transportUnit.itemDic)
            {
                itemSave.Add(GeminiNetworkManager.instance.GetItemSOIndex(itemData.Key), itemData.Value);
            }

            Dictionary<int, Dictionary<int, int>> itemDataSave = new Dictionary<int, Dictionary<int, int>>();
            itemDataSave.Add(0, itemSave);
            data.trUnitItemData = itemDataSave;
        }

        return data;
    }

    public override void RemoveObjClient()
    {
        StopAllCoroutines();

        if (isUIOpened)
            CloseUI();

        if (InfoUI.instance.str == this)
            InfoUI.instance.SetDefault();

        for (int i = 0; i < nearObj.Length; i++)
        {
            if (nearObj[i])
            {
                nearObj[i].ResetNearObj(this);
            }
        }

        if (overclockTower != null && TryGet(out Production prod))
            overclockTower.RemoveObjectsOutOfRange(prod);

        if (!isManualDestroy)
            RemoveFunc();
        TrUnitToHomelessDrone();        
      
        if (GameManager.instance.focusedStructure == this)
        {
            GameManager.instance.focusedStructure = null;
        }

        DestroyFuncServerRpc();
    }
}
