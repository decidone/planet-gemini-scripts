using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using static Inventory;

public class Inventory : NetworkBehaviour
{
    public delegate void OnItemChanged(int slotindex);
    public OnItemChanged onItemChangedCallback;

    public delegate void InvenAllSlotUpdate();
    public InvenAllSlotUpdate invenAllSlotUpdate;

    public int space;   // 아이템 슬롯 상한, 드래그용 슬롯 번호를 겸 함
    public int maxAmount;   // 한 슬롯 당 최대 수량
    [SerializeField]
    GameObject itemPref;

    // 인벤토리에 표시되는 아이템
    public Dictionary<int, Item> items = new Dictionary<int, Item>();
    public Dictionary<int, int> amounts = new Dictionary<int, int>();

    // 아이템 총량 관리
    public ItemListSO itemListSO;
    List<Item> itemList;
    public Dictionary<Item, int> totalItems = new Dictionary<Item, int>();

    MerchandiseListSO merchandiseListSO;
    List<Merchandise> merchandiseList;

    ulong[] singleTarget = new ulong[1];

    bool portalInven = false;

    void Awake()
    {
        itemListSO = Resources.Load<ItemListSO>("SOList/ItemListSO");
        itemList = itemListSO.itemSOList;
        merchandiseListSO = Resources.Load<MerchandiseListSO>("SOList/MerchandiseListSO");
        merchandiseList = merchandiseListSO.MerchandiseSOList;

        if (totalItems.Count == 0)
        {
            foreach (Item item in itemList)
            {
                totalItems.Add(item, 0);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    public int SpaceCheck(Item item)
    {
        int containableAmount = 0;
        for (int i = 0; i < space; i++)
        {
            if (items.ContainsKey(i))
            {
                if (items[i] == item)
                {
                    containableAmount += (maxAmount - amounts[i]);
                }
            }
            else
            {
                containableAmount += maxAmount;
            }
        }

        return containableAmount;
    }

    public bool IsInvenEmpty()
    {
        if (items.Count == 0)
            return true;
        else
            return false;
    }

    public bool MultipleSpaceCheck(Merch[] merchList)
    {
        // 각 상품들은 99개를 초과하지 않음
        bool containable;
        int emptySlots = space - items.Count;
        int emptySlotsRequirement = 0;

        foreach (Merch merch in merchList)
        {
            int merchItemSlotRemaining = 0;

            for (int i = 0; i < space; i++)
            {
                if (items.ContainsKey(i))
                {
                    if (items[i] == merch.item)
                    {
                        merchItemSlotRemaining += (maxAmount - amounts[i]);
                    }
                }
            }
            if (merch.amount > merchItemSlotRemaining)
            {
                emptySlotsRequirement++;
            }
        }

        if (emptySlots < emptySlotsRequirement)
            containable = false;
        else
            containable = true;
        
        return containable;
    }

    public bool MultipleSpaceCheck(int[] merchNum, int[] merchAmount)
    {
        // 각 상품들은 99개를 초과하지 않음
        bool containable;
        int emptySlots = space - items.Count;
        int emptySlotsRequirement = 0;

        for (int i = 0; i < merchNum.Length; i++)
        {
            int merchItemSlotRemaining = 0;

            for (int j = 0; j < space; j++)
            {
                if (items.ContainsKey(j))
                {
                    if (items[j] == merchandiseList[merchNum[i]].item)
                    {
                        merchItemSlotRemaining += (maxAmount - amounts[j]);
                    }
                }
            }
            if (merchAmount[i] > merchItemSlotRemaining)
            {
                emptySlotsRequirement++;
            }
        }

        if (emptySlots < emptySlotsRequirement)
            containable = false;
        else
            containable = true;

        return containable;
    }

    public void Add(Item item, int amount)
    {
        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
        AddServerRpc(itemIndex, amount);
    }

    public void StorageAdd(Item item, int amount)
    {
        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
        StorageAddServerRpc(itemIndex, amount);
    }

    public void RecipeInvenAdd(Item item, int amount)
    {
        for (int i = 0; i < space; i++)
        {
            if (!items.ContainsKey(i))
            {
                NonNetSlotAdd(i, item, amount);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddServerRpc(int itemIndex, int amount, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        bool isHost = clientId == 0;

        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        int containableAmount = SpaceCheck(item);
        int totalAmount = amount;

        if (containableAmount < amount)
        {
            totalAmount = containableAmount;
            int dropAmount = amount - containableAmount;
            Drop(item, dropAmount, isHost);
        }

        // 2. 이미 있던 칸에 수량 증가
        for (int i = 0; i < space; i++)
        {
            if (items.ContainsKey(i))
            {
                if (items[i] == item)
                {
                    int tempAmount = amounts[i];
                    if (tempAmount + totalAmount <= maxAmount)
                    {
                        SlotAdd(i, item, totalAmount);
                        totalAmount = 0;
                    }
                    else
                    {
                        SlotAdd(i, item, maxAmount - tempAmount);
                        totalAmount -= (maxAmount - tempAmount);
                    }
                }
            }
            if (totalAmount == 0)
                break;
        }

        // 3. 2를 처리하고 남은 수량만큼 빈 칸에 배정
        if (totalAmount > 0)
        {
            for (int i = 0; i < space; i++)
            {
                if (!items.ContainsKey(i))
                {
                    if (totalAmount <= maxAmount)
                    {
                        SlotAdd(i, item, totalAmount);
                        totalAmount = 0;
                    }
                    else
                    {
                        SlotAdd(i, item, maxAmount);
                        totalAmount -= maxAmount;
                    }
                }
                if (totalAmount <= 0)
                    break;
            }

            if (totalAmount > 0)
            {
                Drop(item, totalAmount, isHost);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StorageAddServerRpc(int itemIndex, int amount, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        bool isHost = clientId == 0;

        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        int tempAmount = amount;

        // 2. 이미 있던 칸에 수량 증가
        for (int i = 0; i < space; i++)
        {
            if (items.ContainsKey(i))
            {
                if (items[i] == item)
                {
                    if (amounts[i] + tempAmount <= maxAmount)
                    {
                        SlotAdd(i, item, tempAmount);
                        tempAmount = 0;
                    }
                    else
                    {
                        SlotAdd(i, item, maxAmount - amounts[i]);
                        tempAmount -= (maxAmount - amounts[i]);
                    }
                }
            }
            if (tempAmount == 0)
                break;
        }

        // 3. 2를 처리하고 남은 수량만큼 빈 칸에 배정
        if (tempAmount > 0)
        {
            for (int i = 0; i < space; i++)
            {
                if (!items.ContainsKey(i))
                {
                    if (tempAmount <= maxAmount)
                    {
                        SlotAdd(i, item, tempAmount);
                        tempAmount = 0;
                    }
                    else
                    {
                        SlotAdd(i, item, maxAmount);
                        tempAmount -= maxAmount;
                    }
                }
                if (tempAmount <= 0)
                    break;
            }

            if (tempAmount > 0)
            {
                Drop(item, tempAmount, isHost);
            }
        }
    }

    [ClientRpc]
    public void DisplayLootInfoClientRpc(int itemIndex, int amount, ClientRpcParams rpcParams = default)
    {
        QuestManager.instance.QuestCompCheck(12);
        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        LootListManager.instance.DisplayLootInfo(item, amount);
    }

    [ClientRpc(RequireOwnership = false)]
    public void DisplayDestroyLootInfoClientRpc(int itemIndex, int amount, int destroyRequestedBy)
    {
        if (destroyRequestedBy == 0 && IsServer)
        {
            Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
            LootListManager.instance.DisplayLootInfo(item, amount);
        }
        else if (destroyRequestedBy == 1 && !IsServer)
        {
            Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
            LootListManager.instance.DisplayLootInfo(item, amount);
        }
    }

    public void SwapOrMerge(int slotNum)
    {
        SwapOrMergeServerRpc(slotNum, GameManager.instance.isHost);
    }

    [ServerRpc (RequireOwnership = false)]
    public void SwapOrMergeServerRpc(int slotNum, bool isHostRequest)
    {
        Item dragItem = ItemDragManager.instance.GetItem(isHostRequest);

        if (dragItem == null)
        {
            if (items.ContainsKey(slotNum))
            {
                Swap(slotNum, isHostRequest);
            }
        }
        else
        {
            if (!items.ContainsKey(slotNum))
            {
                Swap(slotNum, isHostRequest);
            }
            else
            {
                if (dragItem != items[slotNum])
                {
                    Swap(slotNum, isHostRequest);
                }
                else
                {
                    Merge(slotNum, isHostRequest);
                }
            }
        }
    }

    public void Swap(int slotNum, bool isHostRequest)
    {
        if (!IsHost) return;

        Item dragItem = ItemDragManager.instance.GetItem(isHostRequest);
        int dragAmount = ItemDragManager.instance.GetAmount(isHostRequest);

        if (!items.ContainsKey(slotNum))
        {
            // 타겟 슬롯이 비어있는 경우
            SlotAdd(slotNum, dragItem, dragAmount);
            ItemDragManager.instance.Clear(isHostRequest);
        }
        else if ((!ItemDragManager.instance.IsDragging(isHostRequest) && isHostRequest) || (!ItemDragManager.instance.IsDragging(isHostRequest) && !isHostRequest))
        {
            // 드래그 슬롯이 비어있는 경우

            bool planet;
            if (TryGetComponent(out Structure structure))
            {
                planet = structure.isInHostMap;
            }
            else
            {
                planet = GameManager.instance.hostMapInven == this;
            }

            ItemDragManager.instance.Add(items[slotNum], amounts[slotNum], isHostRequest, planet);
            RemoveServerRpc(slotNum);
        }
        else
        {
            Item tempItem = items[slotNum];
            int tempAmount = amounts[slotNum];
            RemoveServerRpc(slotNum);
            SlotAdd(slotNum, dragItem, dragAmount);
            ItemDragManager.instance.Clear(isHostRequest);

            bool planet;
            if (TryGetComponent(out Structure structure))
            {
                planet = structure.isInHostMap;
            }
            else
            {
                planet = GameManager.instance.hostMapInven == this;
            }
            ItemDragManager.instance.Add(tempItem, tempAmount, isHostRequest, planet);
        }
    }

    public void Merge(int slotNum, bool isHostRequest)
    {
        if (!IsHost) return;

        Item dragItem = ItemDragManager.instance.GetItem(isHostRequest);
        int dragAmount = ItemDragManager.instance.GetAmount(isHostRequest);
        int mergeAmount = dragAmount + amounts[slotNum];

        if (mergeAmount > maxAmount)
        {
            int tempAmount = amounts[slotNum];
            SlotAdd(slotNum, dragItem, maxAmount - amounts[slotNum]);
            ItemDragManager.instance.Sub(maxAmount - tempAmount, isHostRequest);
        }
        else
        {
            SlotAdd(slotNum, dragItem, dragAmount);
            ItemDragManager.instance.Clear(isHostRequest);
        }
    }

    public (Item item, int amount) SlotCheck(int slotNum)
    {
        Item item = null;
        int amount = 0;

        if (items.ContainsKey(slotNum))
        {
            item = items[slotNum];
            amount = amounts[slotNum];
        }

        return (item, amount);
    }

    public int SlotAmountCheck(int slotNum)
    {
        int amount = 0;
        if (amounts.ContainsKey(slotNum))
            amount = amounts[slotNum];

        return amount;
    }

    public void SlotAdd(int slotNum, Item item, int amount)
    {
        // 서버 검증작업 문제없이 다 처리됨
        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
        SlotAddServerRpc(slotNum, itemIndex, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SlotAddServerRpc(int slotNum, int itemIndex, int amount)
    {
        if (!portalInven) 
            SlotAddClientRpc(slotNum, itemIndex, amount);
        else
        {
            GameManager.instance.WaveSkipItemAddServerRpc(slotNum, itemIndex, amount);
        }
    }

    [ClientRpc]
    public void SlotAddClientRpc(int slotNum, int itemIndex, int amount)
    {
        if (TryGetComponent(out Structure structure))
        {
            if (!IsServer && !structure.settingEndCheck)
                return;
        }

        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        if (!items.ContainsKey(slotNum))
        {
            items.Add(slotNum, item);
            amounts.Add(slotNum, amount);
            totalItems[item] += amount;
        }
        else if (items[slotNum] == item)
        {
            amounts[slotNum] += amount;
            totalItems[items[slotNum]] += amount;
        }
        else
        {
            Debug.Log("Slot Add Error");
        }

        onItemChangedCallback?.Invoke(slotNum);
    }

    public void NonNetSlotsAdd(int[] slotNum, Item[] item, int[] amount, int index) // 레시피나 로드된 유저의 인벤토리용
    {
        for (int i = 0; i < index; i++)
        {
            if (!item[i])
                continue;
            else if (!items.ContainsKey(slotNum[i]))
            {
                items.Add(slotNum[i], item[i]);
                amounts.Add(slotNum[i], amount[i]);
                totalItems[item[i]] += amount[i];
            }
            else if (items[slotNum[i]] == item[i])
            {
                amounts[slotNum[i]] += amount[i];
                totalItems[items[slotNum[i]]] += amount[i];
            }
            else
            {
                Debug.Log("Slot Add Error");
            }
        }

        invenAllSlotUpdate?.Invoke();
    }

    public void NonNetSlotAdd(int slotNum, Item item, int amount) // 레시피나 로드된 유저의 인벤토리용
    {
        if (!items.ContainsKey(slotNum))
        {
            items.Add(slotNum, item);
            amounts.Add(slotNum, amount);
            totalItems[item] += amount;
        }
        else if (items[slotNum] == item)
        {
            amounts[slotNum] += amount;
            totalItems[items[slotNum]] += amount;
        }
        else
        {
            Debug.Log("Slot Add Error");
        }

        onItemChangedCallback?.Invoke(slotNum);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SlotSubServerRpc(int slotNum, int amount)
    {
        if (!portalInven)
            SlotSubClientRpc(slotNum, amount);
        else
            GameManager.instance.WaveSkipItemSubServerRpc(slotNum, amount);
    }

    [ClientRpc]
    public void SlotSubClientRpc(int slotNum, int amount)
    {
        if (TryGetComponent(out Structure structure))
        {
            if (!IsServer && !structure.settingEndCheck)
                return;
        }
        
        totalItems[items[slotNum]] -= amount;
        amounts[slotNum] -= amount;
        if (amounts[slotNum] == 0)
        {
            items.Remove(slotNum);
            amounts.Remove(slotNum);
        }

        onItemChangedCallback?.Invoke(slotNum);
    }

    public void Sub(Item item, int amount)
    {
        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
        SubServerRpc(itemIndex, amount);
    }

    [ServerRpc (RequireOwnership = false)]
    public void SubServerRpc(int itemIndex, int amount)
    {
        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        int slotNum = FindItemSlot(item);
        if (slotNum != -1)
        {
            var slotData = SlotCheck(slotNum);
            if (slotData.amount >= amount)
            {
                SlotSubServerRpc(slotNum, amount);
            }
            else
            {
                SlotSubServerRpc(slotNum, slotData.amount);
                Sub(item, amount - slotData.amount);
            }
        }
    }

    int FindItemSlot(Item item)
    {
        for (int i = 0; i < space; i++)
        {
            if (items.ContainsKey(i))
            {
                if (items[i] == item)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    public void Split(int slotNum)
    {
        SplitServerRpc(slotNum);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SplitServerRpc(int slotNum, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        bool isHost = clientId == 0;

        int dragAmount = ItemDragManager.instance.GetAmount(isHost);

        if (items.ContainsKey(slotNum))
        {
            if (amounts[slotNum] > 0)
            {
                if (dragAmount < maxAmount)
                {
                    bool planet;
                    if (TryGetComponent(out Structure structure))
                    {
                        planet = structure.isInHostMap;
                    }
                    else
                    {
                        planet = GameManager.instance.hostMapInven == this;
                    }
                    ItemDragManager.instance.Add(items[slotNum], 1, isHost, planet);
                    SlotSubServerRpc(slotNum, 1);
                }
            }
        }
    }

    public void SplitHalf(int slotNum)
    {
        SplitHalfServerRpc(slotNum);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SplitHalfServerRpc(int slotNum, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        bool isHost = clientId == 0;

        int dragAmount = ItemDragManager.instance.GetAmount(isHost);

        if (items.ContainsKey(slotNum))
        {
            if (amounts[slotNum] > 1)
            {
                if (dragAmount < maxAmount)
                {
                    bool planet;
                    if (TryGetComponent(out Structure structure))
                    {
                        planet = structure.isInHostMap;
                    }
                    else
                    {
                        planet = GameManager.instance.hostMapInven == this;
                    }
                    ItemDragManager.instance.Add(items[slotNum], amounts[slotNum]/2, isHost, planet);
                    SlotSubServerRpc(slotNum, amounts[slotNum] / 2);
                }
            }
        }
    }

    public void LootItem(GameObject itemObj)
    {
        LootItemServerRpc(itemObj.GetComponent<NetworkObject>());
    }

    [ServerRpc(RequireOwnership = false)]
    public void LootItemServerRpc(NetworkObjectReference itemObjNetworkObjectReference, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        itemObjNetworkObjectReference.TryGet(out NetworkObject itemObj);

        if (itemObj != null)
        {
            ItemProps itemProps = itemObj.GetComponent<ItemProps>();
            if (itemProps.waitingForDestroy)
                return;

            singleTarget[0] = clientId;
            ClientRpcParams rpcParams = default;
            rpcParams.Send.TargetClientIds = singleTarget;

            int containableAmount = SpaceCheck(itemProps.item);
            int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(itemProps.item);

            if (itemProps.amount <= containableAmount)
            {
                itemProps.waitingForDestroy = true;
                Add(itemProps.item, itemProps.amount);
                DisplayLootInfoClientRpc(itemIndex, itemProps.amount, rpcParams);
                GeminiNetworkManager.instance.DestroyItem(itemObj);
            }
            else if (containableAmount != 0)
            {
                Add(itemProps.item, containableAmount);
                DisplayLootInfoClientRpc(itemIndex, containableAmount, rpcParams);
                GeminiNetworkManager.instance.SetItemPropsClientRpc(itemObj, itemIndex, (itemProps.amount - containableAmount));
            }
            else
            {
            }
        }
    }

    public void Refresh()
    {
        invenAllSlotUpdate?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveServerRpc(int slotNum)
    {
        if (!portalInven)
            RemoveClientRpc(slotNum);
        else
            GameManager.instance.WaveSkipItemRemoveServerRpc(slotNum);
    }

    [ClientRpc]
    public void RemoveClientRpc(int slotNum)
    {
        if (items.ContainsKey(slotNum))
        {
            totalItems[items[slotNum]] -= amounts[slotNum];
            items.Remove(slotNum);
            amounts.Remove(slotNum);

            onItemChangedCallback?.Invoke(slotNum);
        }
    }

    public void DragDrop()
    {
        if (GameManager.instance.isPlayerInMarket)
        {
            LootListManager.instance.DisplayInfoMessage("Can't drop items in market");
        }
        else
        {
            DragDropServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DragDropServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        bool isHost = clientId == 0;
        if (!ItemDragManager.instance.IsDragging(isHost))
            return;

        Item dragItem = ItemDragManager.instance.GetItem(isHost);
        int dragAmount = ItemDragManager.instance.GetAmount(isHost);
        Drop(dragItem, dragAmount, isHost);
        ItemDragManager.instance.Clear(isHost);

        invenAllSlotUpdate?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnPosDragDropServerRpc(bool isHostTask, bool isHostMap)
    {
        Vector3 pos = isHostMap ? GameManager.instance.hostPlayerSpawnPos : GameManager.instance.clientPlayerSpawnPos;

        Item dragItem = ItemDragManager.instance.GetItem(isHostTask);
        int dragAmount = ItemDragManager.instance.GetAmount(isHostTask);

        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(dragItem);
        GeminiNetworkManager.instance.ItemSpawnServerRpc(itemIndex, dragAmount, pos);

        ItemDragManager.instance.Clear(isHostTask);
    }

    public void Drop(Item item, int amount, bool isHost)
    {
        Vector3 spawnPos = GameManager.instance.GetPlayerPos(isHost);
        int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
        DisplayDropInfoClientRpc(itemIndex, amount, isHost);
        GeminiNetworkManager.instance.ItemSpawnServerRpc(itemIndex, amount, spawnPos);
    }

    [ClientRpc]
    public void DisplayDropInfoClientRpc(int itemIndex, int amount, bool isHost)
    {
        if (isHost != GameManager.instance.isHost) return;

        Item item = GeminiNetworkManager.instance.GetItemSOFromIndex(itemIndex);
        LootListManager.instance.DisplayDropInfo(item, amount);
    }

    public void ResetInven()
    {
        items.Clear();
        amounts.Clear();
        totalItems.Clear();

        foreach (Item item in itemList)
        {
            totalItems.Add(item, 0);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SortServerRpc()
    {
        SortClientRpc();
    }

    [ClientRpc]
    public void SortClientRpc()
    {
        items = new Dictionary<int, Item>();
        amounts = new Dictionary<int, int>();

        foreach (KeyValuePair<Item, int> item in totalItems)
        {
            if (item.Value > 0)
            {
                int tempAmount = item.Value;
                if (tempAmount > 0)
                {
                    for (int i = 0; i < space; i++)
                    {
                        if (!items.ContainsKey(i))
                        {
                            if (tempAmount <= maxAmount)
                            {
                                items[i] = item.Key;
                                amounts[i] = tempAmount;
                                tempAmount = 0;
                            }
                            else
                            {
                                items[i] = item.Key;
                                amounts[i] = maxAmount;
                                tempAmount -= maxAmount;
                            }
                        }
                        if (tempAmount <= 0)
                            break;
                    }
                }
            }
        }

        invenAllSlotUpdate?.Invoke();
    }

    public bool TotalItemsAmountLimitCheck(int amountLimit)
    {
        int amounts = 0;

        foreach (var itemDic in totalItems)
        {
            amounts += itemDic.Value;
            if (amounts >= amountLimit && amounts != 0)
            {
                return true;
            }
        }
        
        return false;
    }

    public bool HasItem()
    {
        return items.Count > 0;
    }

    public int GetItemAmount(Item item)
    {
        return totalItems[item];
    }

    public InventorySaveData SaveData()
    {
        InventorySaveData data = new InventorySaveData();
        Dictionary<int, int> totalItemIndexes = new Dictionary<int, int>();
        Dictionary<int, int> itemIndexes = new Dictionary<int, int>();

        foreach (KeyValuePair<Item, int> kv in totalItems)
        {
            totalItemIndexes.Add(GeminiNetworkManager.instance.GetItemSOIndex(kv.Key), kv.Value);
        }
        foreach (KeyValuePair<int, Item> kv in items)
        {
            itemIndexes.Add(kv.Key, GeminiNetworkManager.instance.GetItemSOIndex(kv.Value));
        }

        data.totalItemIndexes = totalItemIndexes;
        data.itemIndexes = itemIndexes;
        data.amounts = amounts;

        return data;
    }

    public void LoadData(InventorySaveData data)
    {
        Dictionary<Item, int> tempTotalItems = new Dictionary<Item, int>();
        Dictionary<int, Item> tempItems = new Dictionary<int, Item>();
        
        foreach (KeyValuePair<int, int> kv in data.totalItemIndexes)
        {
            tempTotalItems.Add(GeminiNetworkManager.instance.GetItemSOFromIndex(kv.Key), kv.Value);
        }
        foreach (KeyValuePair<int, int> kv in data.itemIndexes)
        {
            tempItems.Add(kv.Key, GeminiNetworkManager.instance.GetItemSOFromIndex(kv.Value));
        }
        totalItems = tempTotalItems;
        items = tempItems;
        amounts = data.amounts;

        Refresh();
    }

    public void PlayerToStorage(int pInvenSlotNum)
    {
        PlayerToStorageServerRpc(pInvenSlotNum);
    }

    [ServerRpc (RequireOwnership = false)]
    public void PlayerToStorageServerRpc(int pInvenSlotNum)
    {
        Structure str = GetComponent<Structure>();
        Inventory playerInven;
        int containable = 0;

        if (str.isInHostMap)
            playerInven = GameManager.instance.hostMapInven;
        else
            playerInven = GameManager.instance.clientMapInven;

        var slot = playerInven.SlotCheck(pInvenSlotNum);
        containable = SpaceCheck(slot.item);
        if (containable >= slot.amount)
            containable = slot.amount;

        Add(slot.item, containable);
        playerInven.SlotSubServerRpc(pInvenSlotNum, containable);
    }

    public void PlayerToStr(int pInvenSlotNum, int sInvenSlotNum)
    {
        PlayerToStrServerRpc(pInvenSlotNum, sInvenSlotNum);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerToStrServerRpc(int pInvenSlotNum, int sInvenSlotNum)
    {
        Structure str = GetComponent<Structure>();
        TankCtrl tank = GetComponent<TankCtrl>();
        Inventory playerInven;
        int containable;

        if (str)
        {
            if (str.isInHostMap)
                playerInven = GameManager.instance.hostMapInven;
            else
                playerInven = GameManager.instance.clientMapInven;
        }
        else
        {
            if (tank.isInHostMap)
                playerInven = GameManager.instance.hostMapInven;
            else
                playerInven = GameManager.instance.clientMapInven;
        }

        var playerSlot = playerInven.SlotCheck(pInvenSlotNum);

        int sInvenAmount;
        if (items.ContainsKey(sInvenSlotNum))
            sInvenAmount = amounts[sInvenSlotNum];
        else
            sInvenAmount = 0;

        if (sInvenAmount + playerSlot.amount > maxAmount)
        {
            containable = maxAmount - sInvenAmount;
        }
        else
        {
            containable = playerSlot.amount;
        }

        SlotAdd(sInvenSlotNum, playerSlot.item, containable);
        playerInven.SlotSubServerRpc(pInvenSlotNum, containable);
    }

    public void StrToPlayer(int slotNum, bool isHostMap)
    {
        StrToPlayerServerRpc(slotNum, isHostMap);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StrToPlayerServerRpc(int slotNum, bool isHostMap)
    {
        if (!items.ContainsKey(slotNum))
            return;

        Inventory playerInven;

        if (isHostMap)
            playerInven = GameManager.instance.hostMapInven;
        else
            playerInven = GameManager.instance.clientMapInven;

        int containable = playerInven.SpaceCheck(items[slotNum]);
        if (amounts[slotNum] <= containable)
        {
            playerInven.Add(items[slotNum], amounts[slotNum]);
            RemoveServerRpc(slotNum);
        }
        else if (containable != 0)
        {
            playerInven.Add(items[slotNum], containable);
            SlotSubServerRpc(slotNum, containable);
        }
        else
        {
        }
    }

    public void BuyMerch(Merch[] merchList, int totalPrice)
    {
        int[] merchNum = new int[merchList.Length];
        int[] amount = new int[merchList.Length];

        for (int i = 0; i < merchList.Length; i++)
        {
            merchNum[i] = merchList[i].merchNum;
            amount[i] = merchList[i].amount;
        }

        BuyMerchServerRpc(merchNum, amount, totalPrice);
    }

    [ServerRpc (RequireOwnership = false)]
    public void BuyMerchServerRpc(int[] merchNum, int[] amount, int totalPrice)
    {
        if (MultipleSpaceCheck(merchNum, amount))
        {
            if (GameManager.instance.finance.finance >= totalPrice)
            {
                for (int i = 0; i < merchNum.Length; i++)
                {
                    Overall.instance.OverallPurchased(merchandiseList[merchNum[i]].item, amount[i]);
                    GameManager.instance.SubFinanceServerRpc(merchandiseList[merchNum[i]].buyPrice * amount[i]);
                    Add(merchandiseList[merchNum[i]].item, amount[i]);
                }
            }
            else
            {
                Debug.Log("Not enough money");
            }
        }
        else
        {
            Debug.Log("Not enough space");
        }
    }

    public void SellMerch(Merch[] merchList)
    {
        int[] merchNum = new int[merchList.Length];
        int[] amount = new int[merchList.Length];

        for (int i = 0; i < merchList.Length; i++)
        {
            merchNum[i] = merchList[i].merchNum;
            amount[i] = merchList[i].amount;
        }

        SellMerchServerRpc(merchNum, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SellMerchServerRpc(int[] merchNum, int[] amount)
    {
        bool isEnough = true;
        for (int i = 0; i < merchNum.Length; i++)
        {
            int itemAmount = GetItemAmount(merchandiseList[merchNum[i]].item);
            if (itemAmount < amount[i])
            {
                isEnough = false;
                break;
            }
        }

        if (isEnough)
        {
            for (int i = 0; i < merchNum.Length; i++)
            {
                Overall.instance.OverallSold(merchandiseList[merchNum[i]].item, amount[i]);
                GameManager.instance.AddFinanceServerRpc(merchandiseList[merchNum[i]].sellPrice * amount[i]);
                Sub(merchandiseList[merchNum[i]].item, amount[i]);
            }
        }
    }

    public void PortalInvenSet()
    {
        portalInven = true;
    }
}
