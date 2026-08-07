# 이벤트 기반 캐싱을 통한 Update 연산 최적화

건물이 매 프레임 `Update()`에서 인벤토리 슬롯을 조회하면 건물 수와 프레임에 비례해 불필요한 연산이 지속적으로 누적됩니다.

이를 해결하기 위해 인벤토리 변경을 이벤트(콜백)로 알리고, 해당 이벤트를 구독한 건물이 데이터가 실제로 변경될 때만 상태를 재계산하도록 구조를 전환했습니다. 그 결과 `Update()`는 매 프레임 인벤토리를 직접 조회하는 대신 미리 캐시된 값만 참조합니다.

관련 원본: [`Inventory.cs`](./Inventory.cs) (구독자 예시 `Production.cs`는 [ProductionFactory](../ProductionFactory/README.md) 참고)


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| `Inventory.onItemChangedCallback` (delegate) | 인벤토리 슬롯이 바뀔 때 바뀐 슬롯 번호와 함께 발행되는 변경 이벤트 |
| 구독자 (예: `Production.CheckSlotState` / `CheckInvenIsFull`) | 이벤트를 구독하여 슬롯 데이터 변경 시에만 자주 참조되는 슬롯 값과 가득 참 여부를 캐시에 갱신 |

> 목적: `Update()` 내 매 프레임 인벤 조회를 없애고, `Update()`는 캐시된 값만 쓰게 한다.


## 동작 흐름

```
아이템 슬롯 변경 (Add / Sub / Drag 등)
↓
Inventory: 슬롯 배열 갱신 후 onItemChangedCallback?.Invoke(slotNum)
↓
구독 중인 건물들이 이벤트를 수신하여 상태 재계산
CheckSlotState  — 자주 조회되는 슬롯 값 캐시
CheckInvenIsFull — 가득 참 여부 갱신
↓
Update()는 캐시된 slot / isInvenFull 참조
```

### 1. 발행 · 슬롯이 변경될 때만

인벤토리는 슬롯 배열이 변경되는 시점에만 변경된 슬롯 번호와 함께 이벤트를 호출합니다.

```csharp
// Inventory — SlotAdd/SlotSub 등 슬롯 변경 지점
// (슬롯 배열 갱신) …
onItemChangedCallback?.Invoke(slotNum);
```

### 2. 구독 · Awake에서 연결

건물 객체는 생성 시 이벤트를 구독하여 자기 상태 갱신 메서드를 등록하고, 파괴 시 이를 해제합니다.

```csharp
// Production.Awake / OnDestroy
inventory.onItemChangedCallback += CheckSlotState;   // 자주 쓰는 슬롯 캐시
inventory.onItemChangedCallback += CheckInvenIsFull; // 가득 참 여부
// OnDestroy: -= 로 해제 (누수 방지)
```

### 3. 캐싱 · Update에서 참조

이벤트 발생 시 `CheckSlotState`와 `CheckInvenIsFull`이 갱신해 둔 `slot`, `isInvenFull` 캐시 변수를 `Update()`에서 사용합니다.

```csharp
// Production.CheckSlotState — 콜백 때만 실행
slot  = inventory.SlotCheck(0);   // Update는 이 캐시만 참조
// Update(): if (slot.Item2 >= recipe.amounts[0] && !isInvenFull) …
```


## 설계 포인트

- **연산 방식 전환:** 프레임 기반 폴링에서 옵저버 패턴(이벤트 구독) 구조로 전환하여, 건물 수 × 프레임 단위로 발생하던 인벤토리 검사를 실제 데이터가 변경되는 시점으로 최소화했습니다.
- **데이터 캐싱:** 자주 참조되는 슬롯 상태값과 인벤토리 가득 참 여부를 메모리에 캐시하여, Update() 루틴에서는 단순 값 비교만 수행하도록 구성했습니다.
- **이벤트 수명주기 관리:** 구독과 해제를 `Awake`와 `OnDestroy`에 쌍으로 배치하여 메모리 누수를 방지했습니다.


## 동작 확인

- 인벤토리 데이터에 변경이 없는 동안 상태 재계산 연산이 동작하지 않는 것을 확인했습니다.
- 슬롯 데이터 변경 시에만 캐시가 갱신되며, 건물의 생산 동작 로직은 기존과 동일하게 유지됨을 확인했습니다.
