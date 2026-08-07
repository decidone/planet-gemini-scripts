using System.Collections.Generic;
using UnityEngine;

public class EnergyGroupConnector : MonoBehaviour
{
    #region Memo
    /*
     * 에너지 공급 범위, 연결, 그룹관련 하청
     * 
     * 기능
     *    1. 에너지 공급 범위 관리
     *    2. 해당 오브젝트에서 감지된 다른 에너지건물 관리
     *       1. 다른 건물 추가
     *          새로 추가될 때 해당 건물에서 기존에 있던 건물들의 그룹 확인
     *          전부 같은 그룹이면 해당 그룹에 편입,
     *          전부 다른 그룹이면 확인된 그룹들 병합
     *       2. 다른 건물 삭제
     *          건물이 삭제될 때 해당 건물에서 기존에 있던 건물들이 서로 연결되어 있는지 확인
     *          연결되어있지 않다면 연결된 상태에 따라 그룹을 분리, 분리될 그룹은 2개보다 더 많을 수도 있음
     *          확인 방법
     *              건물 철거 시 해당 오브젝트에 연결된 오브젝트들을 에너지 그룹 매니저에 전달 후 철거
     *              그룹 매니저에서는 각각의 오브젝트의 연결 상태를 체크
     *              직접 연결이든 중계를 통한 연결이든 서로 다 연결이 되어있으면 별다른 조치 없이 마무리
     *              서로 연결이 안 되어있는 오브젝트들이 있다면 - 이거 문제있음 딴거 생각
     *    3. 다른 건물 건설, 철거 시 처리는 어디에서 할 지 생각할 것
     *    
     *    메모
     *      생각보다 해당 그룹 전체를 체크하는게 리소스를 많이 안 먹을수도 있음
     *      그냥 끊기는 순간 랜덤으로 작업번호 만들고 해당 번호 전파하면서 한 쪽에서 그룹 재지정 해주고
     *      끊겼던 다른 오브젝트 체크해서 해당 작업번호 가지고있으면 넘기고 안 가지고 있으면 그 오브젝트에서 또 랜덤 작업번호 주고 쭉 재지정 반복
     *      이런 식으로 하면 각각 오브젝트에서 자기와 연결된 오브젝트에 매개변수로 작업번호만 주고 도미노 식으로 매서드 돌리면 됨
     *      
     *      연결 상태 체크도 마찬가지
     *      굳이 그룹매니저에서 오브젝트들을 다 받아와서 하나하나 연결상태 체크하는 것보다
     *      그냥 오브젝트끼리 랜덤생성 된 작업번호 전파하라고 해서 가지고있으면 연결, 안가지고 있으면 비연결 상태로 간주하면 될 듯
     *      
     *      작업번호보다 더 좋고 확실한 방법이 있는지는 생각
     */
    #endregion

    [HideInInspector]
    public Structure structure;
    public bool isBuildDone;
    EnergyGroupManager groupManager;
    public List<EnergyGroupConnector> tempConnectors;
    public List<EnergyGroupConnector> connectors;
    public List<Structure> nearbyStr;
    public List<Structure> consumptions;
    public List<EnergyBattery> nearbyBat;
    public List<EnergyBattery> batteries;
    public EnergyGroup group;   //속한 에너지 그룹. 그룹매니저랑 구분
    [SerializeField]
    SpriteRenderer view;
    public int signal;
    [HideInInspector]
    public EnergyGenerator energyGenerator;
    [HideInInspector]
    public SteamGenerator steamGenerator;

