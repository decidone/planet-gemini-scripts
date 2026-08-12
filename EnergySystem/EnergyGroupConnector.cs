using System.Collections.Generic;
using UnityEngine;

public class EnergyGroupConnector : MonoBehaviour
{
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
