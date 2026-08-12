using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Production : Structure
{
    protected const int MaxStackAmount = 99;        // 슬롯당 최대 적재 수량 (자식 건물도 사용)
    protected const int DefaultMaxFuel = 100;       // 연료/버퍼 기본 최대치 (자식 건물도 사용)
    private const int MaxPopUpItemTypes = 5;        // 팝업에 표시할 아이템 종류 최대치
    private const float EfficiencyCheckInterval = 0.5f; // 효율 재계산 주기(초)

    // 연료(석탄, 전기), 작업 시간, 작업량, 재료, 생산품, 아이템 슬롯
    [SerializeField]
    protected GameObject ui;
    [SerializeField]
    protected StructureInvenManager sInvenManager;
    [SerializeField]
    public RecipeManager rManager;

    protected GameObject canvas;
    [HideInInspector]
    public Inventory inventory;
    protected Dictionary<string, Item> itemDic;
    public int fuel;
    protected int maxFuel;
    public Item output;
    protected Recipe recipe = new Recipe();
    public int recipeIndex = -1;
    protected List<Recipe> recipes;
    protected int invenCount;
    protected Dictionary<Item, int> invenSlotDic;
    protected int minerCellCount;

    [SerializeField]
    protected GameObject lineObj;
    [HideInInspector]
    public LineRenderer lineRenderer;
    protected Vector3 startLine;
    protected Vector3 endLine;
    public bool isGetLine;

    public bool isInvenFull = false;
    protected (Item, int) slot = (null, 0);
    protected (Item, int) slot1 = (null, 0);
    protected (Item, int) slot2 = (null, 0);
    protected (Item, int) slot3 = (null, 0);

    [SerializeField]
    protected SmokeControl smokeCtrl; // 연기 기능있는 건물만 사용

    protected override void Awake()
    {
        ProductionAwakeInit();
    }

    // Refinery/SteamGenerator처럼 Awake를 자체 오버라이드하는 자식이 부모 초기화를 그대로 재사용하도록 분리
    // (base.Awake() → Structure.Awake의 전체 초기화 + 인벤토리 세팅)
    protected void ProductionAwakeInit()
    {
        base.Awake();
        inventory = Get<Inventory>();
        if (inventory != null)
        {
            inventory.onItemChangedCallback += CheckSlotState;
            inventory.onItemChangedCallback += CheckInvenIsFull;
            CheckSlotState(0);
            CheckInvenIsFull(0);
        }
        isGetLine = false;
        isStorageBuilding = false;
    }

    protected virtual void Start()
    {
        gameManager = GameManager.instance;
        itemDic = ItemList.instance.itemDic;
        canvas = gameManager.inventoryUiCanvas;
        sInvenManager = canvas.GetComponent<StructureInvenManager>();
        rManager = canvas.GetComponent<RecipeManager>();
        GetUIFunc();

        if (IsServer)
            StrBuilt();
        else if (NetworkObjManager.instance.clientSyncComplete == true)
            StrBuilt();
    }

    protected override void Update()
    {
        base.Update();

        if (IsServer && !isPreBuilding)
        {
            if (canTakeItem && inObj.Count > 0 && !itemGetDelay)
                GetItem();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (inventory != null)
        {
            inventory.onItemChangedCallback -= CheckSlotState;
            inventory.onItemChangedCallback -= CheckInvenIsFull;
        }
    }

    public override void NearStrBuilt()
    {
        // 건물을 지었을 때나 근처에 새로운 건물이 지어졌을 때 동작
        // 변경사항이 생기면 DelayNearStrBuiltCoroutine()에도 반영해야 함
        if (IsServer)
        {
            CheckPos();
            for (int i = 0; i < nearObj.Length; i++)
            {
                if (!nearObj[i] && sizeOneByOne)
                {
                    CheckNearObj(checkPos[i], i, obj => StartCoroutine(SetOutObjCoroutine(obj)));
                }
                else if (!nearObj[i] && !sizeOneByOne)
                {
                    CheckNearObj(i, obj => StartCoroutine(SetOutObjCoroutine(obj)));
                }
            }
        }
        else
        {
            DelayNearStrBuilt();
        }
    }

    public override void DelayNearStrBuilt()
    {
        // 동시 건설, 클라이언트 동기화 등의 이유로 딜레이를 주고 NearStrBuilt()를 실행할 때 사용
        StartCoroutine(DelayNearStrBuiltCoroutine());
    }

    protected override IEnumerator DelayNearStrBuiltCoroutine()
    {
        // 동시 건설이나 그룹핑을 따로 예외처리 하는 경우가 아니면 NearStrBuilt()를 그대로 사용
        yield return new WaitForEndOfFrame();

        CheckPos();
        for (int i = 0; i < nearObj.Length; i++)
        {
            if (!nearObj[i] && sizeOneByOne)
            {
                CheckNearObj(checkPos[i], i, obj => StartCoroutine(SetOutObjCoroutine(obj)));
            }
            else if (!nearObj[i] && !sizeOneByOne)
            {
                CheckNearObj(i, obj => StartCoroutine(SetOutObjCoroutine(obj)));
            }
        }
    }

    public virtual void CheckInvenIsFull(int slotIndex)
    {
        // 생산 건물인 경우 output slot은 체크하지 않게 설정해줘야 함
        for (int i = 0; i < inventory.space; i++)
        {
            if (inventory.SlotAmountCheck(i) < inventory.maxAmount)
            {
                isInvenFull = false;
                return;
            }
        }

        isInvenFull = true;
    }

    public override void ClientConnectSync()
    {
        var data = CollectBaseSyncData();

        data.itemIndexes = new int[0];

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

        ClientConnectSyncClientRpc(data);
    }

    protected override void ApplyItemSync(StructureSyncData data)
    {
        if (data.recipeIndex != -1)
        {
            SetRecipeFromIndex(data.recipeIndex);
        }

        if (inventory == null) return;

        inventory.ResetInven();

        int count = data.inventoryItemIndexes.Length;
        if (count == 0) return;

        Item[] items = new Item[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = GeminiNetworkManager.instance.GetItemSOFromIndex(data.inventoryItemIndexes[i]);
        }
        inventory.NonNetSlotsAdd(data.inventorySlotNums, items, data.inventoryItemAmounts, count);
    }

    public virtual void SetRecipe(Recipe _recipe, int index)
    {
        recipe = _recipe;
        recipeIndex = index;
        sInvenManager.ResetInvenOption();
        cooldown = recipe.cooldown;
        if (conn != null && conn.group != null)
            EfficiencyCheck();
        else
            effiCooldown = cooldown;

        if (isUIOpened)
        {
            float productionTime = effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount);
            float productionPerMin = (recipe.name != null) ? recipe.amounts[recipe.amounts.Count - 1] * (60 / productionTime) : 60 / productionTime;
            sInvenManager.progressBar.SetMaxProgress(productionTime);
            sInvenManager.SetCooldownText(productionTime, FormatFloat(productionPerMin));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetRecipeServerRpc(int index)
    {
        SetRecipeClientRpc(index);
    }

    [ClientRpc]
    public void SetRecipeClientRpc(int index)
    {
        SetRecipeFromIndex(index);
    }

    public void SetRecipeFromIndex(int index)
    {
        Recipe selectRecipe = RecipeList.instance.GetRecipeIndex(structureData.factoryName, index);

        if (itemDic == null)
            itemDic = ItemList.instance.itemDic;

        if (selectRecipe.name != "UICancel")
        {
            if (recipe != null && recipe != selectRecipe)
            {
                if (IsServer)
                {
                    AddInvenItem();
                }
                inventory.ResetInven();
                CheckSlotState(0);

                if (isUIOpened)
                {
                    sInvenManager.ClearInvenOption();
                }
            }

            if (selectRecipe != null)
            {
                if (isUIOpened)
                {
                    SetRecipe(selectRecipe, index);
                    SetOutput(selectRecipe);
                }
                else
                {
                    recipe = selectRecipe;
                    recipeIndex = index;
                    cooldown = recipe.cooldown;
                    if (conn != null && conn.group != null)
                        EfficiencyCheck();
                    else
                        effiCooldown = cooldown;
                    SetOutput(recipe);
                    if (sInvenManager)
                        sInvenManager.progressBar.SetProgress(0);
                }
                CheckInvenIsFull(0);
            }
            FactoryOverlay();
        }
        else
        {
            ResetUI();
            CheckSlotState(0);
            sInvenManager.dicBtn.gameObject.SetActive(false);
            overlay.UIReset();
        }
    }

    protected virtual void ResetUI()
    {
        recipe = new Recipe();
        recipeIndex = -1;
        output = null;
        cooldown = structureData.Cooldown;
        if (conn != null && conn.group != null)
            EfficiencyCheck();
        else
            effiCooldown = cooldown;
        prodTimer = 0;
        OperateStateSet(false);
        if (sInvenManager != null)
            sInvenManager.SetCooldownText(0);
        if (IsServer)
        {
            AddInvenItem();
        }
        inventory.ResetInven();
        if (isUIOpened)
        {
            sInvenManager.ClearInvenOption();
        }
    }

    public virtual void SetOutput(Recipe recipe)
    {
        // 건물 레시피 따라서 output을 바꿔줘야 하는 경우 오버라이딩
    }

    public override void GameStartRecipeSet(int recipeId)
    {
        if (recipeId != -1)
            SetRecipeClientRpc(recipeId);
    }

    public void GameStartItemSet(InventorySaveData data)
    {
        if (inventory != null)
            inventory.LoadData(data);
    }

    public virtual float GetProgress() { return prodTimer; }
    public virtual float GetFuel() { return fuel; }
    public virtual void OpenRecipe() { }
    public virtual void GetUIFunc() { }

    public virtual void OpenUI()
    {
        isUIOpened = true;
        ClientUISetServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    protected void ClientUISetServerRpc()
    {
        ClientUISetClientRpc(fuel, prodTimer, cooldown, effiCooldown);
    }

    [ClientRpc]
    protected void ClientUISetClientRpc(int syncFuel, float syncTimer, float syncCooldown, float syncEffiCooldown)
    {
        if (IsServer)
            return;

        fuel = syncFuel;
        prodTimer = syncTimer;
        cooldown = syncCooldown;
        effiCooldown = syncEffiCooldown;
    }

    public virtual void CloseUI()
    {
        isUIOpened = false;
        sInvenManager.dicBtn.gameObject.SetActive(false);
        GameManager.instance.CheckAndCancelFocus(this);
    }

    protected override IEnumerator SetOutObjCoroutine(Structure obj)
    {
        yield return new WaitForSeconds(SetOutObjDelay);

        if (!obj)
            yield break;

        if (obj.TryGet<BeltCtrl>(out var belt))
        {
            if (belt.beltGroupMgr.nextObj == this && (belt.beltState == BeltState.EndBelt || belt.beltState == BeltState.SoloBelt))
            {
                StartCoroutine(SetInObjCoroutine(obj));
                yield break;
            }
            if (!outObj.Contains(obj))
                outObj.Add(obj);
            belt.FactoryPosCheck(this);
        }
        else
        {
            outSameList.Add(obj);
            StartCoroutine(OutCheck(obj));
            StartCoroutine(UnderBeltConnectCheck(obj));
        }
    }

    public override void OnFactoryItem(ItemProps itemProps)
    {
        if (IsServer)
        {
            for (int i = 0; i < inventory.space; i++)
            {
                if (itemDic[recipe.items[i]] == itemProps.item)
                {
                    inventory.SlotAdd(i, itemProps.item, itemProps.amount);
                    break;
                }
            }
        }

        base.OnFactoryItem(itemProps);
    }

    public override void OnFactoryItem(Item item)
    {
        if (IsServer)
        {
            for (int i = 0; i < inventory.space; i++)
            {
                if (itemDic[recipe.items[i]] == item)
                {
                    inventory.SlotAdd(i, item, 1);
                    break;
                }
            }
        }
    }

    protected override void SubFromInventory()
    {
        if (IsServer)
            inventory.SlotSubServerRpc(inventory.space - 1, 1);
    }

    public virtual bool CanTakeItem(Item item)
    {
        if (isInvenFull) return false;

        if (recipe == null || recipe.items == null)
            return false;

        for (int i = 0; i < inventory.space - 1; i++)
        {
            var slot = inventory.SlotCheck(i);
            if (itemDic[recipe.items[i]] == item && slot.amount < MaxStackAmount)
                return true;
        }
        return false;
    }

    public override bool CheckOutItemNum()
    {
        var slot = inventory.SlotCheck(inventory.space - 1);
        if (slot.amount > 0)
            return true;
        else
            return false;
    }

    protected override void GetItemFunc(int inObjIndex)
    {
        if (inObj[inObjIndex].TryGet(out BeltCtrl belt))
        {
            if (belt.itemObjList.Count > 0 && CanTakeItem(belt.itemObjList[0].item))
            {
                GetItemFuncServerRpc(inObjIndex);
            }
        }
        DelayGetItem();
    }

    public override void AddInvenItem()
    {
        base.AddInvenItem();
        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);

            if (invenItem.item != null && invenItem.amount > 0)
            {
                playerInven.Add(invenItem.item, invenItem.amount);
            }
        }
    }

    public void SortInven()
    {
        if (!ItemDragManager.instance.isDrag)
        {
            inventory.SortServerRpc();
        }
    }

    public override Dictionary<Item, int> PopUpItemCheck()
    {
        Dictionary<Item, int> returnDic = new Dictionary<Item, int>();

        int itemsCount = 0;
        //다른 슬롯의 같은 아이템도 개수 추가하도록
        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);

            if (invenItem.item != null && invenItem.amount > 0)
            {
                if (!returnDic.ContainsKey(invenItem.item))
                {
                    returnDic.Add(invenItem.item, invenItem.amount);
                    itemsCount++;
                }
                else
                {
                    returnDic[invenItem.item] += invenItem.amount;
                }

                if (itemsCount > MaxPopUpItemTypes)
                    break;
            }
        }

        if (returnDic.Count > 0)
        {
            return returnDic;
        }
        else
            return null;
    }

    public bool UnloadItemCheck(Item item)
    {
        bool canUnload = false;
        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);
            if (invenItem.item == item && invenItem.amount > 0)
            {
                canUnload = true;
                break;
            }
        }
        return canUnload;
    }

    public void UnloadItem(Item item)
    {
        if (IsServer)
            inventory.Sub(item, 1);
    }

    public void LineRendererSet(Vector2 endPos)
    {
        if (endPos != Vector2.zero && lineRenderer == null)
        {
            startLine = new Vector3(transform.position.x, transform.position.y, -1);
            endLine = new Vector3(endPos.x, endPos.y, -1);

            GameObject currentLine = Instantiate(lineObj, startLine, Quaternion.identity);
            lineRenderer = currentLine.GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startLine);
            lineRenderer.SetPosition(1, endLine);
        }
    }

    public virtual void DestroyLineRenderer()
    {
        if (lineRenderer != null)
        {
            Destroy(lineRenderer.gameObject);
            lineRenderer = null;
        }
    }

    public void ResetLine(Vector2 endPos)
    {
        DestroyLineRenderer();
        LineRendererSet(endPos);
    }

    public override IEnumerator EfficiencyCheckLoop()
    {
        while (true)
        {
            if (conn != null && conn.group != null)
            {
                if (conn.group.efficiency > MinEfficiency)
                {
                    efficiency = conn.group.efficiency;
                    effiCooldown = cooldown / efficiency;

                    if (isUIOpened)
                    {
                        if (buildName != "Miner")
                        {
                            float productionTime = effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount);
                            float productionPerMin = (recipe.name != null) ? recipe.amounts[recipe.amounts.Count - 1] * (60 / productionTime) : 60 / productionTime;
                            sInvenManager.progressBar.SetMaxProgress(productionTime);
                            sInvenManager.SetCooldownText(productionTime, FormatFloat(productionPerMin));
                        }
                        else
                        {
                            float productionTime = effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount);
                            float productionPerMin = minerCellCount * (60 / productionTime);
                            sInvenManager.progressBar.SetMaxProgress(productionTime);
                            sInvenManager.SetCooldownText(productionTime, FormatFloat(productionPerMin));
                        }
                    }
                }
                else
                {
                    efficiency = 0;
                    effiCooldown = cooldown;
                }
            }

            yield return new WaitForSecondsRealtime(EfficiencyCheckInterval);
        }
    }

    public override void EfficiencyCheck()
    {
        if (conn != null && conn.group != null)
        {
            if (conn.group.efficiency > MinEfficiency)
            {
                efficiency = conn.group.efficiency;
                effiCooldown = cooldown / efficiency;
            }
            else
            {
                efficiency = 0;
                effiCooldown = cooldown;
            }
        }
    }

    [ServerRpc]
    public void OverclockSyncServerRpc(bool isOn)
    {
        OverclockSyncClientRpc(isOn);
    }

    [ClientRpc]
    void OverclockSyncClientRpc(bool isOn)
    {
        overclockOn = isOn;
        if (isUIOpened)
        {
            if (buildName != "Miner")
            {
                float productionTime = effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount);
                float productionPerMin = (recipe.name != null) ? recipe.amounts[recipe.amounts.Count - 1] * (60 / productionTime) : 60 / productionTime;
                sInvenManager.progressBar.SetMaxProgress(productionTime);
                sInvenManager.SetCooldownText(productionTime, FormatFloat(productionPerMin));
            }
            else
            {
                float productionTime = effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount);
                float productionPerMin = minerCellCount * (60 / productionTime);
                sInvenManager.progressBar.SetMaxProgress(productionTime);
                sInvenManager.SetCooldownText(productionTime, FormatFloat(productionPerMin));
            }
        }
    }

    protected override void ItemDrop()
    {
        if (inventory == null)
        {
            return;
        }

        for (int i = 0; i < inventory.space; i++)
        {
            var invenItem = inventory.SlotCheck(i);

            if (invenItem.item != null && invenItem.amount > 0)
            {
                ItemToItemProps(invenItem.item, invenItem.amount);
            }
        }
    }

    public override StructureSaveData SaveData()
    {
        StructureSaveData data = base.SaveData();

        if(TryGet(out Inventory inventory))
        {
            data.inven = inventory.SaveData();
        }

        data.recipeId = recipeIndex;
        data.prodTimer = prodTimer;
        data.fuel = fuel;

        return data;
    }

    protected override void FactoryOverlay()
    {
        if (!gameManager.overlayOn)
        {
            overlay.UIReset();
        }
        else
        {
            if (output)
                overlay.UISet(output);
            else
                overlay.UIReset();
        }
    }
}
