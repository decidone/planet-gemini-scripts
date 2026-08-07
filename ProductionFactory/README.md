# JSON 데이터 기반 파이프라인을 통한 생산·공장 자동화

제련, 조립, 유닛 생산 등 모든 공장 오브젝트가 `Production` 클래스를 상속하며, JSON 기반의 `Recipe` 데이터(재료, 생산품, 소요 시간)를 기반으로 동작합니다. 공통 생산 로직을 단일 파이프라인으로 구성하여, 코드 수정 없이 레시피 정의만으로 건설 재료·연구 재료·전투 소모품·전투 유닛·거래용 아이템까지 동일한 메커니즘으로 생산하도록 구현했습니다.

관련 원본: [`Production.cs`](./Production.cs) · [`Recipe.cs`](./Recipe.cs) · [`RecipeList.cs`](./RecipeList.cs) · [`Smelter.cs`](./Smelter.cs) · [`UnitFactory.cs`](./UnitFactory.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/b3b2f1c1-1f95-4e8d-b0c2-42996b8a7e15" />


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| 상속 계층 `Structure → Production → (각 건물)` | 생산 루프, 슬롯 검사, 아이템 배출 등 공통 처리 로직을 `Production`에 모아서 구현하고, 하위 건물은 슬롯 구성·레시피·UI 등 건물마다 다른 부분을 정의합니다. |
| `Recipe` / `RecipeList` | 건물별 필요 재료, 결과물, 생산 소요 시간을 외부 데이터로 분리하여 코드 수정 없이 생산 항목을 관리할 수 있습니다. |
| `EnergyGroup.efficiency` | 전력망 효율(0~1)을 기반으로 실시간 생산 소요 시간을 산출해 생산 속도를 제어합니다. |
| Netcode `ServerRpc` / `ClientRpc` | 재료 차감과 배출 처리를 서버 권한으로 수행하고 RPC를 통해 결과를 동기화함으로써 멀티플레이 간 상태 불일치를 방지합니다. |


## 동작 흐름

```
입력 슬롯에 재료
↓
Update: 가동 조건 판정 (레시피 + 전력 효율 + 재료·출력 여유)
↓
prodTimer 누적 → effiCooldown 도달
↓
서버: 입력 소모 + 출력 생성 + Overall 집계
↓
출력 슬롯 → SendItem (ServerRpc → ClientRpc) → 벨트
```

레시피만 바꾸면 같은 골격으로 건설재, 연구재, 전투 소모품, 전투 유닛(UnitFactory), 거래재를 생산할 수 있습니다.

### 1. 가동 조건 판정 및 타이머 갱신

할당된 레시피 존재 여부, 전력 공급 상태, 입력 슬롯의 재료 유무, 출력 슬롯의 공간 여유를 검사하여 가동 조건을 만족할 때만 타이머를 활성화합니다.

```csharp
// Smelter.Update (Production 공통 패턴)
if (conn?.group != null && conn.group.efficiency > 0
    && slot.Item2 >= recipe.amounts[0] && slot1.Item2 >= recipe.amounts[1]   // 입력 재료 충분
    && slot2.Item2 + recipe.amounts[^1] <= maxAmount) {                      // 출력 슬롯 여유
    prodTimer += Time.deltaTime;
    if (prodTimer > effiCooldown - ((overclockOn ? effiCooldown * overclockPer / 100 : 0) + effiCooldownUpgradeAmount))
        Produce();   // 재료 소모 + 출력 생성 (2단계)
}
```

### 2. 서버 권한 기반 생산 확정

지정한 생산 시간에 도달하면 서버에서 입력 재료를 차감하고 출력 슬롯에 결과물을 생성하며, 전체 생산 통계를 집계합니다.

```csharp
inventory.SlotSubServerRpc(0, recipe.amounts[0]);          // 입력 소모
inventory.SlotAdd(2, output, recipe.amounts[^1]);          // 출력 생성
Overall.instance.OverallProd(output, recipe.amounts[^1]);  // 통계 집계
```

### 3. 전력 효율 기반 속도 제어

기본 소요 시간을 전력망 효율 수치로 나누어 실제 소요 시간을 계산합니다. 전력 공급이 부족하면 소요 시간이 늘어나며, 효율이 0일 경우 가동을 정지합니다.

```csharp
// Production.EfficiencyCheck
effiCooldown = cooldown / efficiency;   // 여기에 오버클럭·연구 보정이 추가로 차감
```

### 4. 아이템 배출 및 동기화

출력 슬롯의 완성품 배출을 서버에서 판정하고, `ClientRpc`를 통해 각 클라이언트의 벨트 위에 해당 아이템 오브젝트를 스폰해 이송을 시작합니다.

```csharp
SendItemServerRpc(itemIndex, outObjIndex);   // → ClientRpc → 각 클라이언트가 풀에서 스폰
```


## 설계 포인트

- **공통 생산 파이프라인 수립:** 슬롯 검사, 생산, 배출로 이어지는 흐름을 `Production` 클래스에 공통화하여 하위 건물의 코드 중복을 최소화했습니다.
- **데이터 기반 생산 확장:** 재료, 결과물, 생산 시간을 `Recipe` 데이터로 분리해 코드 수정 없이 신규 아이템 및 유닛 생산 로직을 추가할 수 있도록 구현했습니다.
- **서버 권한 상태 동기화:** 재료 소모, 결과물 생성, 배출 판정을 서버에서 확정한 후 RPC로 전파하는 방식으로 멀티플레이 환경의 데이터 정합성을 확보했습니다.


## 동작 확인

- 필요한 재료 및 전력 공급 조건 충족 시 설정된 소요 시간마다 정상적으로 1회 생산이 완료됨을 확인했습니다.
- 전력망 효율 저하 시 생산 소요 시간이 비례하여 증가하며, 효율 0일 때 생산이 정지됨을 확인했습니다.
- 단일 생산 파이프라인 내에서 레시피 설정에 따른 아이템/유닛이 생산되는 것을 확인했습니다.
- 생산 완료된 아이템이 출력 슬롯을 거쳐 컨베이어 벨트 오브젝트에 스폰되는 것을 확인했습니다.
