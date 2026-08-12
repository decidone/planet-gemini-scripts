using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnergyGenerator : Production
{
    private const int FuelPerItem = 50;     // 연료 아이템 1개당 충전되는 연료량

    public EnergyGroupConnector connector;
    public Item FuelItem;
    bool isBuildDone;
    PreBuilding preBuilding;
    Structure preBuildingStr;
    bool preBuildingCheck;
    public int fuelRequirement;

    protected override void Start()
    {
        base.Start();
        maxFuel = DefaultMaxFuel;
        isBuildDone = false;
        preBuildingCheck = false;
        gameManager = GameManager.instance;
        preBuilding = PreBuilding.instance;
        view.enabled = false;
    }

    protected override void Update()
    {
        base.Update();

        if (gameManager.focusedStructure == null)
        {
            if (preBuilding.isBuildingOn && !removeState)
            {
                if (!preBuildingCheck)
                {
                    preBuildingCheck = true;
                    if (preBuilding.isEnergyUse || preBuilding.isEnergyStr)
                    {
                        view.enabled = true;
                    }
                }
            }
            else
            {
                if (preBuildingCheck)
                {
                    preBuildingCheck = false;
                    view.enabled = false;
                }
            }
        }
        if (!isPreBuilding)
        {
            if (!isBuildDone)
            {
                connector.Init();
                isBuildDone = true;
            }

            if (fuel <= 50 && slot.Item1 == FuelItem && slot.Item2 > 0)
            {
                if (IsServer)
                {
                    Overall.instance.OverallConsumption(slot.Item1, 1);
                    inventory.SlotSubServerRpc(0, 1);
                }
                fuel += FuelPerItem;
                if (isUIOpened)
                    sInvenManager.SetCooldownText(fuel + "/" + maxFuel);
                soundManager.PlaySFX(gameObject, "structureSFX", "Flames");
            }

            prodTimer += Time.deltaTime;
            if (prodTimer > cooldown)
            {
                if (fuel >= fuelRequirement && !destroyStart)
                {
                    fuel -= fuelRequirement;
                    if (isUIOpened)
                        sInvenManager.SetCooldownText(fuel + "/" + maxFuel);
                    OperateStateSet(true);
                    prodTimer = 0;
                }
                else
                {
                    OperateStateSet(false);
                }
            }
        }

        if (connector != null && connector.group != null)
        {
            if (removeState)
                connector.RemoveFromGroup();
        }
    }

    public override void CheckSlotState(int slotindex)
    {
        // update에서 검사해야 하는 특정 슬롯들 상태를 인벤토리 콜백이 있을 때 미리 저장
        slot = inventory.SlotCheck(0);
    }

    protected override IEnumerator CheckWarning()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);

            if (!isPreBuilding && !removeState && settingEndCheck)
            {
                if (fuel > 0)
                {
                    if (warningIconCheck)
                    {
                        if (warning != null)
                        {
                            StopCoroutine(warning);
                            StrWarningManager.instance.RemoveStrList(this);
                        }
                        warningIconCheck = false;
                        warningIcon.enabled = false;
                        mapWarningIcon.enabled = false;
                    }
                }
                else
                {
                    if (!warningIconCheck)
                    {
                        if (warning != null)
                        {
                            StopCoroutine(warning);
                            StrWarningManager.instance.RemoveStrList(this);
                        }
                        warning = FlickeringIcon();
                        StartCoroutine(warning);
                        warningIconCheck = true;
                        StrWarningManager.instance.AddStrList(this);
                        mapWarningIcon.sprite = warningIcon.sprite;
                        mapWarningIcon.enabled = true;
                    }
                }
            }
        }
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

        data.fuel = this.fuel;

        ClientConnectSyncClientRpc(data);
    }

    protected override void ApplyExtraSync(StructureSyncData data)
    {
        fuel = data.fuel;
    }

    public override float GetProgress() { return fuel; }

    public override void Focused()
    {
        if (connector.group != null)
        {
            connector.group.TerritoryViewOn();
        }
    }

    public override void DisableFocused()
    {
        if (connector.group != null)
        {
            connector.group.TerritoryViewOff();
        }
    }

    public override void RemoveObjServer()
    {
        DisableFocused();
        connector.RemoveFromGroup();

        RemoveObjClientRpc();
    }

    public override void OpenUI()
    {
        base.OpenUI();
        sInvenManager.SetInven(inventory, ui);
        sInvenManager.SetProd(this);
        sInvenManager.progressBar.SetMaxProgress(100);
        sInvenManager.SetCooldownText(fuel + "/" + maxFuel);
        sInvenManager.slots[0].SetInputItem(FuelItem);
    }

    public override void CloseUI()
    {
        base.CloseUI();
        sInvenManager.SetCooldownText(string.Empty);
        sInvenManager.ReleaseInven();
    }

    public override bool CanTakeItem(Item item)
    {
        if (isInvenFull) return false;

        if (FuelItem == item && slot.Item2 < MaxStackAmount)
            return true;

        return false;
    }

    public override void OnFactoryItem(ItemProps itemProps)
    {
        if (IsServer && FuelItem == itemProps.item)
            inventory.SlotAdd(0, itemProps.item, itemProps.amount);

        itemProps.ClientResetItemProps();
    }

    public override void OnFactoryItem(Item item)
    {
        if (IsServer && FuelItem == item)
            inventory.SlotAdd(0, item, 1);
    }

    public override void GetUIFunc()
    {
        InventoryList inventoryList = canvas.GetComponent<InventoryList>();

        foreach (GameObject list in inventoryList.StructureStorageArr)
        {
            if (list.name == "Generator")
            {
                ui = list;
            }
        }
    }

    public override (bool, bool, bool, EnergyGroup, float) PopUpEnergyCheck()
    {
        if (connector != null && connector.group != null)
        {
            return (energyUse, isEnergyStr, false, connector.group, energyProduction);
        }

        return (false, false, false, null, 0);
    }

    protected override void NonOperateStateSet(bool isOn)
    {
        setModel.sprite = strImg[isOn ? 1 : 0];
        smokeCtrl.SetSmokeActive(isOn);
    }
}
