using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;
using BlueprintSystem;

public class DataManager : MonoBehaviour
{
    public SaveData saveData;
    public string path;
    public int selectedSlot;    // 저장 슬롯. 나중에 ui 넣을 때 지정
    NetworkObjManager netObjMgr;
    [SerializeField]
    GameObject beltGroup;
    [SerializeField]
    GameObject beltMgr;
    Dictionary<Transporter, StructureSaveData> transporters = new Dictionary<Transporter, StructureSaveData>();
    List<LDConnector> lDConnectors = new List<LDConnector>();
    [SerializeField]
    GameObject spawner;
    AreaLevelData[] levelData;

    #region Singleton
    public static DataManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        path = Application.persistentDataPath + "/save";
    }
    #endregion

    private void Start()
    {
        saveData = new SaveData();
        selectedSlot = 0;
        netObjMgr = NetworkObjManager.instance;
    }

    public (string, byte[]) ClientConnToData() // 클라이언트 접속시 저장 기능(클라이언트 접속시 딜레이를 주면 고장남 그래서 별도로 저장)
    {
        PlayerSaveData lastClientSaveData = saveData.clientPlayerData;
        saveData = new SaveData();

        InGameData inGameData = GameManager.instance.SaveData();
        saveData.InGameData = inGameData;
        saveData.InGameData.hostPortalName = GameManager.instance.portal[0].portalName;
        saveData.InGameData.clientPortalName = GameManager.instance.portal[1].portalName;

        // 플레이어
        saveData.hostPlayerData = GameManager.instance.PlayerSaveData(true);
        PlayerSaveData tempData = GameManager.instance.PlayerSaveData(false);

        if (lastClientSaveData.clientFirstConnection && tempData.hp == -1)
        {
            saveData.clientPlayerData = lastClientSaveData;
        }
        else
        {
            saveData.clientPlayerData = tempData;
        }

        // 행성 인벤토리
        InventorySaveData hostMapInventoryData = GameManager.instance.hostMapInven.SaveData();
        saveData.hostMapInvenData = hostMapInventoryData;
        InventorySaveData clientMapInventoryData = GameManager.instance.clientMapInven.SaveData();
        saveData.clientMapInvenData = clientMapInventoryData;

        foreach (ScienceBtn scienceBtn in ScienceDb.instance.scienceBtns)
        {
            ScienceData scienceData = scienceBtn.SaveData();
            saveData.scienceData.Add(scienceData);
        }

        OverallSaveData overallSaveData = Overall.instance.SaveData();
        saveData.overallData = overallSaveData;

        MapsSaveData mapsSaveData = MapGenerator.instance.SaveData();

        string json = JsonConvert.SerializeObject(saveData);
        string mapJson = JsonConvert.SerializeObject(mapsSaveData);
        var compData = Compression.Compress(mapJson);

        return (json, compData);
    }

    public void SyncSessionBlueprintsToClient(ulong targetClientId)
    {
        if (BlueprintStorageManager.instance == null) return;

        var blueprints = BlueprintStorageManager.instance.sessionBlueprints;
        if (blueprints == null || blueprints.Count == 0) return;

        foreach (var bp in blueprints)
            BlueprintStorageManager.instance.SyncToClient(bp, targetClientId);

        Debug.Log($"[Blueprint] 클라이언트 {targetClientId}에 세션 블루프린트 {blueprints.Count}개 동기화");
    }

    public void Save(int saveSlotNum, string fileName) // 오토 세이브 및 일반 저장용
    {
        GameManager.instance.CancelDragServerRpc(true);
        GameManager.instance.CancelDragServerRpc(false);

        GameManager.instance.GameSaveStopServerRpc(true);
        StartCoroutine(SaveCoroutine(saveSlotNum, fileName));
    }

    private IEnumerator SaveCoroutine(int saveSlotNum, string fileName)
    {
        GameManager.instance.saveImg.enabled = true;

        yield return null;

        PlayerSaveData lastClientSaveData = saveData.clientPlayerData;
        saveData = new SaveData();

        InGameData inGameData = GameManager.instance.SaveData();
        saveData.InGameData = inGameData;
        saveData.InGameData.fileName = fileName;
        saveData.InGameData.hostPortalName = GameManager.instance.portal[0].portalName;
        saveData.InGameData.clientPortalName = GameManager.instance.portal[1].portalName;

        // 플레이어
        saveData.hostPlayerData = GameManager.instance.PlayerSaveData(true);
        PlayerSaveData tempData = GameManager.instance.PlayerSaveData(false);

        if (lastClientSaveData.clientFirstConnection && tempData.hp == -1)
        {
            saveData.clientPlayerData = lastClientSaveData;
        }
        else
        {
            saveData.clientPlayerData = tempData;
        }

        // 행성 인벤토리
        InventorySaveData hostMapInventoryData = GameManager.instance.hostMapInven.SaveData();
        saveData.hostMapInvenData = hostMapInventoryData;
        InventorySaveData clientMapInventoryData = GameManager.instance.clientMapInven.SaveData();
        saveData.clientMapInvenData = clientMapInventoryData;

        foreach (ScienceBtn scienceBtn in ScienceDb.instance.scienceBtns)
        {
            ScienceData scienceData = scienceBtn.SaveData();
            saveData.scienceData.Add(scienceData);
        }

        foreach (Structure structure in netObjMgr.netStructures)
        {
            StructureSaveData structureSaveData = structure.SaveData();
            saveData.structureData.Add(structureSaveData);
        }

        foreach (BeltGroupMgr beltGroup in netObjMgr.netBeltGroupMgrs)
        {
            if (beltGroup.beltList.Count > 0)
            {
                BeltGroupSaveData beltGroupSaveData = beltGroup.SaveData();
                saveData.beltGroupData.Add(beltGroupSaveData);
            }
        }

        foreach (UnitCommonAi unitAi in netObjMgr.netUnitCommonAis)
        {
            UnitSaveData unitSaveData = unitAi.SaveData();
            saveData.unitData.Add(unitSaveData);
        }

        MonsterSpawnerManager monsterSpawner = MonsterSpawnerManager.instance;
        SpawnerManagerSaveData spawnerManagerSaveData = monsterSpawner.SaveData();
        saveData.spawnerManagerSaveData = spawnerManagerSaveData;

        OverallSaveData overallSaveData = Overall.instance.SaveData();
        saveData.overallData = overallSaveData;

        MapsSaveData mapsSaveData = MapGenerator.instance.SaveData();

        List<NetItemPropsData> netItemPropsDatas = NetworkItemPoolSync.instance.NetItemSaveData();
        saveData.netItemData = netItemPropsDatas;

        List<HomelessDroneSaveData> homelessDroneSaveData = HomelessDroneManager.instance.SaveDroneData();
        saveData.homelessDroneData = homelessDroneSaveData;

        // Json 저장
        Debug.Log("saved: " + path);
        string json = JsonConvert.SerializeObject(saveData);
        File.WriteAllText(path + saveSlotNum.ToString() + ".json", json);

        string mapJson = JsonConvert.SerializeObject(mapsSaveData);
        var compData = Compression.Compress(mapJson);
        File.WriteAllBytes(path + saveSlotNum.ToString() + ".maps", compData);

        string sessionBlueprintJson = JsonConvert.SerializeObject(BlueprintStorageManager.instance.sessionBlueprints);
        var compBlueprintData = Compression.Compress(sessionBlueprintJson);
        File.WriteAllBytes(path + saveSlotNum.ToString() + ".blueprints", compBlueprintData);

        selectedSlot = saveSlotNum;
        SaveLoadMenu.instance.SaveUI(saveSlotNum, saveData.InGameData);

        GameManager.instance.saveImg.enabled = false;
        GameManager.instance.GameSaveStopServerRpc(false);
    }

    public void Load()
    {
        // 호스트가 파일로부터 json을 불러와서 동기화
        saveData = LoadManager.instance.GetSaveData();

        LoadData(saveData);

        transporters.Clear();
        lDConnectors.Clear();

        foreach (StructureSaveData structureSave in saveData.structureData)
        {
            SpawnStructure(structureSave);
        }

        SearchObjectsInRangeManager.instance.StrSearchFunc();

        foreach (BeltGroupSaveData beltGroupSave in saveData.beltGroupData)
        {
            SpawnBeltGroup(beltGroupSave);
        }

        foreach (UnitSaveData unitSave in saveData.unitData)
        {
            SpawnUnit(unitSave);
        }

        NetworkItemPoolSync.instance.NetItemLoadData(saveData.netItemData);

        HomelessDroneManager.instance.LoadDroneData(saveData.homelessDroneData);

        SetSpawnerManager(saveData.spawnerManagerSaveData);

        SetConnectedFunc();

        LoadSessionBlueprints();
    }

    public void LoadClient()
    {
        // 클라이언트가 접속 시 호스트로부터 json을 받아서 동기화
        // 네트워크 오브젝트라서 스폰을 시킬 필요가 없는 경우 등등 호스트가 파일을 불러와서 동기화 하는 과정과는 좀 달라질 예정
        SaveData saveData = LoadManager.instance.GetSaveData();
        LoadData(saveData);
    }

    public void LoadData(SaveData saveData)
    {
        GameManager.instance.LoadData(saveData.InGameData);
        GameManager.instance.LoadPlayerData(saveData.hostPlayerData, saveData.clientPlayerData);

        // 행성 인벤토리
        GameManager.instance.hostMapInven.LoadData(saveData.hostMapInvenData);
        GameManager.instance.clientMapInven.LoadData(saveData.clientMapInvenData);

        ScienceDb.instance.LoadSet(saveData.scienceData);
        Overall.instance.LoadData(saveData.overallData);
    }

    private void LoadSessionBlueprints()
    {
        var blueprints = LoadManager.instance.GetSessionBlueprints();
        if (blueprints == null || blueprints.Count == 0) return;

        var storage = BlueprintStorageManager.instance;
        foreach (var bp in blueprints)
            storage.OnReceiveSessionBlueprint(bp);
    }

    void SpawnStructure(StructureSaveData saveData)
    {
        Building building = GeminiNetworkManager.instance.GetBuildingSOFromIndex(saveData.index);
        Vector3 spawnPos = Vector3Extensions.ToVector3(saveData.pos);
        GameObject spawnobj;

        if (!saveData.sideObj)
        {
            spawnobj = Instantiate(building.gameObj, spawnPos, Quaternion.identity);
        }
        else
        {
            spawnobj = Instantiate(building.sideObj, spawnPos, Quaternion.identity);
        }

        spawnobj.TryGetComponent(out NetworkObject netObj);
        if (!netObj.IsSpawned) netObj.Spawn(true);

        if (netObj.TryGetComponent(out Structure structure))
        {
            structure.GameStartSpawnSet(saveData.level, saveData.direction, building.height, building.width, saveData.planet, saveData.index);
            structure.StructureStateSet(saveData.isPreBuilding, saveData.destroyStart, saveData.hp, saveData.repairGauge, saveData.destroyTimer);
            structure.GameStartRecipeSet(saveData.recipeId);
            structure.MapDataSaveClientRpc(Vector3Extensions.ToVector3(saveData.pos));

            if (saveData.portalName != "")
                structure.portalName = saveData.portalName;

            if (structure.TryGet(out Production prod))
            {
                prod.GameStartItemSet(saveData.inven);
                prod.loadedProdTimer = saveData.prodTimer;
                prod.fuel = saveData.fuel;

                if (prod.Get<PortalObj>())
                {
                    if (saveData.planet)
                    {
                        Portal portal = GameManager.instance.portal[0];
                        spawnobj.transform.parent = portal.transform;
                        portal.SetPortalObjEnd(structure.structureData.FactoryName, spawnobj);
                    }
                    else
                    {
                        Portal portal = GameManager.instance.portal[1];
                        spawnobj.transform.parent = portal.transform;
                        portal.SetPortalObjEnd(structure.structureData.FactoryName, spawnobj);
                    }
                }

                if (structure.TryGet(out AttackTower tower))
                {
                    tower.energyBulletAmount = saveData.energyBulletAmount;
                }

                if (structure.TryGet(out Disintegrator disintegrator))
                {
                    disintegrator.SetAuto(saveData.isAuto);
                }

                if (structure.TryGet(out AutoSeller autoSeller))
                {
                    if (saveData.trUnitPosData.Count > 0)
                    {
                        for (int i = 0; i < saveData.trUnitPosData.Count; i++)
                        {
                            Vector3 unitSpawnPos = Vector3Extensions.ToVector3(saveData.trUnitPosData[i]);

                            Dictionary<int, int> itemDic = new Dictionary<int, int>();

                            if (saveData.trUnitItemData.ContainsKey(i))
                            {
                                itemDic = saveData.trUnitItemData[i];
                            }

                            autoSeller.UnitLoad(unitSpawnPos, itemDic);
                        }
                    }
                }

                if (structure.TryGet(out AutoBuyer autoBuyer))
                {
                    autoBuyer.maxBuyAmount = saveData.maxBuyAmount;
                    autoBuyer.buyInterval = saveData.sendingOption;
                    autoBuyer.cooldown = autoBuyer.buyInterval;

                    if (saveData.trUnitPosData.Count > 0)
                    {
                        for (int i = 0; i < saveData.trUnitPosData.Count; i++)
                        {
                            Vector3 unitSpawnPos = Vector3Extensions.ToVector3(saveData.trUnitPosData[i]);

                            Dictionary<int, int> itemDic = new Dictionary<int, int>();

                            if (saveData.trUnitItemData.ContainsKey(i))
                            {
                                itemDic = saveData.trUnitItemData[i];
                            }

                            autoBuyer.UnitLoad(unitSpawnPos, itemDic);
                        }
                    }
                }

                if (structure.TryGet(out Transporter transporter))
                {
                    transporter.SendFuncSetServerRpc(saveData.isAuto, saveData.sendingOption);

                    transporters.Add(transporter, saveData);
                    if (saveData.connectedStrPos.Count > 0)
                        structure.ConnectedPosListPosSet(Vector3Extensions.ToVector3(saveData.connectedStrPos[0]));
                }
                else if (structure.TryGet(out UnitFactory unitFactory))
                {
                    if (saveData.connectedStrPos.Count > 0)
                        unitFactory.UnitSpawnPosSetServerRpc(Vector3Extensions.ToVector3(saveData.connectedStrPos[0]));
                }

                if(prod.TryGet(out FluidFactoryCtrl fluidFactoryCtrl))
                {
                    fluidFactoryCtrl.FluidGameStartSet(saveData.fluidType, saveData.storedFluid);
                }
            }
            else
            {
                if (structure.TryGet(out SplitterCtrl splitterCtrl))
                {
                    for (int a = 0; a < saveData.filters.Count; a++)
                    {
                        FilterSaveData filterSaveData = saveData.filters[a];
                        splitterCtrl.GameStartFillterSet(a, filterSaveData.filterOn, filterSaveData.filterInvert, filterSaveData.filterItemIndex);
                    }
                }
                else if (structure.TryGet(out Unloader unloader))
                {
                    FilterSaveData filterSaveData = saveData.filters[0];
                    if (filterSaveData.filterItemIndex != -1)
                        unloader.GameStartFillterSet(filterSaveData.filterItemIndex);
                }
                else if (structure.TryGet(out LDConnector lDConnector))
                {
                    lDConnectors.Add(lDConnector);
                    if (saveData.connectedStrPos.Count > 0)
                    {
                        for (int i = 0; i < saveData.connectedStrPos.Count; i++)
                        {
                            structure.ConnectedPosListPosSet(Vector3Extensions.ToVector3(saveData.connectedStrPos[i]));
                        }
                    }
                }
                else if (structure.TryGet(out SendUnderBeltCtrl sendUnderBelt))
                {
                    sendUnderBelt.LoadSendingItems(saveData.sendUnderBeltItems);
                }

                foreach (int itemIndex in saveData.itemIndex)
                {
                    structure.GameStartItemSet(itemIndex);
                }
            }
        }
    }

    void SpawnBeltGroup(BeltGroupSaveData saveData)
    {
        GameObject beltGroupObj = Instantiate(beltGroup);
        beltGroupObj.TryGetComponent(out NetworkObject netObj);
        if (!netObj.IsSpawned) netObj.Spawn(true);
        beltGroupObj.transform.parent = beltMgr.transform;
        beltGroupObj.TryGetComponent(out BeltGroupMgr beltGroupMgr);
        beltGroupMgr.loadConnStr = saveData.connStr;
        foreach (var beltData in saveData.beltList)
        {
            Building building = GeminiNetworkManager.instance.GetBuildingSOFromIndex(beltData.Item2.index);
            Vector3 spawnPos = Vector3Extensions.ToVector3(beltData.Item2.pos);
            GameObject beltObj = Instantiate(building.gameObj, spawnPos, Quaternion.identity);
            beltObj.TryGetComponent(out NetworkObject netBeltObj);
            if (!netBeltObj.IsSpawned) netBeltObj.Spawn(true);

            netBeltObj.TryGetComponent(out Structure structure);

            structure.GameStartSpawnSet(beltData.Item2.level, beltData.Item2.direction, building.height, building.width, beltData.Item2.planet, beltData.Item2.index);
            structure.StructureStateSet(beltData.Item2.isPreBuilding, beltData.Item2.destroyStart, beltData.Item2.hp, beltData.Item2.repairGauge, beltData.Item2.destroyTimer);
            structure.MapDataSaveClientRpc(Vector3Extensions.ToVector3(beltData.Item2.pos));
            
            if (structure.TryGet(out BeltCtrl belt))
            {
                belt.GameStartBeltSet(beltData.Item1.modelMotion, beltData.Item1.isTrun, beltData.Item1.isRightTurn, beltData.Item1.beltState);

                for (int i = 0; i < beltData.Item1.itemIndex.Count; i++)
                {
                    Vector3 itemPos = Vector3Extensions.ToVector3(beltData.Item1.itemPos[i]);
                    belt.GameStartItemSet(itemPos, beltData.Item1.itemIndex[i]);
                }
            }

            beltObj.transform.parent = beltGroupMgr.transform;
            beltGroupMgr.beltList.Add(belt);
        }

        beltGroupMgr.SetBeltData();
        beltGroupMgr.ItemIndexSet();

        foreach (BeltCtrl belt in beltGroupMgr.beltList)
        {
            belt.GameStartItemDataSet();
            belt.isGameStartItemReady = true;
        }
    }

    void SetConnectedFunc()
    {
        foreach (var transporterData in transporters)
        {
            Transporter transporter = transporterData.Key;
            StructureSaveData strData = transporterData.Value;
            Transporter takeTransporter = null;
            Structure findObj = null;
            if (transporter.connectedPosList.Count > 0)
                findObj = CellObjFind(transporter.connectedPosList[0], transporter.isInHostMap);

            if (findObj != null && findObj.TryGet(out takeTransporter))
            {
                transporter.TakeBuildSet(takeTransporter);
                if (transporter.TryGet(out MapClickEvent mapClick) && takeTransporter.TryGet(out MapClickEvent othMapClick))
                {
                    mapClick.GameStartSetRenderer(othMapClick);
                }
            }

            if (strData.trUnitPosData.Count > 0)
            {
                for (int i = 0; i < strData.trUnitPosData.Count; i++)
                {
                    Vector3 spawnPos = Vector3Extensions.ToVector3(strData.trUnitPosData[i]);

                    Dictionary<int, int> itemDic = new Dictionary<int, int>();

                    if (strData.trUnitItemData.ContainsKey(i))
                    {
                        itemDic = strData.trUnitItemData[i]; 
                    }

                    if (takeTransporter != null)
                    {
                        transporter.UnitLoad(spawnPos, takeTransporter, itemDic);
                    }
                    else
                    {
                        transporter.UnitLoad(spawnPos, itemDic);
                    }
                }
            }
        }

        foreach (LDConnector lDConnector in lDConnectors)
        {
            lDConnector.connector.Init();
            lDConnector.isBuildDone = true;

            for (int i = 0; i < lDConnector.connectedPosList.Count; i++)
            {
                Structure findObj = CellObjFind(lDConnector.connectedPosList[i], lDConnector.isInHostMap);
                if (findObj != null && findObj.TryGet(out LDConnector othLDConnector))
                {
                    if (lDConnector.TryGet(out MapClickEvent mapClick) && othLDConnector.TryGet(out MapClickEvent othMapClick))                    
                    {
                        mapClick.GameStartSetRenderer(othMapClick);
                    }
                }
            }
        }
    }

    void SpawnUnit(UnitSaveData unitSaveData)
    {
        GameObject spawnobj = Instantiate(GeminiNetworkManager.instance.unitListSO.userUnitList[unitSaveData.unitIndex]);
        spawnobj.TryGetComponent(out NetworkObject netObj);
        if (!netObj.IsSpawned) netObj.Spawn(true);

        spawnobj.transform.position = Vector3Extensions.ToVector3(unitSaveData.pos);
        spawnobj.GetComponent<UnitAi>().GameStartSet(unitSaveData);
    }

    void SetSpawnerManager(SpawnerManagerSaveData spawnerManagerSaveData)
    {
        levelData = SpawnerSetManager.instance.arealevelData;
        if(spawnerManagerSaveData.splitCount != 0)
            MonsterSpawnerManager.instance.SplitCountSet(spawnerManagerSaveData.splitCount);

        if(spawnerManagerSaveData.waveSpawnerAliveState)
            MonsterSpawnerManager.instance.WaveStateLoad(spawnerManagerSaveData);

        for (int i = 0; i < spawnerManagerSaveData.splitCount; i++)
        {
            for (int j = 0; j < spawnerManagerSaveData.splitCount; j++)
            {
                SpawnerGroupData spawner1GroupData = spawnerManagerSaveData.spawnerMap1Matrix[i, j];
                if(spawner1GroupData != null)
                {
                    GameObject group = SetSpawnerGroupMgr(spawner1GroupData, true);
                    MonsterSpawnerManager.instance.MatrixSet(group, (i, j), true);
                }

                SpawnerGroupData spawner2GroupData = spawnerManagerSaveData.spawnerMap2Matrix[i, j];
                if (spawner2GroupData != null)
                {
                    GameObject group = SetSpawnerGroupMgr(spawner2GroupData, false);
                    MonsterSpawnerManager.instance.MatrixSet(group, (i, j), false);
                }
            }
        }
    }

    GameObject SetSpawnerGroupMgr(SpawnerGroupData spawnerGroupData, bool planet)
    {
        SpawnerSetManager spawnerSetManager = SpawnerSetManager.instance;
        GameObject spawnerGroupObj = spawnerSetManager.SpawnerGroupSet(Vector3Extensions.ToVector3(spawnerGroupData.pos));
        spawnerGroupObj.TryGetComponent(out SpawnerGroupManager spawnerGroup);
        spawnerGroup.SpawnerGroupStatsSet(spawnerGroupData.spawnerMatrixIndex);
        foreach (SpawnerSaveData spawnerSaveData in spawnerGroupData.spawnerSaveDataList)
        {
            if (spawnerSaveData.dieCheck)
                continue;

            GameObject spawner = SpawnSpawner(spawnerSaveData);
            spawnerGroup.SpawnerSet(spawner);
            spawner.TryGetComponent(out MonsterSpawner monsterSpawner);
            monsterSpawner.dieCheck = spawnerSaveData.dieCheck;
            MonsterSpawnerManager.instance.AreaGroupSet(monsterSpawner, spawnerSaveData.spawnerGroupIndex, planet);
            monsterSpawner.groupManager = spawnerGroup;
            monsterSpawner.GameStartSet(spawnerSaveData, levelData[spawnerSaveData.level - 1], Vector3Extensions.ToVector3(spawnerSaveData.wavePos), planet, spawnerSaveData.spawnerGroupIndex);
            SetSpawner(monsterSpawner, spawnerSaveData, planet);
            if (monsterSpawner.dieCheck)
            {
                monsterSpawner.DieFuncLoad();
            }
        }

        return spawnerGroupObj;
    }

    void SetSpawner(MonsterSpawner monsterSpawner, SpawnerSaveData spawnerSaveData, bool planet)
    {
        foreach (UnitSaveData unitSaveData in spawnerSaveData.monsterList)
        {
            MonsterAi monster = null;
            if (!unitSaveData.waveState)
            {
                monster = monsterSpawner.SpawnMonster(unitSaveData.monsterType, unitSaveData.unitIndex, planet);
                monster.transform.position = Vector3Extensions.ToVector3(unitSaveData.pos);
                monster.GameStartSet(unitSaveData);
            }
            else
            {
                monster = monsterSpawner.WaveMonsterSpawn(unitSaveData.monsterType, unitSaveData.unitIndex, planet, unitSaveData.isWaveColonyCallCheck);
                monster.transform.position = Vector3Extensions.ToVector3(unitSaveData.pos);
                monster.GameStartSet(unitSaveData);
                monster.LoadGameWaveSet(Vector3Extensions.ToVector3(unitSaveData.wavePos));
            }
        }
    }

    GameObject SpawnSpawner(SpawnerSaveData spawnerSaveData)
    {
        GameObject spawnerObj = Instantiate(spawner);
        NetworkObject networkObject = spawnerObj.GetComponent<NetworkObject>();
        if (!networkObject.IsSpawned) networkObject.Spawn(true);
        spawnerObj.transform.position = Vector3Extensions.ToVector3(spawnerSaveData.spawnerPos);
        MonsterSpawner monsterSpawner = spawnerObj.GetComponent<MonsterSpawner>();
        if (!spawnerSaveData.dieCheck)
            MapGenerator.instance.SetCorruption(monsterSpawner, spawnerSaveData.level);

        return spawnerObj;
    }

    public Structure CellObjFind(Vector3 findPos, bool isInHostMap)
    {
        int x = Mathf.FloorToInt(findPos.x);
        int y = Mathf.FloorToInt(findPos.y);
        Map map;
        if (isInHostMap)
            map = GameManager.instance.hostMap;
        else
            map = GameManager.instance.clientMap;

        Cell cell = map.GetCellDataFromPos(x, y);

        return cell.structure;
    }
}
