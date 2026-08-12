using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnergyGroup
{
    EnergyGroupManager groupManager;
    public List<EnergyGroupConnector> connectors = new List<EnergyGroupConnector>();
    List<EnergyGroupConnector> tempConnectors = new List<EnergyGroupConnector>();
    List<EnergyGroupConnector> splitConnectors = new List<EnergyGroupConnector>();

    public float energy;   //생산량, 저장량 나눠야 할 듯
    public float consumption;
    public float efficiency;   //에너지 생산량, 사용량 비율로 충분하면 1, 아니면 비율만큼 생산 효율 감소
    float syncFrequency;

    public bool isHostMapEnergyGroup;

    public EnergyGroup(EnergyGroupManager _groupManager, EnergyGroupConnector conn, bool isHostMap)
    {
        Init();
        groupManager = _groupManager;
        connectors.Add(conn);
        conn.ChangeGroup(this);
        groupManager.AddGroup(this);
        isHostMapEnergyGroup = isHostMap;
    }

    public EnergyGroup(EnergyGroupManager _groupManager, List<EnergyGroupConnector> conns, bool isHostMap)
    {
        Init();
        groupManager = _groupManager;
        connectors = conns.ToList();
        for (int i = 0; i < connectors.Count; i++)
        {
            connectors[i].ChangeGroup(this);
        }

        groupManager.AddGroup(this);
        isHostMapEnergyGroup = isHostMap;
  }

    public void Init()
    {
        syncFrequency = EnergyGroupManager.instance.syncFrequency;
        energy = 0;
    }

    public void AddConnector(EnergyGroupConnector conn, List<EnergyGroupConnector> connList)
    {
        if (!connectors.Contains(conn))
        {
            connectors.Add(conn);
        }

        for (int i = 0; i < connList.Count; i++)
        {
            if (connList[i].group != this && connList[i].group != null)
            {
                MergeGroup(connList[i].group);
            }
        }
    }

    public void RemoveConnector(EnergyGroupConnector conn)
    {
        if (connectors.Contains(conn))
        {
            connectors.Remove(conn);
        }

        if (connectors.Count == 0)
        {
            RemoveGroup();
        }
        else
        {
            ConnectionCheck(0);
        }
    }

    public void RemoveConnectorWithoutCheck(EnergyGroupConnector conn)
    {
        // 클라이언트 입장 시 그룹 재정렬을 위한 초기화용 메서드
        if (connectors.Contains(conn))
        {
            connectors.Remove(conn);
        }

        if (connectors.Count == 0)
        {
            RemoveGroup();
        }
    }

    public void MergeGroup(EnergyGroup group)
    {
        connectors.AddRange(group.connectors);
        for (int i = 0; i < group.connectors.Count; i++)
        {
            group.connectors[i].group = this;
        }

        EnergyCheck();
        group.RemoveGroup();
    }

    public void RemoveGroup()
    {
        groupManager.RemoveGroup(this);
    }

    public void ConnectionCheck(int code)
    {
        code++;
        if (code == 1)
            tempConnectors = connectors.ToList();

        if (connectors.Count != 0)
        {
            connectors[0].SendSignal(code);
        }

        bool isSplited = false;
        for (int i = 0; i < connectors.Count; i++)
        {
            if (connectors[i].signal == 0)
            {
                isSplited = true;
                break;
            }
        }

        if (isSplited)
        {
            for (int i = 0; i < connectors.Count; i++)
            {
                if (connectors[i].signal != code)
                {
                    splitConnectors.Add(connectors[i]);
                }
            }
            for (int i = 0; i < splitConnectors.Count; i++)
            {
                connectors.Remove(splitConnectors[i]);
            }

            EnergyGroup splitGroup = new EnergyGroup(groupManager, splitConnectors, isHostMapEnergyGroup);
            splitGroup.isHostMapEnergyGroup = isHostMapEnergyGroup;
            splitGroup.ConnectionCheck(code);
            splitGroup.EnergyCheck();
            splitConnectors.Clear();
        }

        if (code == 1)
        {
            for (int i = 0; i < tempConnectors.Count; i++)
            {
                tempConnectors[i].ResetSignal();
            }
            tempConnectors.Clear();
        }
    }

    public void TerritoryViewOn()
    {
        for (int i = 0; i < connectors.Count; i++)
        {
            connectors[i].ViewOn();
        }
    }

    public void TerritoryViewOff()
    {
        for (int i = 0; i < connectors.Count; i++)
        {
            connectors[i].ViewOff();
        }
    }

    public void EnergyCheck()
    {
        Charge();
        Consume();
        BatteryCheck();
    }

    public void Charge()
    {
        float temp = 0f;
        for (int i = 0; i < connectors.Count; i++)
        {
            if (connectors[i].energyGenerator != null && connectors[i].energyGenerator.isOperate)
            {
                temp += connectors[i].energyGenerator.energyProduction;
            }
            else if (connectors[i].steamGenerator != null && connectors[i].steamGenerator.isOperate)
            {
                temp += connectors[i].steamGenerator.energyProduction;
            }
        }
        energy = temp;
    }

    public void Consume()
    {
        float temp = 0f;
        for (int i = 0; i < connectors.Count; i++)
        {
            for (int j = 0; j < connectors[i].consumptions.Count; j++)
            {
                if (connectors[i].consumptions[j].isOperate)
                {
                    temp += connectors[i].consumptions[j].energyConsumption;
                }
            }
        }
        consumption = temp;
    }

    void BatteryCheck()
    {
        if (energy > consumption)
        {
            StoreEnergy(energy - consumption);
            efficiency = 1;
        }
        else if (energy == consumption)
        {
            if (energy == 0)
            {
                float stored = 0;
                for (int i = 0; i < connectors.Count; i++)
                {
                    for (int j = 0; j < connectors[i].batteries.Count; j++)
                    {
                        stored = connectors[i].batteries[j].GetStatus();
                        if (stored != 0)
                        {
                            efficiency = 1;
                            return;
                        }
                    }
                }

                if (stored == 0)
                {
                    efficiency = 0;
                    return;
                }
            }
            else
            {
                efficiency = 1;
            }
        }
        else
        {
            float lack = (consumption - energy) * syncFrequency;
            for (int i = 0; i < connectors.Count; i++)
            {
                for (int j = 0; j < connectors[i].batteries.Count; j++)
                {
                    lack = connectors[i].batteries[j].PullEnergy(lack);
                    if (lack == 0)
                    {
                        efficiency = 1;
                        return;
                    }
                }
            }

            if (energy == 0 && lack == (consumption - energy) * syncFrequency)
            {
                efficiency = 0;
                return;
            }

            float pulled = (consumption - energy) - (lack / syncFrequency);
            efficiency = Mathf.Clamp(((energy + pulled) / consumption), 0, 1);
            if (efficiency < 0.001f)
                efficiency = 0;
        }
    }

    void StoreEnergy(float surplus)
    {
        surplus *= syncFrequency;
        for (int i = 0; i < connectors.Count; i++)
        {
            for (int j = 0; j < connectors[i].batteries.Count; j++)
            {
                if (!connectors[i].batteries[j].isPreBuilding)
                    surplus = connectors[i].batteries[j].StoreEnergy(surplus);

                if (surplus == 0)
                    return;
            }
        }
    }
}
