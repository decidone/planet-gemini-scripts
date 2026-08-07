using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Slot : MonoBehaviour
{
    public delegate void OnSlotChanged();
    public OnSlotChanged onSlotChangedCallback;

    public Image icon;
    public Text amountText;
    public Text reqAmountText;
    public Item item;
    public List<Item> inputItem;  //inputSlot 받는 아이템
    public int amount;
    public int needAmount;
    public int slotNum;
    public bool inputSlot;
    public bool outputSlot;
    public string inGameName;

    // 건물 UI 전용
    public bool strDataSet;
    public bool isBuildingUI;
    public bool isEnergyUse; 
    public bool isEnergyStr;
    public float energyConsumption;
    public float energyProduction;
    public string throughput;

    private void OnEnable()
    {
        onSlotChangedCallback += SlotChanged;
        onSlotChangedCallback?.Invoke();
    }

    private void OnDisable()
    {
        onSlotChangedCallback -= SlotChanged;
    }

    void SlotChanged()
    {
        if (inputSlot)
        {
            if (inputItem.Count == 1)
            {
                Color color = icon.color;
                icon.sprite = inputItem[0].icon;
                icon.enabled = true;

                if (amount == 0)
                {
                    color.a = 0.5f;
                    if (needAmount != 0)
                    {
                        amountText.text = needAmount.ToString();
                        amountText.enabled = true;
                    }
                }
                else
                {
                    color.a = 1f;
                }
                icon.color = color;
            }
        }
    }

    public void AddItem(Item newItem, int itemAmount, string gameName)
    {
        AddItem(newItem, itemAmount);
        inGameName = gameName;
    }

    public void AddItem(Item newItem, int itemAmount)
    {
        item = newItem;
        amount = itemAmount;
        string name = InGameNameDataGet.instance.ReturnName(1, newItem.name);
        inGameName = name;

        icon.sprite = item.icon;
        icon.enabled = true;
        amountText.text = amount.ToString();
        amountText.enabled = true;

        onSlotChangedCallback?.Invoke();
    }

    public void ClearSlot()
    {
        item = null;
        amount = 0;

        icon.sprite = null;
        icon.enabled = false;
        amountText.text = null;
        amountText.enabled = false;
        onSlotChangedCallback?.Invoke();
    }

    public void ResetOption()
    {
        inputSlot = false;
        inputItem.Clear();
        outputSlot = false;
        reqAmountText.text = null;
        reqAmountText.enabled = false;
    }

    public void SetInputItem(Item _item)
    {
        inputSlot = true;
        if (!inputItem.Contains(_item))
            inputItem.Add(_item);

        onSlotChangedCallback?.Invoke();
    }

    public void SetInputItem(List<Item> items)
    {
        inputSlot = true;

        List<Item> itemTemp = new List<Item>(items);
        inputItem = itemTemp;

        onSlotChangedCallback?.Invoke();
    }

    public void SetItemAmount(int _amount) //디스플레이 슬롯용
    {
        amount = _amount;
        amountText.text = _amount + "";

        onSlotChangedCallback?.Invoke();
    }

    public void SetNeedAmount(int _needAmount) //디스플레이 슬롯용
    {
        needAmount = _needAmount;
        reqAmountText.enabled = true;
        reqAmountText.text = needAmount.ToString();

        onSlotChangedCallback?.Invoke();
    }

    public void SetReqAmount(int reqAmount) //슬롯 옆에 아이템 요구량 표시
    {
        reqAmountText.enabled = true;
        reqAmountText.text = reqAmount.ToString();

        onSlotChangedCallback?.Invoke();
    }

    public void StrData(Building building)
    {
        isEnergyStr = false;
        isEnergyUse = false;
        strDataSet = false;

        isEnergyStr = building.isEnergyStr;
        isEnergyUse = building.isEnergyUse;
        building.gameObj.TryGetComponent(out Structure str);

        if (str.structureData.Throughput.Length > 0)
        {
            throughput = str.structureData.Throughput[building.level - 1];
            strDataSet = true;
        }

        if (!isEnergyStr && !isEnergyUse)
            return;

        energyConsumption = str.structureData.Consumption[building.level - 1];
        energyProduction = str.structureData.Production;

        if (isEnergyStr && energyProduction == 0)
            return;


        strDataSet = true;
    }
}
