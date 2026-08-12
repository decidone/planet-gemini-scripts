using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int saveVersion = 1;
    public InGameData InGameData = new InGameData();

    // 플레이어
    public PlayerSaveData hostPlayerData = new PlayerSaveData();
    public PlayerSaveData clientPlayerData = new PlayerSaveData();

    // 행성 인벤토리
    public InventorySaveData hostMapInvenData = new InventorySaveData();
    public InventorySaveData clientMapInvenData = new InventorySaveData();

    public List<ScienceData> scienceData = new List<ScienceData>();

    public List<StructureSaveData> structureData = new List<StructureSaveData>();
    public List<BeltGroupSaveData> beltGroupData = new List<BeltGroupSaveData>();
    public List<UnitSaveData> unitData = new List<UnitSaveData>();
    public SpawnerManagerSaveData spawnerManagerSaveData = new SpawnerManagerSaveData();

    public OverallSaveData overallData = new OverallSaveData();
    public List<NetItemPropsData> netItemData = new List<NetItemPropsData>();
    public List<HomelessDroneSaveData> homelessDroneData = new List<HomelessDroneSaveData>();
}
