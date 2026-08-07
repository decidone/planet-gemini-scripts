using System.Collections;
using UnityEngine;

public class LDConnector : Structure
{
    public EnergyGroupConnector connector;
    [HideInInspector]
    public bool isBuildDone;
    PreBuilding preBuilding;
    Structure preBuildingStr;
    bool preBuildingCheck;
    [HideInInspector]
    public MapClickEvent clickEvent;

    protected override void Awake()
    {
        base.Awake();
        isBuildDone = false;
    }

    protected void Start()
    {
        clickEvent = Get<MapClickEvent>();
        gameManager = GameManager.instance;
        preBuilding = PreBuilding.instance;
        view.enabled = false;
    }

    protected override void Update()
    {
        base.Update();

        if (gameManager.focusedStructure == null)
        {
            if (preBuilding.isBuildingOn && !removeState)
            {
                if (!preBuildingCheck)
                {
                    preBuildingCheck = true;
                    if (preBuilding.isEnergyUse || preBuilding.isEnergyStr)
                    {
                        view.enabled = true;
                    }
                }
            }
            else
            {
                if (preBuildingCheck)
                {
                    preBuildingCheck = false;
                    view.enabled = false;
                }
            }
        }
        if (!isPreBuilding)
        {
            if (!isBuildDone)
            {
                connector.Init();
                isBuildDone = true;
            }
        }

        if (connector != null && connector.group != null)
        {
            if (removeState)
            {
                connector.RemoveFromGroup();
            }
            else
            {
                if (connector.group.efficiency > 0)
                {
                    OperateStateSet(true);
                }
                else
                {
                    OperateStateSet(false);
                }
            }
        }
    }

    public override void ClientConnectSync()
    {
        var data = CollectBaseSyncData();

        if (clickEvent != null && clickEvent.lines.Count > 0)
        {
            Vector3[] linePositions = new Vector3[clickEvent.lines.Count];
            for (int i = 0; i < clickEvent.lines.Count; i++)
            {
                LineRenderer lineRenderer = clickEvent.lines[i];
                MapLine lineProps = lineRenderer.GetComponent<MapLine>();
                linePositions[i] = lineProps.lineTarget.transform.position;
            }
            data.connectedLinePositions = linePositions;
        }

        ClientConnectSyncClientRpc(data);
    }

    protected override void ApplyExtraSync(StructureSyncData data)
    {
        if (data.connectedLinePositions != null)
        {
            foreach (var pos in data.connectedLinePositions)
            {
                StartCoroutine(SetInvoke(pos));
            }
        }
    }

    IEnumerator SetInvoke(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        Map map;
        if (isInHostMap)
            map = gameManager.hostMap;
        else
            map = gameManager.clientMap;

        Cell cell = map.GetCellDataFromPos(x, y);

        if (cell.structure == null)
        {
            yield return null;
            StartCoroutine(SetInvoke(pos));
        }
        else
        {
            Structure findObj = cell.structure;
            if (findObj != null && findObj.TryGet(out LDConnector othLDConnector))
            {
                if (TryGet(out MapClickEvent mapClick) && othLDConnector.TryGet(out MapClickEvent othMapClick))
                {
                    mapClick.GameStartSetRenderer(othMapClick);
                }
            }
        }
    }

    public override void Focused()
    {
        if (connector.group != null)
        {
            connector.group.TerritoryViewOn();
        }
    }

    public override void DisableFocused()
    {
        if (connector.group != null)
        {
            connector.group.TerritoryViewOff();
        }
    }

    public override void RemoveObjServer()
    {
        //건물 철거 전 처리
        DisableFocused();
        connector.RemoveFromGroup();
        clickEvent.RemoveAllLines();

        RemoveObjClientRpc();
    }

    public override StructureSaveData SaveData()
    {
        StructureSaveData data = base.SaveData();

        for (int i = 0; i < clickEvent.lines.Count; i++)
        {
            LineRenderer lineRenderer = clickEvent.lines[i];
            MapLine lineProps = lineRenderer.GetComponent<MapLine>();
            data.connectedStrPos.Add(Vector3Extensions.FromVector3(lineProps.lineTarget.gameObject.transform.position));
        }

        return data;
    }
    public override (bool, bool, bool, EnergyGroup, float) PopUpEnergyCheck()
    {
        if (connector != null && connector.group != null)
        {
            return (energyUse, isEnergyStr, false, connector.group, energyConsumption);
        }

        return (false, false, false, null, 0);
    }

    protected override void NonOperateStateSet(bool isOn)
    {
        if (animController == null) return;

        if (isOn)
        {
            if (!animController.isInitialized)
            {
                setModel.material = shaderAnimatedMat;
            }
            animController.Refresh();
        }
        else
        {
            animController.SetStaticSprite(strImg[0]);
        }
    }
}
