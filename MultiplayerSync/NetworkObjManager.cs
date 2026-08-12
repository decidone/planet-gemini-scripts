using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Pathfinding;

public class NetworkObjManager : NetworkBehaviour
{
    public List<Portal> netPortals = new List<Portal>();
    public List<Structure> netStructures = new List<Structure>();
    public List<BeltGroupMgr> netBeltGroupMgrs = new List<BeltGroupMgr>();
    public List<UnitCommonAi> netUnitCommonAis = new List<UnitCommonAi>();
    public List<BeltCtrl> networkBelts = new List<BeltCtrl>();

    public delegate void OnStructureChanged(int type);
    public OnStructureChanged onStructureChangedCallback;

    public delegate void OnUnitChanged(int type);
    public OnUnitChanged onUnitChangedCallback;

    private int _syncTargetStructureCount = -1;
    private int _syncTargetBeltGroupCount = -1;
    private int _syncTargetBeltCount = -1;

    public bool clientSyncComplete = false;

    private int _clientAckedBatchId = -1;
    private ClientRpcParams _syncTargetClient;
    private ulong _syncTargetClientId;

    #region SingletonAwake
    public static NetworkObjManager instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
    #endregion

    [ServerRpc(RequireOwnership = false)]
    public void RequestSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        _syncTargetClientId = rpcParams.Receive.SenderClientId;

