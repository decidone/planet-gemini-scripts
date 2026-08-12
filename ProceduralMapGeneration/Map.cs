using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{
    public int width;
    public int height;
    public List<List<Cell>> mapData;
    public int offsetY;
    public (int x, int y) spawnTile;   // mapData의 x, y. 실제 pos와는 다름

    public bool IsOnMap(int x, int y)
    {
        // pos
        bool isOnMap = (0 <= x && x < width) && (0 <= (y - offsetY) && (y - offsetY) < height);
        return isOnMap;
    }

    public bool IsOnMapData(int x, int y)
    {
        // data
        bool isOnMap = (0 <= x && x < width) && (0 <= y && y < height);
        return isOnMap;
    }

    public void SetOffsetY(int offset)
    {
        offsetY = offset;
    }

    public Cell GetCellDataFromPos(int x, int y)
    {
        // x, y값이 좌표값이여야 함. 데이터 배열의 행렬 인덱스 번호로 실수하지 않게 조심
        if (IsOnMap(x, y))
            return mapData[x][y - offsetY];
        else
            return null;
    }

    public void SetSpawnTile(int x, int y)
    {
        spawnTile = (x, y);
    }
}
