# 에너지 그룹 단위 관리 및 생산 효율 계산

에너지 관련 건물마다 부여된 원형 커버 영역을 기준으로 전력망(그룹)을 형성하고, 발전량과 소비량의 비율로 산출된 효율을 소속 건물의 생산 속도에 반영합니다. 그룹 구성 요소 간 시그널 전파(DFS 플러드 필)로 실제 전력 도달 가능성을 판정하여 전력망을 분리·병합하며, `LDConnector`를 활용해 물리적으로 떨어진 거점 전력망도 단일망으로 통합할 수 있도록 구현했습니다.

관련 원본: [`EnergyGroup.cs`](./EnergyGroup.cs) · [`EnergyGroupConnector.cs`](./EnergyGroupConnector.cs) · [`LDConnector.cs`](./LDConnector.cs) · [`EnergyBattery.cs`](./EnergyBattery.cs) · [`EnergyGenerator.cs`](./EnergyGenerator.cs)

> 🎞️ _데모 GIF 예정_


## 구성 요소와 역할

| 구성 요소                  | 역할                                                                                                        |
| ---------------------- | --------------------------------------------------------------------------------------------------------- |
| `EnergyGroup`          | 단일 전력망 데이터 구조입니다. 소속 발전기와 소비 건물의 총량을 집계하여 효율(0~1)을 산출하고, 잉여 전력 저장 및 저장 전력 출력을 통해 부족분을 상쇄합니다.              |
| `EnergyGroupConnector` | 원형 커버 영역을 보유한 전력 접속 노드입니다. 영역이 겹치는 이웃 노드로 시그널을 재귀 전파하여 전력 도달 가능성을 검사하며, 끊김 발생 시 도달 불가 노드들을 별도 그룹으로 분리합니다. |
| `EnergyGroupManager`   | 전체 전력망 목록을 관리하며, 주기적으로 발전량·소비량·효율 수치를 재계산합니다.                                                             |
| `LDConnector`          | 원거리 노드 간 논리적 연결을 형성하여 물리적으로 격리된 전력망을 하나로 병합합니다.                                                           |


## 동작 흐름

```
커넥터 연결/해제 (커버 영역 겹침 또는 LDConnector 원거리 연결)
↓
그룹 병합·분리 (MergeGroup / 시그널 전파로 분리)
↓
EnergyCheck
Charge  — 가동 발전기 생산량 합 → energy
Consume — 가동 소비 건물 합    → consumption
BatteryCheck — energy vs consumption (+배터리) → efficiency (0~1)
↓
Production.effiCooldown = cooldown / efficiency  (전 건물 속도에 반영)
```

### 1. 발전 및 소비량 실시간 집계

전력망에 속한 가동 중인 모든 발전기의 생산량 합과 소비 건물의 요구량을 각각 집계합니다.

```csharp
// EnergyGroup
energy      = connectors[i].energyGenerator.energyProduction;   // Charge
consumption = connectors[i].consumptions[j].energyConsumption;  // Consume
```

### 2. 전력 효율 산출 및 배터리 완충

발전량이 소비량보다 많으면 효율 1을 유지하고 남은 전력을 배터리에 저장합니다. 전력이 부족할 경우 배터리에 저장된 전력을 꺼내 보충하며, 이마저도 부족하면 전력 공급 비율에 비례해 효율(0~1)이 감소합니다.

```csharp
// EnergyGroup.BatteryCheck
if (energy > consumption) { StoreEnergy(energy - consumption); efficiency = 1; }
else { efficiency = Mathf.Clamp((energy + pulled) / consumption, 0, 1); }   // pulled = 배터리 방전분
```

### 3. 시그널 전파 기반 그룹 병합 및 분리

커넥터의 커버 영역이 겹치면 인접 그룹을 하나로 통합하고, 영역 간 끊김이 발생하면 시그널을 전파해 신호가 닿지 않는 노드들을 독립된 전력망으로 분리합니다.

```csharp
// EnergyGroup
MergeGroup(otherGroup);                        // 병합: 커넥터 합치고 EnergyCheck
connectors[0].SendSignal(code);                // 분리: 신호 전파 → 못 받은 커넥터를 새 그룹으로
```

### 4. 원거리 전력망 연결 (LDConnector)

두 `LDConnector` 간 논리적 연결이 형성되면 물리적 거리와 관계없이 동일한 커넥터 인덱스로 묶어 단일 전력망으로 통합합니다.

```csharp
// LDConnector — 다른 LDConnector를 찾아 연결
Cell cell = map.GetCellDataFromPos(x, y);
if (cell.structure.TryGet(out LDConnector othLDConnector)) {
    // EnergyGroupConnector 병합 → 그룹 통합
    mapClick.GameStartSetRenderer(othMapClick);
}
```


## 설계 포인트

- **시그널 전파 기반 네트워크 분리:** 물리적 끊김 발생 시 시그널 전파 탐색을 통해 전력망을 재분할하도록 구현했습니다.
- **그룹 내 생산 시스템 제어:** 전력 효율 수치(0~1) 단 하나로 소속 건물의 작동 속도를 제어하게 설계하여, 전력 시스템과 생산 건물 간의 결합도를 낮추었습니다.
- **배터리 버퍼를 통한 전력 안정화:** 잉여 전력 저장 및 저장 전력 사용 메커니즘을 적용하여 일시적인 부하 수치 변동 시 발생할 수 있는 갑작스러운 생산 정지를 방지했습니다.
- **원거리 거점 전력 공유:** `LDConnector`를 활용해 타일 인접 제약을 극복하고 격리된 다수의 거점을 단일 전력 네트워크로 관리하도록 설계했습니다.


## 동작 확인

- 발전량이 소비량 이상일 때 모든 건물이 100% 속도로 작동하며 잔여 전력이 배터리에 축적됨을 확인했습니다.
- 전력 부족 시 배터리 저장 전력으로 보충하며, 배터리가 소진되면 실제 전력 공급 비율에 따라 생산 속도가 감소함을 확인했습니다.
- 중간 커넥터 철거로 커넥터 간 연결이 끊기면 전력망이 즉시 독립된 두 개로 분리 계산됨을 확인했습니다.
- `LDConnector` 연결 시 멀리 떨어진 두 전력망이 하나의 그룹으로 통합되어 전력 효율을 공유하는 것을 확인했습니다.
