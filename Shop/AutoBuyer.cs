using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class AutoBuyer : Production
{
    [SerializeField]
    GameObject trUnit;
    TransportUnit transportUnit;
    NetworkVariable<bool> isUnitInStr = new NetworkVariable<bool>();
    bool isTransportable;
    bool isBuyable;
    [SerializeField]
    MerchandiseListSO oreShopMerchListSO;
    [SerializeField]
    MerchandiseListSO manaStoneShopMerchListSO;
    List<Merchandise> merchList = new List<Merchandise>();

    public int maxBuyAmount;    // 목표 구매 수량
    public int buyInterval;    // 아이템 보유 수량이 해당 변수 아래로 내려갈 때 (최대 수량 - 현재 수량)만큼 구매
    List<TransportUnit> getItemUnit = new List<TransportUnit>();

    float transportTimer;
    float transportInterval;

    Dictionary<Item, int> invItemCheckDic = new Dictionary<Item, int>();

    protected override void Start()
    {
        base.Start();
        isRunning = true;
        maxFuel = DefaultMaxFuel;
        transportInterval = 1.0f;
        isStorageBuilding = false;
        if (IsServer)
            isUnitInStr.Value = (transportUnit == null);

        merchList = oreShopMerchListSO.MerchandiseSOList.Concat(manaStoneShopMerchListSO.MerchandiseSOList).ToList();
        inventory.onItemChangedCallback += TransportableCheck;
        inventory.invenAllSlotUpdate += TransportableCheck;
        TransportableCheck();
        GameManager.instance.onFinanceChangedCallback += BuyableCheck;
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

        if (!isPreBuilding && recipe.name != null && isRunning)
        {
            prodTimer += Time.deltaTime;
            if (isTransportable)
            {
                if (prodTimer > cooldown)
                {
                    if (isUnitInStr.Value && isBuyable)
                    {
                        if (IsServer)
                        {
                            SendTransportItemDicCheck();
                        }
                        prodTimer = 0;
                    }
                }
            }

            if (IsServer)
            {
                if (getItemUnit.Count > 0)
                {
                    transportTimer += Time.deltaTime;
                    if (transportTimer > transportInterval)
                    {
                        if (IsServer)
                            ExStorageCheck();
                        transportTimer = 0;
                    }
                }
                else
                    transportTimer = 0;
            }

            if (IsServer && slot.Item2 > 0 && outObj.Count > 0 && !itemSetDelay)
            {
                int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(output);
                SendItem(itemIndex);
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
        GameManager.instance.onFinanceChangedCallback -= BuyableCheck;
    }

    public override void CheckSlotState(int slotindex)
    {
        // update에서 검사해야 하는 특정 슬롯들 상태를 인벤토리 콜백이 있을 때 미리 저장
        slot = inventory.SlotCheck(0);
    }

    public void MaxSliderUIValueChanged(int amount)
    {
        MaxSliderValueSyncServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MaxSliderValueSyncServerRpc(int amount)
    {
        MaxSliderValueSyncClientRpc(amount);
    }

    [ClientRpc]
    public void MaxSliderValueSyncClientRpc(int amount)
    {
        AutoBuyerManager buyerManager = AutoBuyerManager.instance;
        if (isUIOpened && buyerManager.buyer == this)
        {
            buyerManager.SetMaxSliderValue(amount);
        }
        maxBuyAmount = amount;
        TransportableCheck(0);
    }

    public void MinSliderUIValueChanged(int amount)
    {
        MinSliderValueSyncServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MinSliderValueSyncServerRpc(int amount)
    {
        MinSliderValueSyncClientRpc(amount);
    }

    [ClientRpc]
    public void MinSliderValueSyncClientRpc(int amount)
    {
        AutoBuyerManager buyerManager = AutoBuyerManager.instance;
        if (isUIOpened && buyerManager.buyer == this)
        {
            buyerManager.SetMinSliderValue(amount);
        }
        buyInterval = amount;
        cooldown = buyInterval;
        if (isUIOpened)
        {
            sInvenManager.progressBar.SetMaxProgress(cooldown);
            sInvenManager.SetCooldownText(cooldown);
        }
        TransportableCheck();
    }

    void TransportableCheck()
    {
        TransportableCheck(0);
    }

    public void TransportableCheck(int slotIndex)
    {
        if (output == null)
        {
            isTransportable = false;
        }
        else
        {
            if (slot.Item1 == null)
            {
                if (maxBuyAmount > 0)
                {
                    isTransportable = true;
                }
                else
                {
                    isTransportable = false;
                }
            }
            else
            {
                isTransportable = (slot.Item2 < maxBuyAmount);
            }
        }

        if (isTransportable)
        {
            BuyableCheck();
        }
        else
        {
            if (isUIOpened)
                sInvenManager.finance.SetFinance(0);
        }
    }

    public void BuyableCheck()
    {
        int availableAmount = 0;
        if (slot.Item1 != null)
        {
            availableAmount = maxBuyAmount - slot.Item2;
        }
        else
        {
            availableAmount = maxBuyAmount;
        }

        int totalPrice = 0;
        foreach (var merch in merchList)
        {
            if (merch.item == output)
            {
                totalPrice = (merch.buyPrice * availableAmount);
                break;
            }
        }

        isBuyable = (GameManager.instance.finance.finance >= totalPrice);
        if (isUIOpened)
            sInvenManager.finance.SetFinance(totalPrice, isBuyable);
    }

    public override void OpenUI()
    {
        base.OpenUI();
        sInvenManager.SetInven(inventory, ui);
        sInvenManager.SetProd(this);
        sInvenManager.progressBar.SetMaxProgress(cooldown);
        sInvenManager.SetCooldownText(cooldown);
        sInvenManager.finance.gameObject.SetActive(true);

        sInvenManager.InvenInit();
        if (recipe.name != null)
            SetRecipe(recipe, recipeIndex);

        AutoBuyerManager.instance.SetBuyer(this);
    }

    public override void CloseUI()
    {
        base.CloseUI();
        sInvenManager.finance.gameObject.SetActive(false);
        sInvenManager.finance.SetFinance(0);
        sInvenManager.ReleaseInven();
        AutoBuyerManager.instance.ResetValue();
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

    public override void OpenRecipe()
    {
        if (!isUnitInStr.Value) return;

        rManager.OpenUI();
        rManager.SetRecipeUI("AutoBuyer", this);
    }

    public override void SetRecipe(Recipe _recipe, int index)
    {
        if (recipe != _recipe)
        {
            MaxSliderUIValueChanged(0);
            MinSliderUIValueChanged(10);
        }

        recipe = _recipe;
        recipeIndex = index;
        sInvenManager.ResetInvenOption();
        sInvenManager.slots[0].SetInputItem(itemDic[recipe.items[0]]);
        sInvenManager.slots[0].outputSlot = true;
        sInvenManager.progressBar.SetMaxProgress(cooldown);
        sInvenManager.SetCooldownText(cooldown);
    }

    public override void SetOutput(Recipe recipe)
    {
        if (recipe != null)
            output = itemDic[recipe.items[0]];
        else
            output = null;

        FactoryOverlay();
    }

    protected override void ResetUI()
    {
        base.ResetUI();
        MaxSliderUIValueChanged(0);
        MinSliderUIValueChanged(0);
    }

    public override void ClientConnectSync()
    {
        var data = CollectBaseSyncData();

        // Production은 itemList 안 씀
        data.itemIndexes = new int[0];

        // Production: inventory 데이터
        if (inventory != null)
        {
            var slotNums = new List<int>();
            var itemIdxs = new List<int>();
            var amounts = new List<int>();

            for (int i = 0; i < inventory.space; i++)
            {
                var slot = inventory.SlotCheck(i);
                int idx = GeminiNetworkManager.instance.GetItemSOIndex(slot.item);
                if (idx != -1)
                {
                    slotNums.Add(i);
                    itemIdxs.Add(idx);
                    amounts.Add(slot.amount);
                }
            }

            data.inventorySlotNums = slotNums.ToArray();
            data.inventoryItemIndexes = itemIdxs.ToArray();
            data.inventoryItemAmounts = amounts.ToArray();
        }

        data.recipeIndex = this.recipeIndex;

        // AutoBuyer 전용
        data.maxBuyAmount = this.maxBuyAmount;
        data.buyInterval = this.buyInterval;

        ClientConnectSyncClientRpc(data);
    }

    protected override void ApplyExtraSync(StructureSyncData data)
    {
        maxBuyAmount = data.maxBuyAmount;
        buyInterval = data.buyInterval;
        cooldown = buyInterval;
        if (isUIOpened)
        {
            sInvenManager.progressBar.SetMaxProgress(cooldown);
            sInvenManager.SetCooldownText(cooldown);
        }
    }

    void SendTransportItemDicCheck()
    {
        Dictionary<Item, int> tempInvItemCheckDic = new Dictionary<Item, int>();

        var invenItem = inventory.SlotCheck(0);

        if (invenItem.item != null)
        {
            if (invenItem.amount < maxBuyAmount)
            {
                int availableAmount = maxBuyAmount - invenItem.amount;
                tempInvItemCheckDic.Add(invenItem.item, availableAmount);
            }
        }
        else
        {
            // 레시피 통해서 아웃풋 지정해줘야 함
            if (output != null)
                tempInvItemCheckDic.Add(output, maxBuyAmount);
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
            int totalPrice = 0;
            foreach (var merch in merchList)
            {
                if (merch.item == output)
                {
                    totalPrice = (merch.buyPrice * invItemCheckDic[output]);
                    break;
                }
            }

            if (GameManager.instance.finance.finance >= totalPrice && totalPrice != 0)
            {
                GameObject unit = Instantiate(trUnit, transform.position, Quaternion.identity);
                unit.TryGetComponent(out NetworkObject netObj);
                if (!netObj.IsSpawned) netObj.Spawn(true);

                if (IsServer)
                    isUnitInStr.Value = false;
                transportUnit = unit.GetComponent<TransportUnit>();
                transportUnit.SetUnitColorIndex(0);
                Vector3 portalPos;
                if (this.isInHostMap)
                    portalPos = GameManager.instance.hostPlayerSpawnPos;
                else
                    portalPos = GameManager.instance.clientPlayerSpawnPos;

                GameManager.instance.SubFinanceServerRpc(totalPrice);
                invItemCheckDic.Add(ItemList.instance.itemDic["CopperGoblet"], 0);
                transportUnit.MovePosSet(this, portalPos, invItemCheckDic);
            }
            else
            {
                Debug.Log("Not enough money or lack of input");
            }
        }
    }

    public override void GetUIFunc()
    {
        InventoryList inventoryList = canvas.GetComponent<InventoryList>();

        foreach (GameObject list in inventoryList.StructureStorageArr)
        {
            if (list.name == "AutoBuyer")
            {
                ui = list;
            }
        }
    }

    public void TakeTransportItem(TransportUnit takeUnit, Dictionary<Item, int> _itemDic)
    {
        if (_itemDic != null && _itemDic.Count > 0)
        {
            getItemUnit.Add(takeUnit);
            ExStorageCheck();
            OpenAnimServerRpc("ItemGetOpen");
        }
        else
        {
            takeUnit.TakeItemEnd(false);
        }
    }

    void ExStorageCheck()
    {
        foreach (var exStorage in getItemUnit[0].itemDic.ToList()) // ToList()를 사용하여 복제
        {
            int containableAmount = inventory.SpaceCheck(exStorage.Key);
            if (exStorage.Value <= containableAmount)
            {
                inventory.Add(exStorage.Key, exStorage.Value);
                getItemUnit[0].itemDic.Remove(exStorage.Key);
            }
            else if (containableAmount != 0)
            {
                inventory.Add(exStorage.Key, containableAmount);
                getItemUnit[0].itemDic[exStorage.Key] -= containableAmount; // 원래 변수 수정
            }
            else
            {
                break;
            }
        }

        if (getItemUnit[0].itemDic.Count == 0)
        {
            getItemUnit[0].TakeItemEnd(true);
            getItemUnit.RemoveAt(0);
        }
    }

    public void RemoveFunc()
    {
        if (transportUnit !=  null)
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

        TransportUnit unitScript = unit.GetComponent<TransportUnit>();
        transportUnit = unitScript;
        transportUnit.SetUnitColorIndex(0);
        unitScript.MovePosSet(this, portalPos, item);

        //보낼 때 체크용 아이템을 하나 넣어두고 리턴할 때 삭제함. 따라서 아이템이 1개 있는 경우 돌아오는 드론
        if (item.Count <= 1)
            unitScript.TakeItemEnd(false);
    }

    public override StructureSaveData SaveData()
    {
        StructureSaveData data = base.SaveData();
        data.maxBuyAmount = this.maxBuyAmount;
        data.sendingOption = this.buyInterval;

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
