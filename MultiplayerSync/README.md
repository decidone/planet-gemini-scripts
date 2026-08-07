# 슬라이딩 윈도우를 활용한 멀티플레이 상태 동기화

게임 진행 중 접속한 클라이언트에게 대량의 오브젝트(건물, 벨트, 포탈 등)의 상태를 전달할 때, 단일 프레임에 대량의 RPC를 집중 발송하면 네트워크 트래픽 폭주로 인한 프레임 드랍 및 RPC 유실이 발생합니다. 이를 방지하기 위해 상태 데이터를 배치 단위로 분할 전송하고, 수신 확인(ACK)을 받지 못한 미확인 배치를 일정 수 이하로 유지하는 슬라이딩 윈도우 알고리즘을 구현하여 데이터 전송량을 제어했습니다.

관련 원본: [`NetworkObjManager.cs`](./NetworkObjManager.cs)


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| Unity Netcode `ServerRpc` / `ClientRpc` | 상태 배치 전송 및 클라이언트 수신 확인(ACK) 패킷 통신을 담당합니다. |
| `NetworkObjManager` | 네트워크 오브젝트(건물, 벨트, 벨트 그룹, 포탈, 유닛)를 통합 등록·관리하며 접속 시 상태 동기화를 담당합니다. |
| 배치 전송 · 전송량 제한 | 상태 데이터를 100개 단위 배치로 나누어 전송하고, 미확인 배치를 최대 5개로 제한하여 접속 시점의 네트워크 과부하를 방지합니다. |


## 동작 흐름

```
동기화 요청  (클라이언트 → RequestSyncServerRpc)
↓
서버가 동기화 대상 수 전달
↓
Client가 NetworkObject 생성 확인  (카운트 도달까지 대기)
↓
준비 완료 응답  (NotifyReadyServerRpc)
↓
서버가 상태 동기화 시작
100개씩 배치 전송 → 배치 경계마다 클라이언트 ACK
미확인 배치 > 5 → 대기 (슬라이딩 윈도우)
↓
동기화 완료 (clientSyncComplete)
```

### 1. 요청 · 대상 수 회신

접속한 클라이언트가 동기화를 요청하면, 서버는 해당 클라이언트가 동기화해야 할 전체 오브젝트 수량을 집계하여 전달합니다.

```csharp
// NetworkObjManager.RequestSyncServerRpc
_syncTargetClientId = rpcParams.Receive.SenderClientId;
SendSyncTargetClientRpc(netStructures.Count, netBeltGroupMgrs.Count, networkBelts.Count, _syncTargetClient);
```

### 2. 생성 확인 · 준비 완료

클라이언트는 서버가 전달한 목표 수량만큼 네트워크 오브젝트 스폰이 완료될 때까지 대기한 후, 상태 수신 준비 완료(Ready) 신호를 서버로 전송합니다.

```csharp
// NetworkObjManager.WaitForSyncCoroutine
yield return new WaitUntil(() =>
    netStructures.Count  >= _syncTargetStructureCount &&
    netBeltGroupMgrs.Count >= _syncTargetBeltGroupCount &&
    networkBelts.Count   >= _syncTargetBeltCount);
NotifyReadyServerRpc();
```

### 3. 상태 전송 · 서버 → 클라이언트 (배치)

서버는 오브젝트 상태를 100개 단위 배치로 나누어 수신 클라이언트에 전송하며, 각 배치가 끝나는 시점에 배치 경계 RPC를 발송합니다. 클라이언트는 해당 경계를 수신하면 서버로 즉시 ACK 패킷을 회신합니다.

```csharp
// NetworkObjManager.SyncCoroutine
netStructures[i].OnClientConnectedCallback();
if ((i + 1) % batchSize == 0 || isLast) {
    SendBatchBoundaryClientRpc(currentBatchId, _syncTargetClient);   // 경계 알림
    currentBatchId++;
    yield return WaitForInFlightLimit();
}
// 클라이언트: 경계 수신 → BatchAckServerRpc(batchId) → 서버 _clientAckedBatchId = batchId
```

### 4. 미확인 배치 수 제한

서버는 ACK를 아직 받지 못한 미확인 배치 수가 최대 허용치(`maxInFlight = 5`)에 도달하면 클라이언트의 ACK가 도착할 때까지 다음 배치 전송을 대기합니다.

```csharp
const int batchSize   = 100;
const int maxInFlight = 5;
IEnumerator WaitForInFlightLimit() {
    while (currentBatchId - _clientAckedBatchId > maxInFlight)
        yield return null;
}
```


## 설계 포인트

- **슬라이딩 윈도우 기반 패킷 제어:** 상태 동기화 RPC를 배치 단위로 분할하고 미확인 배치 수를 최대 5개로 제한해 클라이언트 접속 시점의 RPC 유실로 인한 접속 장애를 방지하도록 구현했습니다.
- **오브젝트 스폰 검증을 통한 순서 보장:** 상태 전송 전 클라이언트의 오브젝트 스폰 수량을 미리 검증하도록 구성하여, 동기화 대상 인스턴스가 존재하지 않는 상태에서 데이터가 먼저 수신되어 유실되는 현상을 차단했습니다.
- **배치 경계 기반 ACK 최적화:** Netcode의 RPC 순서 보장 특성을 활용해 개별 오브젝트가 아닌 배치 경계 패킷으로 수신을 검증함으로써, ACK 오버헤드가 최소화되면서 동기화 신뢰성이 확보되도록 구현했습니다.


## 동작 확인

- 수천 개의 오브젝트가 존재하는 환경에서 클라이언트 접속 시 실패 없이 정상 동기화되는 것을 확인했습니다.
- 동기화 진행 중 미확인 배치 수가 설정치인 5개를 초과하지 않고 전송 제어가 유지되는 것을 확인했습니다.
- 클라이언트 측 인스턴스 스폰 완료 응답 이후에 상태 데이터가 순차적으로 적용되는 것을 확인했습니다.
