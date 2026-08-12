# ScriptableObject와 오브젝트 풀링을 통한 인벤토리·아이템 관리

아이템 데이터를 `ScriptableObject`로 한 곳에 정의해 프리팹·레시피·인벤토리가 동일한 인스턴스를 참조하도록 구성하고, 인벤토리는 (아이템, 수량) 형태의 슬롯 배열로 관리합니다. 빈번한 생성과 소멸이 발생하는 컨베이어 벨트 위의 아이템은 오브젝트 풀링을 적용해 GC 오버헤드를 최소화했습니다.

관련 스크립트: [`Item.cs`](./Item.cs) · [`ItemList.cs`](./ItemList.cs) · [`Inventory.cs`](./Inventory.cs) · [`Slot.cs`](./Slot.cs) · [`ItemPoolManager.cs`](./ItemPoolManager.cs) · [`ItemProps.cs`](./ItemProps.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/8deee5f3-73e7-4187-b987-9f11761be7bc" />


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| `Item` (ScriptableObject) | 이름·아이콘·티어 등 고유 속성을 정의하는 데이터 에셋입니다. 프리팹·레시피·인벤토리가 동일 인스턴스를 참조하여 메모리 중복을 방지합니다. |
| `ItemList` | 이름 기반 `Item` 검색용 딕셔너리와 정렬/조회용 목록을 제공합니다. |
| `Inventory` / `Slot` | 슬롯 배열로 아이템과 수량을 관리하며, 호스트에서 변경 사항을 검증·확정한 뒤 상태 변경 콜백을 호출합니다. |
| `ItemPoolManager` / `ItemProps` | 벨트 위 대량 아이템 오브젝트를 `ObjectPool` 기반으로 재사용하여 프레임 드랍과 GC 오버헤드를 완화합니다. |


## 동작 흐름

```
아이템 정의 (Item ScriptableObject: name, icon, tier)
↓
ItemList: name → Item 조회 딕셔너리
↓
Inventory: Slot[] (슬롯당 아이템 + 수량)
Add / Sub 는 서버 권한 → 변경 시 onItemChangedCallback 발행
↓
벨트 위 아이템: ItemPoolManager가 ItemProps를 풀링해 재사용
```

### 1. ScriptableObject 기반 데이터 정의

아이템 속성을 ScriptableObject 에셋으로 관리하여 프리팹, 제작 레시피, 인벤토리 슬롯이 단일 인스턴스를 공유하도록 구성했습니다.

```csharp
// Item.cs
public class Item : ScriptableObject {
    new public string name = "New Item";
    public Sprite icon;
    public int tier = -1;
    // …
}
```

### 2. 서버 권한 슬롯 제어 및 이벤트 통지

슬롯 배열 구조로 인벤토리를 관리하며, 아이템 증감 요청은 서버에서 검증·확정한 뒤 이벤트 콜백을 발생시켜 UI를 최신화합니다.

```csharp
// Inventory — 슬롯 변경 후
onItemChangedCallback?.Invoke(slotNum);   // 구독자(건물 UI 등)에 통지
```

### 3. 벨트 아이템 오브젝트 풀링

생성과 파괴가 짧은 주기로 반복되는 벨트 위 아이템은 `UnityEngine.Pool.ObjectPool`을 통해 재사용하여 잦은 생성·소멸에 따른 GC 부하를 줄입니다.

```csharp
// ItemPoolManager
Pool = new ObjectPool<GameObject>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool,
                                  OnDestroyPoolObject, true, defaultCapacity, maxPoolSize);
// 사용: Pool.Get() → 반환: itemPool.Release(gameObject)
```


## 설계 포인트

- **단일 데이터 참조 구조:** 아이템 스펙을 ScriptableObject로 분리해 코드 변경 없이 수치를 조정할 수 있게 하고, 단일 참조 방식으로 메모리 낭비를 방지했습니다.
- **서버 권한 기반 상태 관리:** 인벤토리의 아이템 수량 변경을 서버 권한으로 확정한 뒤 이벤트를 발행하도록 구성해 멀티플레이 환경에서 데이터 정합성과 UI 동기화를 구현했습니다.
- **GC 부하 감소:** 컨베이어 벨트용 아이템에 오브젝트 풀링을 적용해, 벨트 위 아이템이 쉴 새 없이 생성·소멸하는 중에도 인스턴스 파괴 없이 성능을 안정적으로 유지하도록 설계했습니다.


## 동작 확인

- 아이템 데이터 변경 시 이를 참조하는 모든 UI 및 시스템에 즉시 반영됨을 확인했습니다.
- 서버 검증을 거쳐 인벤토리 슬롯 데이터가 변경되고 연결된 UI가 업데이트되는 것을 확인했습니다.
- 컨베이어 벨트 위의 아이템이 소멸 시 파괴되지 않고 오브젝트 풀로 반환되어 재사용되는 것을 확인했습니다.
