using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using BlueprintSystem;

public class LoadManager : MonoBehaviour
{
    SaveData loadedData;
    MapsSaveData loadedMapData;
    private List<BlueprintData> _sessionBlueprints;

    #region Singleton
    public static LoadManager instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    public void SetSaveData(SaveData saveData)
    {
        loadedData = saveData;
    }

    public void SetMapSaveData(MapsSaveData mapData)
    {
        loadedMapData = mapData;
    }

    public void SetMapSaveData(byte[] bytes)
    {
        string decompData = Compression.Decompress(bytes);
        MapsSaveData mapData = new MapsSaveData();
        mapData = JsonConvert.DeserializeObject<MapsSaveData>(decompData);

        loadedMapData = mapData;
    }

    public SaveData GetSaveData()
    {
        return loadedData;
    }

    public MapsSaveData GetMapSaveData()
    {
        return loadedMapData;
    }

    public void ClearSaveData()
    {
        SaveData saveData = new SaveData();
        loadedData = saveData;
    }

    public void ClearMapSaveData()
    {
        MapsSaveData mapData = new MapsSaveData();
        loadedMapData = mapData;
    }

    public void SetSessionBlueprints(byte[] bytes)
    {
        try
        {
            string json = Compression.Decompress(bytes);
            _sessionBlueprints = JsonConvert.DeserializeObject<List<BlueprintData>>(json) ?? new();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Blueprint] 세션 블루프린트 로드 실패: {e.Message}");
            _sessionBlueprints = new();
        }
    }

    public List<BlueprintData> GetSessionBlueprints()
        => _sessionBlueprints ?? new();

    public void ClearSessionBlueprints()
        => _sessionBlueprints = new();
}