    void Awake()
    {
        isBuildDone = false;
        signal = 0;
        groupManager = EnergyGroupManager.instance;
        structure = GetComponentInParent<Structure>();
        tempConnectors = new List<EnergyGroupConnector>();
        connectors = new List<EnergyGroupConnector>();
        nearbyStr = new List<Structure>();
        consumptions = new List<Structure>();
        energyGenerator = GetComponentInParent<EnergyGenerator>();
        steamGenerator = GetComponentInParent<SteamGenerator>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Energy"))
        {
            EnergyGroupConnector connector = collision.GetComponent<EnergyGroupConnector>();
            if (connector)
            {
                tempConnectors.Add(connector);
            }
        }
        if (collision.TryGetComponent(out Structure structure))
        {
            if (!nearbyStr.Contains(structure) && structure.energyUse)
            {
                nearbyStr.Add(structure);
                structure.AddConnector(this);
            }
            if (structure.TryGet(out EnergyBattery bat))
            {
                if (!nearbyBat.Contains(bat))
                {
                    nearbyBat.Add(bat);
                    bat.AddConnector(this);
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Energy"))
        {
            EnergyGroupConnector connector = collision.GetComponent<EnergyGroupConnector>();
            if (connector)
            {
                tempConnectors.Remove(connector);
            }
        }
        if (collision.TryGetComponent(out Structure structure))
        {
            if (nearbyStr.Contains(structure))
            {
                nearbyStr.Remove(structure);
                structure.RemoveConnector(this);
            }
            if (structure.TryGet(out EnergyBattery bat)){
                if (nearbyBat.Contains(bat))
                {
                    nearbyBat.Remove(bat);
                    bat.RemoveConnector(this);
                }
            }
        }
    }

    public void Init()
    {
        isBuildDone = true;
        for (int i = 0; i < nearbyStr.Count; i++)
        {
            if (nearbyStr[i].energyUse)
                nearbyStr[i].AddConnector(this);
        }
        for (int i = 0; i < nearbyBat.Count; i++)
        {
            if (nearbyBat[i].TryGet(out EnergyBattery bat))
            {
                bat.AddConnector(this);
            }
        }

        for (int i = 0; i < tempConnectors.Count; i++)
        {
            if (tempConnectors[i].isBuildDone)
            {
                connectors.Add(tempConnectors[i]);
                tempConnectors[i].CheckAndAdd(this);
            }
        }

        if (connectors.Count == 0)
        {
            group = new EnergyGroup(groupManager, this, structure.isInHostMap);
        }
        else
        {
            for (int i = 0; i < connectors.Count; i++)
            {
                if (connectors[i].group != null)
                    group = connectors[i].group;
            }

            if (group == null)
                group = new EnergyGroup(groupManager, this, structure.isInHostMap);
            else
                group.AddConnector(this, connectors);
        }
    }

    public void CheckAndAdd(EnergyGroupConnector conn)
    {
        if (!connectors.Contains(conn))
        {
            connectors.Add(conn);
        }
    }

    public void RemoveFromGroup()
    {
        for (int i = 0; i < connectors.Count; i++)
        {
            connectors[i].SubtractConnector(this);
        }
        if (group != null)
        {
            group.RemoveConnector(this);
            group = null;
        }
    }

    public void RemoveGroup()
    {
        if (group != null)
        {
            group.RemoveConnectorWithoutCheck(this);
            group = null;
        }
    }

    public void SubtractConnector(EnergyGroupConnector conn)
    {
        if (connectors.Contains(conn))
        {
            connectors.Remove(conn);
        }
    }

    public void SendSignal(int code)
    {
        if (signal == 0)
        {
            signal = code;
            for (int i = 0; i < connectors.Count; i++)
            {
                connectors[i].SendSignal(code);
            }
        }
    }

    public void ResetSignal()
    {
        signal = 0;
    }

    public void AddConsumption(Structure str)
    {
        if (!consumptions.Contains(str))
        {
            consumptions.Add(str);
        }
    }

    public void RemoveConsumption(Structure str)
    {
        if (consumptions.Contains(str))
        {
            consumptions.Remove(str);
        }
    }

    public void AddBattery(EnergyBattery bat)
    {
        if (!batteries.Contains(bat))
        {
            batteries.Add(bat);
        }
    }

    public void RemoveBattery(EnergyBattery bat)
    {
        if (batteries.Contains(bat))
        {
            batteries.Remove(bat);
        }
    }

    public void ChangeGroup(EnergyGroup _group)
    {
        group = _group;
    }

    public void ViewOn()
    {
        view.enabled = true;
    }

    public void ViewOff()
    {
        view.enabled = false;
    }
}
