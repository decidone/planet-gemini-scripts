using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class UnitFactory : Production
{
    private const float UnitSpawnDistance = 1.5f;   // 생성 유닛의 건물 중심으로부터 배치 거리

    [SerializeField]
    Vector2[] nearPos = new Vector2[8];
    [SerializeField]
    Vector2 spawnPos;
    Vector2 movePos;
    bool isSetPos = false;
    float spawnPosCheckTimer;
    float spawnPosCheckInterval = 1f;
    List<GameObject> unitObjList;

    GameObject spawnUnit;
    string setUnitName;
    
    protected override void Start()
    {
        base.Start();
        isGetLine = true;
        unitObjList = UnitList.instance.unitList;
        StartCoroutine(EfficiencyCheckLoop());
    }

    protected override void Update()
    {
        base.Update();
        if (!isPreBuilding)
        {
            if (recipe.name != null)
            {
                if (conn != null && conn.group != null && conn.group.efficiency > 0)
                {
                    if (slot.Item2 >= recipe.amounts[0] && slot1.Item2 >= recipe.amounts[1] && slot2.Item2 >= recipe.amounts[2] &&
                        (gameManager.playerUnitLimit > gameManager.playerUnitAmount || recipe.name == "Tank"))
                    {
                        OperateStateSet(true);
                        prodTimer += Time.deltaTime;
                        spawnPosCheckTimer += Time.deltaTime;
                        if (prodTimer > effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount))
                        {
                            if (spawnPosCheckTimer > spawnPosCheckInterval)
                            {
                                bool spawnPosExist = UnitSpawnPosFind();

                                if (spawnPosExist)
                                {
                                    if (IsServer)
                                    {
                                        Overall.instance.OverallConsumption(slot.Item1, recipe.amounts[0]);
                                        Overall.instance.OverallConsumption(slot1.Item1, recipe.amounts[1]);
                                        Overall.instance.OverallConsumption(slot2.Item1, recipe.amounts[2]);

                                        inventory.SlotSubServerRpc(0, recipe.amounts[0]);
                                        inventory.SlotSubServerRpc(1, recipe.amounts[1]);
                                        inventory.SlotSubServerRpc(2, recipe.amounts[2]);

                                        SetUnit();
                                        SpawnUnit();
                                    }

                                    soundManager.PlaySFX(gameObject, "structureSFX", "Machine");
                                    prodTimer = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        OperateStateSet(false);
                        prodTimer = 0;
                    }
                }
                else
                {
                    OperateStateSet(false);
                    prodTimer = 0;
                }
            }
        }
    }

    public override void CheckSlotState(int slotindex)
    {
        // update에서 검사해야 하는 특정 슬롯들 상태를 인벤토리 콜백이 있을 때 미리 저장
        slot = inventory.SlotCheck(0);
        slot1 = inventory.SlotCheck(1);
        slot2 = inventory.SlotCheck(2);
    }

    public override void CheckInvenIsFull(int slotIndex)
    {
        // output slot을 제외하고 나머지 슬롯이 가득 차 있는지 체크
        for (int i = 0; i < 3; i++)
        {
            if (inventory.SlotAmountCheck(i) < inventory.maxAmount)
            {
                isInvenFull = false;
                return;
            }
        }

        isInvenFull = true;
    }

    public override void OpenUI()
    {
        base.OpenUI();
        sInvenManager.SetInven(inventory, ui);
        sInvenManager.SetProd(this);

        rManager.recipeBtn.gameObject.SetActive(true);
        rManager.recipeBtn.onClick.RemoveAllListeners();
        rManager.recipeBtn.onClick.AddListener(OpenRecipe);

        sInvenManager.InvenInit();
        if (recipe.name != null)
            SetRecipe(recipe, recipeIndex);

        if (isSetPos)
            LineRendererSet(movePos);
    }

    public override void CloseUI()
    {
        base.CloseUI();
        sInvenManager.ReleaseInven();

        rManager.recipeBtn.onClick.RemoveAllListeners();
        rManager.recipeBtn.gameObject.SetActive(false);

        base.DestroyLineRenderer();
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

        data.isSetPos = this.isSetPos;
        data.movePos = this.movePos;

        ClientConnectSyncClientRpc(data);
    }

    protected override void ApplyExtraSync(StructureSyncData data)
    {
        if (data.isSetPos)
        {
            isSetPos = true;
            movePos = data.movePos;
        }
    }

    public override void OpenRecipe()
    {
        rManager.OpenUI();
        rManager.SetRecipeUI("UnitFactory", this);
    }

    public override void SetRecipe(Recipe _recipe, int index)
    {
        base.SetRecipe(_recipe, index);
        sInvenManager.slots[0].SetInputItem(itemDic[recipe.items[0]]);
        sInvenManager.slots[0].SetNeedAmount(recipe.amounts[0]);
        sInvenManager.slots[1].SetInputItem(itemDic[recipe.items[1]]);
        sInvenManager.slots[1].SetNeedAmount(recipe.amounts[1]);
        sInvenManager.slots[2].SetInputItem(itemDic[recipe.items[2]]);
        sInvenManager.slots[2].SetNeedAmount(recipe.amounts[2]);
        sInvenManager.slots[3].SetInputItem(itemDic[recipe.items[3]]);
        sInvenManager.slots[3].SetNeedAmount(recipe.amounts[3]);
        sInvenManager.slots[3].outputSlot = true;

        sInvenManager.dicBtn.gameObject.SetActive(true);
        sInvenManager.dicBtn.onClick.RemoveAllListeners();
        sInvenManager.dicBtn.onClick.AddListener(() => InfoDictionary.instance.Search(recipe.items[3], true));

        if (recipe.name == "Tank" || recipe.name == "UICancel")
            sInvenManager.UnitIconSet(false);
        else
            sInvenManager.UnitIconSet(true);
    }

    public void CooldownTextSet()
    {
        sInvenManager.UnitLimitText();
    }

    public override void GetUIFunc()
    {
        InventoryList inventoryList = canvas.GetComponent<InventoryList>();

        foreach (GameObject list in inventoryList.StructureStorageArr)
        {
            if (list.name == "UnitFactory")
            {
                ui = list;
            }
        }
    }

    protected override void CheckNearObj(int index, Action<Structure> callback)
    {
        int nearX = (int)transform.position.x + twoDirections[index, 0];
        int nearY = (int)transform.position.y + twoDirections[index, 1];
        Cell cell = GameManager.instance.GetCellDataFromPosWithoutMap(nearX, nearY);
        if (cell == null)
            return;

        if (nearPos[index] != null)
            nearPos[index] = new Vector2(nearX, nearY);

        Structure obj = cell.structure;
        if (obj != null)
        {
            nearObj[index] = obj;
            callback(obj);
        }
    }

    public bool UnitSpawnPosFind()
    {
        bool spawnPosExist = false;
        spawnPosCheckTimer = 0;
        for (int i = 0; i < nearPos.Length; i++)
        {
            if (nearObj[i] != null && !nearObj[i].Get<BeltCtrl>())
                continue;
            else
            {
                if (recipe.name == "Tank")
                {
                    Collider2D[] hits = Physics2D.OverlapBoxAll(nearPos[i], new Vector2(1f, 1f), 0f, LayerMask.GetMask("Tank"));
                    if (hits.Length == 0)
                    {
                        spawnPosExist = true;
                        spawnPos = nearPos[i];
                        break;
                    }
                }
                else
                {
                    spawnPosExist = true;
                    spawnPos = nearPos[i];
                    break;
                }
            }
        }

        return spawnPosExist;
    }

    [ServerRpc(RequireOwnership = false)]
    public void UnitSpawnPosSetServerRpc(Vector2 _movePos)
    {
        UnitSpawnPosSetClientRpc(_movePos);
    }

    [ClientRpc]
    public void UnitSpawnPosSetClientRpc(Vector2 _movePos)
    {
        isSetPos = true;
        movePos = _movePos;
    }

    void SetUnit()
    {
        if (spawnUnit == null || (spawnUnit != null && (setUnitName != itemDic[recipe.items[3]].name)))
        {
            foreach (GameObject obj in unitObjList)
            {
                obj.TryGetComponent(out UnitAi unitAi);
                if (itemDic[recipe.items[3]].name == obj.name)
                {
                    spawnUnit = obj;
                    setUnitName = unitAi.unitName;
                }
            }
        }
    }

    void SpawnUnit()
    {
        GameObject unit = Instantiate(spawnUnit);
        Vector3 spawnSet = transform.position + ((Vector3)spawnPos - transform.position).normalized * UnitSpawnDistance;
        unit.transform.position = spawnSet;
        NetworkObject networkObject = unit.GetComponent<NetworkObject>();
        if (!networkObject.IsSpawned) networkObject.Spawn(true);

        unit.TryGetComponent(out UnitAi unitAi);
        unitAi.AStarSet(isInHostMap);
        if (isSetPos)
            unitAi.MovePosSetServerRpc(movePos, 0.1f, true);
    }

    public override void DestroyLineRenderer()
    {
        base.DestroyLineRenderer();
        isSetPos = false;
    }

    public override StructureSaveData SaveData()
    {
        StructureSaveData data = base.SaveData();

        if (isSetPos)
        {
            data.connectedStrPos.Add(Vector3Extensions.FromVector3(movePos));
        }

        return data;
    }

    protected override void NonOperateStateSet(bool isOn)
    {
        setModel.sprite = strImg[isOn ? 1 : 0];
    }

    protected override void FactoryOverlay()
    {
        if (!gameManager.overlayOn)
        {
            overlay.UIReset();
        }
        else
        {
            if (recipe.name != null && itemDic[recipe.items[3]])
                overlay.UISet(itemDic[recipe.items[3]]);
            else
                overlay.UIReset();
        }
    }
}