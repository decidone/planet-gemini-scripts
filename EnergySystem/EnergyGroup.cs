using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnergyGroup
{
    /*
     * 그룹 클래스
     * 그룹에 필요한 기본적인 변수
     *    그룹 매니저, 에너지 총량, 그룹에 속한 커넥터 리스트
     * 메서드
     *    새로운 그룹 생성(여기는 생성자)
     *    기존 그룹 제거
     *    그룹에 커넥터 있는지 확인하는 기능
     *    그룹에 추가 -> 여기서 첫 번째 커넥터가 추가될 때 그룹 생성, 그룹매니저에 추가
     *    그룹에서 제거 -> 여기서 마지막 커넥터가 제거될 때 그룹매니저에서 제거
     *    에너지 추가
     *    에너지 사용
     *    해당 그룹의 커넥터들이 전부 연결되어있는지 확인하는 기능
     *       리스트의 첫 번째 커넥터에서 시그널 전달, 받으면 bool값 true, 다 돌린다음 못 받은 커넥터가 있는지 확인
     *       못 받은 커넥터중 첫 번째 커넥터들로 새로운 그룹 클래스를 만들고 시그널 전달 반복
     * 기존 그룹매니저에 커넥터 리스트 다 넣는 방식에서 > 그룹 클래스 리스트로 관리하는 방식으로 변경
     * 에너지 사용은 그룹매니저에서 터치하지 않음
     * 그룹 매니저는 그룹 클래스 추가, 제거만 확인
     */

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
