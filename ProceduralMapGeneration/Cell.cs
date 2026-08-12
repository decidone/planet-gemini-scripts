using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Cell
{
    public int x;
    public int y;
    public Tile tile;
    public Tile exTile;     //절벽 바이옴으로 바뀔 때 기존에 있던 일반 바이옴 타일을 저장
    public string tileType;
    public Biome biome;
    public Resource resource;
    public int resourceNum;
    public int oilTile;
    public int resourceChunkNum = -1;
    public GameObject obj;
    public int objNum = -1;
    public Structure structure;
    public List<string> buildable = new List<string>();

    public int corruptionId = 0;
    public GameObject corruptionObj;

    //0: 기본, 1: 외곽, 2: 위쪽 경계, 3: 왼쪽 위 안쪽 코너, 4: 오른쪽 위 안쪽 코너, 5: 왼쪽 아래 안쪽 코너, 6: 오른쪽 아래 안쪽 코너
    public int cliffType = -1;
    public bool spawnArea = false;
    
    public bool BuildCheck(string str)
    {
        bool build = false;
        if (buildable.Contains(str))
        {
            build = true;
        }
        return build;
    }
}