        _syncTargetClient = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { _syncTargetClientId } }
        };

        SendSyncTargetClientRpc(netStructures.Count, netBeltGroupMgrs.Count, networkBelts.Count, _syncTargetClient);
    }

    [ClientRpc]
    private void SendSyncTargetClientRpc(int structureCount, int beltGroupCount, int beltCount, ClientRpcParams rpcParams = default)
    {
        _syncTargetStructureCount = structureCount;
        _syncTargetBeltGroupCount = beltGroupCount;
        _syncTargetBeltCount = beltCount;
        StartCoroutine(WaitForSyncCoroutine());
    }

    public void NetObjAdd(WorldObj worldObj)
    {
        if (worldObj.TryGet(out Portal portal))
        {
            netPortals.Add(portal);
        }
        else if (worldObj.TryGet(out Structure structure))
        {
            if(worldObj.TryGet(out BeltCtrl belt))
            {
                networkBelts.Add(belt);
            }
            else
            {
                netStructures.Add(structure);
                onStructureChangedCallback?.Invoke(20);
            }
        }
        else if (worldObj.TryGet(out UnitCommonAi unitCommonAi))
        {
            netUnitCommonAis.Add(unitCommonAi);
            onUnitChangedCallback?.Invoke(23);
        }
    }

    public void BeltGroupAdd(BeltGroupMgr beltGroupMgr)
    {
        netBeltGroupMgrs.Add(beltGroupMgr);
        onStructureChangedCallback?.Invoke(24);
    }

    public void NetObjRemove(WorldObj netObj)
    {
        if (netObj.TryGet(out BeltCtrl belt))
        {
            networkBelts.Remove(belt);
        }
        else if (netObj.TryGet(out Structure structure))
        {
            netStructures.Remove(structure);
        }
        else if (netObj.TryGet(out UnitCommonAi unitCommonAi))
        {
            netUnitCommonAis.Remove(unitCommonAi);
        }
    }

    private IEnumerator WaitForSyncCoroutine()
    {
        yield return new WaitUntil(() =>
            netStructures.Count >= _syncTargetStructureCount &&
            netBeltGroupMgrs.Count >= _syncTargetBeltGroupCount &&
            networkBelts.Count >= _syncTargetBeltCount
        );
        NotifyReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyReadyServerRpc()
    {
        StartCoroutine(SyncCoroutine()); // 서버에서 실행
    }

    private IEnumerator SyncCoroutine()
    {
        const int batchSize = 100;
        const int maxInFlight = 5;

        int currentBatchId = 0;

        IEnumerator WaitForInFlightLimit()
        {
            while (currentBatchId - _clientAckedBatchId > maxInFlight)
                yield return null;
        }

        for (int i = 0; i < netPortals.Count; i++)
        {
            netPortals[i].OnClientConnectedCallback();
        }

        for (int i = 0; i < netStructures.Count; i++)
        {
            netStructures[i].OnClientConnectedCallback();

            bool isLastInBatch = (i + 1) % batchSize == 0;
            bool isLast = i == netStructures.Count - 1;

            if (isLastInBatch || isLast)
            {
                SendBatchBoundaryClientRpc(currentBatchId, _syncTargetClient);
                currentBatchId++;
                yield return WaitForInFlightLimit();
            }
        }

        for (int i = 0; i < networkBelts.Count; i++)
        {
            networkBelts[i].OnClientConnectedCallback();

            bool isLastInBatch = (i + 1) % batchSize == 0;
            bool isLast = i == networkBelts.Count - 1;

            if (isLastInBatch || isLast)
            {
                SendBatchBoundaryClientRpc(currentBatchId, _syncTargetClient);
                currentBatchId++;
                yield return WaitForInFlightLimit();
            }
        }

        for (int i = 0; i < netBeltGroupMgrs.Count; i++)
        {
            netBeltGroupMgrs[i].BeltGroupClientConnectSyncServerRpc(_syncTargetClientId);

            bool isLastInBatch = (i + 1) % batchSize == 0;
            bool isLast = i == netBeltGroupMgrs.Count - 1;

            if (isLastInBatch || isLast)
            {
                SendBatchBoundaryClientRpc(currentBatchId, _syncTargetClient);
                currentBatchId++;
                yield return WaitForInFlightLimit();
            }
        }

        while (_clientAckedBatchId < currentBatchId - 1)
            yield return null;

        StartCoroutine(NotifySyncDelay());
    }

    [ClientRpc]
    private void SendBatchBoundaryClientRpc(int batchId, ClientRpcParams rpcParams = default)
    {
        // 중첩 RPC 무시, 앞의 RPC들이 처리된 후 도착이 보장되므로 바로 ACK
        BatchAckServerRpc(batchId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BatchAckServerRpc(int batchId, ServerRpcParams rpcParams = default)
    {
        _clientAckedBatchId = batchId;
    }

    private IEnumerator NotifySyncDelay()
    {
        yield return new WaitForSecondsRealtime(1f);

        GameManager gameManager = GameManager.instance;
        gameManager.SetClientSyncPauseServerRpc(false);
        gameManager.LoadingPopupServerRpc();
        ClientSyncCompleteClientRpc();
    }

    [ClientRpc]
    void ClientSyncCompleteClientRpc()
    {
        clientSyncComplete = true;
    }

    public void BeltGroupRemove(BeltGroupMgr beltGroupMgr)
    {
        netBeltGroupMgrs.Remove(beltGroupMgr);
    }

    public bool StructureCheck(StructureData strData)
    {
        for (int i = 0; i < netStructures.Count; i++)
        {
            if (netStructures[i].structureData == strData)
            {
                return true;
            }
        }

        return false;
    }

    public bool UnitCheck(UnitCommonData unitData)
    {
        for (int i = 0; i < netUnitCommonAis.Count; i++)
        {
            if (netUnitCommonAis[i].unitCommonData == unitData)
            {
                return true;
            }
        }

        return false;
    }

    public void InitConnectors()
    {
        for (int i = 0; i < netStructures.Count; i++)
        {
            EnergyGroupConnector connector = netStructures[i].GetComponentInChildren<EnergyGroupConnector>();
            if (connector != null)
            {
                connector.RemoveGroup();
            }
        }

        for (int i = 0; i < netStructures.Count; i++)
        {
            EnergyGroupConnector connector = netStructures[i].GetComponentInChildren<EnergyGroupConnector>();
            if (connector != null)
            {
                connector.Init();
            }
        }
    }
}
