using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Biome : MonoBehaviour
{
    private const int RareTilePlacementThreshold = 90;  // random.Next(0,100)이 이 값 초과 시 희귀 타일 배치 (약 10% 확률)

    public int biomeNum;    // 0: plain, 1: forest, 2: desert, 3: snow, 4: frozen, 5: lake, 6: cliff
    public string biome;
    public string diffBiome;

    [Space]
    [Header("Tiles")]
    public List<Tile> tiles;
    public List<Tile> pointTiles;
    public List<Tile> exceptionalTiles;
    public List<Tile> top;
    public List<Tile> left;
    public List<Tile> right;
    public List<Tile> bottom;
    public List<Tile> corner;
    public List<Tile> innerCorner;

    [Space]
    [Header("Cliff Tiles")]
    public List<Tile> cliffWall;
    public List<Tile> cliffDarkWall;
    public List<Tile> cliffLeftCornerWall;
    public List<Tile> cliffRightCornerWall;
    public List<Tile> cliffBottomLeftCornerWall;
    public List<Tile> cliffBottomRightCornerWall;
    
    [Space]
    [Header("Objects")]
    public List<GameObject> objects;
    public float objectsSpawnrate;

    public List<Tile> GetTilesList()
    {
        List<Tile> tilesList = new();

        tilesList.AddRange(tiles);
        tilesList.AddRange(pointTiles);
        tilesList.AddRange(exceptionalTiles);
        tilesList.AddRange(top);
        tilesList.AddRange(left);
        tilesList.AddRange(right);
        tilesList.AddRange(bottom);
        tilesList.AddRange(corner);
        tilesList.AddRange(innerCorner);
        tilesList.AddRange(cliffWall);
        tilesList.AddRange(cliffDarkWall);
        tilesList.AddRange(cliffLeftCornerWall);
        tilesList.AddRange(cliffRightCornerWall);
        tilesList.AddRange(cliffBottomLeftCornerWall);
        tilesList.AddRange(cliffBottomRightCornerWall);

        return tilesList;
    }

    public int BiomeSmoother(Map map, int x, int y, bool isSpecificDiffBiome)
    {
        List<Cell> neighbors = new List<Cell>();
        List<int> nearDiff = new List<int>();
        List<int> diagonalDiff = new List<int>();

        for (int i = 0; i < 8; i++)
            neighbors.Add(new Cell());

        for (int i = 0; i < 9; i++)
        {
            int nx = x + (i % 3) - 1;
            int ny = y + -((i / 3) - 1);

            if (i != 4)
            {
                int j = (i < 4) ? i : i - 1;
                if (map.IsOnMapData(nx, ny))
                {
                    neighbors[j] = map.mapData[nx][ny];
                    if (isSpecificDiffBiome)
                    {
                        if (neighbors[j].biome.biome == diffBiome)
                        {
                            if (i % 2 == 0)
                            {
                                diagonalDiff.Add(j);
                            }
                            else
                            {
                                nearDiff.Add(j);
                            }
                        }
                    }
                    else
                    {
                        if (neighbors[j].biome != map.mapData[x][y].biome)
                        {
                            if (i % 2 == 0)
                            {
                                diagonalDiff.Add(j);
                            }
                            else
                            {
                                nearDiff.Add(j);
                            }
                        }
                    }
                }
            }
        }

        if (nearDiff.Count > 0 || diagonalDiff.Count > 0)
        {
            if (nearDiff.Count == 1)
            {
                if (nearDiff.Contains(1) && !diagonalDiff.Contains(5) && !diagonalDiff.Contains(7) && top.Count > 0)
                {
                    return 0;
                }
                else if (nearDiff.Contains(3) && !diagonalDiff.Contains(2) && !diagonalDiff.Contains(7) && left.Count > 0)
                {
                    return 0;
                }
                else if (nearDiff.Contains(4) && !diagonalDiff.Contains(0) && !diagonalDiff.Contains(5) && right.Count > 0)
                {
                    return 0;
                }
                else if (nearDiff.Contains(6) && !diagonalDiff.Contains(0) && !diagonalDiff.Contains(2) && bottom.Count > 0)
                {
                    return 0;
                }
            }
            else if (nearDiff.Count == 2 && corner.Count > 0)
            {
                if (nearDiff.Contains(1) && nearDiff.Contains(3) && !diagonalDiff.Contains(7))
                {
                    return 0;
                }
                else if (nearDiff.Contains(1) && nearDiff.Contains(4) && !diagonalDiff.Contains(5))
                {
                    return 0;
                }
                else if (nearDiff.Contains(6) && nearDiff.Contains(3) && !diagonalDiff.Contains(2))
                {
                    return 0;
                }
                else if (nearDiff.Contains(6) && nearDiff.Contains(4) && !diagonalDiff.Contains(0))
                {
                    return 0;
                }
            }
            else if (nearDiff.Count == 0 && diagonalDiff.Count == 1 && innerCorner.Count > 0)
            {
                if (diagonalDiff.Contains(0))
                {
                    return 0;
                }
                else if (diagonalDiff.Contains(2))
                {
                    return 0;
                }
                else if (diagonalDiff.Contains(5))
                {
                    return 0;
                }
                else if (diagonalDiff.Contains(7))
                {
                    return 0;
                }
            }

            Biome diffBiome;
            if (nearDiff.Count > 0)
                diffBiome = neighbors[nearDiff[0]].biome;
            else
                diffBiome = neighbors[diagonalDiff[0]].biome;

            if (biome == "cliff" && diffBiome.biome == "lake")
            {
                map.mapData[x][y].biome = MapGenerator.instance.forest;
            }
            else
            {
                map.mapData[x][y].biome = diffBiome;
            }

            return 1;
        }

        return 0;
    }

    public (Tile tile, string form) SetTile(System.Random random, Map map, int x, int y)
    {
        Tile tile = null;
        string form = "corner";
        bool isBorder = false;

        if (exceptionalTiles.Count > 0)
        {
            List<Cell> neighbors = new List<Cell>();
            List<int> nearDiff = new List<int>();
            List<int> diagonalDiff = new List<int>();

            for (int i = 0; i < 8; i++)
                neighbors.Add(new Cell());

            for (int i = 0; i < 9; i++)
            {
                int nx = x + (i % 3) - 1;
                int ny = y + -((i / 3) - 1);

                if (i != 4)
                {
                    int j = (i < 4) ? i : i - 1;
                    if (map.IsOnMapData(nx, ny))
                    {
                        neighbors[j] = map.mapData[nx][ny];
                        if (neighbors[j].biome.biome == diffBiome)
                        {
                            if (i % 2 == 0)
                            {
                                diagonalDiff.Add(j);
                            }
                            else
                            {
                                nearDiff.Add(j);
                            }
                        }
                    }
                }
            }

            if (nearDiff.Count > 0 || diagonalDiff.Count > 0)
            {
                isBorder = true;
                if (nearDiff.Count == 1)
                {
                    form = "side";
                    if (nearDiff.Contains(1) && !diagonalDiff.Contains(5) && !diagonalDiff.Contains(7) && top.Count > 0)
                    {
                        tile = top[random.Next(0, top.Count)];
                    }
                    else if (nearDiff.Contains(3) && !diagonalDiff.Contains(2) && !diagonalDiff.Contains(7) && left.Count > 0)
                    {
                        tile = left[random.Next(0, left.Count)];
                    }
                    else if (nearDiff.Contains(4) && !diagonalDiff.Contains(0) && !diagonalDiff.Contains(5) && right.Count > 0)
                    {
                        tile = right[random.Next(0, right.Count)];
                    }
                    else if (nearDiff.Contains(6) && !diagonalDiff.Contains(0) && !diagonalDiff.Contains(2) && bottom.Count > 0)
                    {
                        tile = bottom[random.Next(0, bottom.Count)];
                    }
                }
                else if (nearDiff.Count == 2 && corner.Count > 0)
                {
                    form = "corner";
                    if (nearDiff.Contains(1) && nearDiff.Contains(3) && !diagonalDiff.Contains(7))
                    {
                        tile = corner[0];
                    }
                    else if (nearDiff.Contains(1) && nearDiff.Contains(4) && !diagonalDiff.Contains(5))
                    {
                        tile = corner[1];
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(3) && !diagonalDiff.Contains(2))
                    {
                        tile = corner[2];
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(4) && !diagonalDiff.Contains(0))
                    {
                        tile = corner[3];
                    }
                }
                else if (nearDiff.Count == 0 && diagonalDiff.Count == 1 && innerCorner.Count > 0)
                {
                    form = "innerCorner";
                    if (diagonalDiff.Contains(0))
                    {
                        tile = innerCorner[0];
                    }
                    else if (diagonalDiff.Contains(2))
                    {
                        tile = innerCorner[1];
                    }
                    else if (diagonalDiff.Contains(5))
                    {
                        tile = innerCorner[2];
                    }
                    else if (diagonalDiff.Contains(7))
                    {
                        tile = innerCorner[3];
                    }
                }

                if (tile == null)
                {
                    tile = exceptionalTiles[random.Next(0, exceptionalTiles.Count)];
                }
            }
        }

        if (!isBorder)
        {
            form = "normal";
            if (random.Next(0, 100) > RareTilePlacementThreshold && pointTiles.Count > 0)
            {
                tile = pointTiles[random.Next(0, pointTiles.Count)];
            }
            else
            {
                tile = tiles[random.Next(0, tiles.Count)];
            }
        }

        return (tile, form);
    }

    public Tile SetNormalTile(System.Random random)
    {
        return tiles[random.Next(0, tiles.Count)];
    }

    public (Tile, bool) SetCliffTile(System.Random random, Map map, int x, int y)
    {
        Tile tile = null;
        bool isBorder = false;
        
        if (exceptionalTiles.Count > 0)
        {
            Cell cell = map.mapData[x][y];
            cell.cliffType = 0;

            List<Cell> neighbors = new List<Cell>();
            List<int> nearDiff = new List<int>();
            List<int> diagonalDiff = new List<int>();

            for (int i = 0; i < 8; i++)
                neighbors.Add(new Cell());

            for (int i = 0; i < 9; i++)
            {
                int nx = x + (i % 3) - 1;
                int ny = y + -((i / 3) - 1);

                if (i != 4)
                {
                    int j = (i < 4) ? i : i - 1;
                    if (map.IsOnMapData(nx, ny))
                    {
                        neighbors[j] = map.mapData[nx][ny];
                        if (neighbors[j].biome != cell.biome)
                        {
                            if (i % 2 == 0)
                            {
                                diagonalDiff.Add(j);
                            }
                            else
                            {
                                nearDiff.Add(j);
                            }
                        }
                    }
                }
            }

            if (nearDiff.Count > 0 || diagonalDiff.Count > 0)
            {
                isBorder = true;
                if (nearDiff.Count == 1)
                {
                    if (nearDiff.Contains(1) && top.Count > 0)
                    {
                        tile = top[random.Next(0, top.Count)];
                        cell.cliffType = 2;
                    }
                    else if (nearDiff.Contains(3) && left.Count > 0)
                    {
                        tile = left[random.Next(0, left.Count)];
                        cell.cliffType = 1;
                    }
                    else if (nearDiff.Contains(4) && right.Count > 0)
                    {
                        tile = right[random.Next(0, right.Count)];
                        cell.cliffType = 1;
                    }
                    else if (nearDiff.Contains(6) && bottom.Count > 0)
                    {
                        tile = bottom[random.Next(0, bottom.Count)];
                        cell.cliffType = 1;
                    }
                }
                else if (nearDiff.Count == 2 && corner.Count > 0)
                {
                    if (nearDiff.Contains(1) && nearDiff.Contains(3))
                    {
                        tile = corner[0];
                        cell.cliffType = 1;
                    }
                    else if (nearDiff.Contains(1) && nearDiff.Contains(4))
                    {
                        tile = corner[1];
                        cell.cliffType = 1;
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(3))
                    {
                        tile = corner[2];
                        cell.cliffType = 1;
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(4))
                    {
                        tile = corner[3];
                        cell.cliffType = 1;
                    }
                }
                else if (nearDiff.Count == 0 && diagonalDiff.Count == 1 && innerCorner.Count > 0)
                {
                    if (diagonalDiff.Contains(0))
                    {
                        tile = innerCorner[0];
                        cell.cliffType = 3;
                    }
                    else if (diagonalDiff.Contains(2))
                    {
                        tile = innerCorner[1];
                        cell.cliffType = 4;
                    }
                    else if (diagonalDiff.Contains(5))
                    {
                        tile = innerCorner[2];
                        cell.cliffType = 5;
                    }
                    else if (diagonalDiff.Contains(7))
                    {
                        tile = innerCorner[3];
                        cell.cliffType = 6;
                    }
                }

                if (tile == null && exceptionalTiles.Count > 0)
                {
                    tile = exceptionalTiles[random.Next(0, exceptionalTiles.Count)];
                }
            }
        }

        if (!isBorder)
            tile = tiles[random.Next(0, tiles.Count)];

        return (tile, isBorder);
    }

    public Tile SetCliffWallTile(System.Random random, Map map, int x, int y, int distance)
    {
        // distance는 위쪽 경계면과 타일을 설치하고자 하는 벽면과의 거리 값은 1 or 2
        Tile tile = null;
        Cell cell = map.mapData[x][y];
        Cell tileCell = map.mapData[x][y - distance];

        if (tileCell.cliffType == 0)
        {
            if (cell.cliffType == 2)
            {
                if (distance == 1)
                    tile = cliffWall[random.Next(0, cliffWall.Count)];
                else if (distance == 2)
                    tile = cliffDarkWall[random.Next(0, cliffDarkWall.Count)];
            }
            else if (cell.cliffType == 3)
            {
                tile = cliffLeftCornerWall[distance - 1];
            }
            else if (cell.cliffType == 4)
            {
                tile = cliffRightCornerWall[distance - 1];
            }
        }
        else if (tileCell.cliffType == 5)
        {
            tile = cliffBottomLeftCornerWall[distance - 1];
        }
        else if (tileCell.cliffType == 6)
        {
            tile = cliffBottomRightCornerWall[distance - 1];
        }

        return tile;
    }

    public Tile SetCorruptionTile(System.Random random, Map map, int x, int y, int corruptionId)
    {
        Tile tile = null;
        bool isBorder = false;

        if (exceptionalTiles.Count > 0)
        {
            List<Cell> neighbors = new List<Cell>();
            List<int> nearDiff = new List<int>();
            List<int> diagonalDiff = new List<int>();

            for (int i = 0; i < 8; i++)
                neighbors.Add(new Cell());

            for (int i = 0; i < 9; i++)
            {
                int nx = x + (i % 3) - 1;
                int ny = y + -((i / 3) - 1);

                if (i != 4)
                {
                    int j = (i < 4) ? i : i - 1;
                    if (map.IsOnMapData(nx, ny))
                    {
                        neighbors[j] = map.mapData[nx][ny];
                        if (neighbors[j].corruptionId != corruptionId)
                        {
                            if (i % 2 == 0)
                            {
                                diagonalDiff.Add(j);
                            }
                            else
                            {
                                nearDiff.Add(j);
                            }
                        }
                    }
                }
            }

            if (nearDiff.Count > 0 || diagonalDiff.Count > 0)
            {
                isBorder = true;
                if (nearDiff.Count == 1)
                {
                    if (nearDiff.Contains(1) && top.Count > 0)
                    {
                        tile = top[random.Next(0, top.Count)];
                    }
                    else if (nearDiff.Contains(3) && left.Count > 0)
                    {
                        tile = left[random.Next(0, left.Count)];
                    }
                    else if (nearDiff.Contains(4) && right.Count > 0)
                    {
                        tile = right[random.Next(0, right.Count)];
                    }
                    else if (nearDiff.Contains(6) && bottom.Count > 0)
                    {
                        tile = bottom[random.Next(0, bottom.Count)];
                    }
                }
                else if (nearDiff.Count == 2 && corner.Count > 0)
                {
                    if (nearDiff.Contains(1) && nearDiff.Contains(3))
                    {
                        tile = corner[0];
                    }
                    else if (nearDiff.Contains(1) && nearDiff.Contains(4))
                    {
                        tile = corner[1];
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(3))
                    {
                        tile = corner[2];
                    }
                    else if (nearDiff.Contains(6) && nearDiff.Contains(4))
                    {
                        tile = corner[3];
                    }
                }
                else if (nearDiff.Count == 0 && diagonalDiff.Count == 1 && innerCorner.Count > 0)
                {
                    if (diagonalDiff.Contains(0))
                    {
                        tile = innerCorner[0];
                    }
                    else if (diagonalDiff.Contains(2))
                    {
                        tile = innerCorner[1];
                    }
                    else if (diagonalDiff.Contains(5))
                    {
                        tile = innerCorner[2];
                    }
                    else if (diagonalDiff.Contains(7))
                    {
                        tile = innerCorner[3];
                    }
                }

                if (tile == null)
                {
                    tile = exceptionalTiles[random.Next(0, exceptionalTiles.Count)];
                }
            }
        }

        if (!isBorder)
        {
            if (random.Next(0, 100) > RareTilePlacementThreshold && pointTiles.Count > 0)
            {
                tile = pointTiles[random.Next(0, pointTiles.Count)];
            }
            else
            {
                tile = tiles[random.Next(0, tiles.Count)];
            }
        }

        return tile;
    }

    public GameObject SetRandomObject(System.Random random)
    {
        GameObject obj = null;
        if (objectsSpawnrate * 10 > random.Next(0, 1000) && objects.Count > 0)
        {
            obj = objects[random.Next(0, objects.Count)];
        }

        return obj;
    }

    public GameObject SetObject(System.Random random)
    {
        GameObject obj = null;
        if (objects.Count > 0)
        {
            obj = objects[random.Next(0, objects.Count)];
        }

        return obj;
    }
}
