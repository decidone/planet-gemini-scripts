# NPC 상점 및 드론 물류 기반의 경제 시스템

공장에서 생산한 거래용 자원을 판매하여 재화를 획득하고, 이를 활용해 건설 재료, 타 행성 고유 자원의 대체품, 상점 전용 하이테크 아이템을 구매할 수 있습니다. 수동 거래(NPC 상점) 방식과 더불어, 전용 건물이 운송 드론을 마켓으로 왕복시키는 자동 매매 시스템도 함께 구현했습니다.

관련 원본: [`Merchandise.cs`](./Merchandise.cs) · [`Shop.cs`](./Shop.cs) · [`AutoSeller.cs`](./AutoSeller.cs) · [`AutoBuyer.cs`](./AutoBuyer.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/4bfe844d-79f8-4e06-b131-846da9ae4731" />


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| `Merchandise` (ScriptableObject) | 아이템별 구매가·판매가·분해가를 정의하는 데이터 에셋입니다. 수치가 `-1`일 경우 해당 거래 행위가 불가능함을 의미합니다. |
| `Shop` | 수동 거래 UI 관리 및 거래 완료 처리를 담당합니다. 인벤토리 공간과 보유 재화를 검증한 후 거래를 성사시킵니다. |
| `AutoSeller` | 자동 판매 건물입니다. 가동 조건 충족 및 드론 복귀 확인 시 드론을 사용하여 거래재를 마켓으로 운송합니다. |
| `AutoBuyer` | 자동 구매 건물입니다. 필요 재화 확보 및 드론 복귀 확인 시 드론을 마켓으로 파견하여 지정 아이템을 수령해옵니다. |
| `TransportUnit` | 화물을 마켓까지 물리적으로 이송하는 드론 유닛입니다. 외출 및 복귀 상태는 건물의 `isUnitInStr`(`NetworkVariable`)을 통해 동기화합니다. |


## 동작 흐름

```
[수동] NPC 상호작용 → 상점 UI (Merchandise 목록)
↓
품목·수량 담기 → 총액 계산
↓
구매: 인벤 여유 && 재화 ≥ 가격 → 재화 차감 + 아이템 지급
판매: 아이템 → 재화 획득

[자동] AutoSeller / AutoBuyer
↓
조건 충족(생산시간 + 재고/재화) + 드론 복귀(isUnitInStr) 확인
↓
TransportUnit 드론 스폰 → 마켓 위치로 이동
↓
판매: 거래재 넘기고 재화 획득 / 구매: 재화 지불하고 아이템 싣고 복귀
```

### 1. ScriptableObject 기반 품목 정의

아이템별 구매가, 판매가, 분해가 속성을 데이터화하여 관리하며, 수치를 `-1`로 설정해 해당 행위의 수행 가능 여부를 제어합니다.

```csharp
// Merchandise.cs
public class Merchandise : ScriptableObject {
    public Item item;
    public int buyPrice  = -1;   // 구매 불가 품목은 -1
    public int sellPrice = -1;   // 판매 불가 품목은 -1
    public int scrapValue = -1;  // 분해 불가 품목은 -1
}
```

### 2. 수동 거래 검증 및 성사

인벤토리 잔여 슬롯과 보유 재화를 사전 검증한 후 차감 및 지급을 처리하며, 판매 시 아이템 회수와 재화 지급을 진행합니다.

```csharp
// Shop.BuyMerch
if (inventory.MultipleSpaceCheck(merchList)) {
    if (finance.finance >= totalPrice) {
        inventory.BuyMerch(merchList, totalPrice);   // 재화 차감 + 아이템 지급
    }
}
// Shop.SellMerch → inventory.SellMerch(merchList);   // 아이템 회수 + 재화 획득
```

### 3. 드론 기반 자동 매매 파이프라인

자동 매매 건물은 쿨다운 및 드론 복귀 상태(`isUnitInStr`)를 확인한 뒤 드론 인스턴스를 생성해 마켓으로 파견합니다. 파견된 드론은 포탈 위치에 도달해 거래재를 판매하거나 구매한 아이템을 적재한 뒤 건물로 복귀합니다.

```csharp
// AutoSeller — 생산시간마다, 드론이 돌아와 있을 때만
if (prodTimer > cooldown && isUnitInStr.Value) {
    // 판매할 거래재를 모아 드론 스폰 → 마켓(portalPos)으로 이동
    transportUnit.MovePosSet(this, portalPos, invItemCheckDic, totalPrice);
    isUnitInStr.Value = false;   // NetworkVariable — 드론 외출 상태 동기화
}
```


## 설계 포인트

- **데이터 기반 품목 관리:** 품목 및 가격 스펙을 `Merchandise` ScriptableObject로 관리하고, 특수 수치(`-1`)를 활용해 코드 수정 없이 행위별(구매/판매/분해) 거래 제한을 구현했습니다.
- **상점 경제 루프 구축:** 자원 생산, 판매, 구매로 이어지는 경제 순환 구조를 구축하여 공장 생산물의 활용 범위를 확장했습니다.
- **드론 왕복 연출과 상태 동기화:** 자동 매매 건물에서 드론 오브젝트가 포탈까지 물리적으로 이동하도록 구현하고, 화물 적재 여부에 따라 이동 속도가 달라지도록 처리했습니다. 또한 `NetworkVariable`을 활용해 드론의 외출 및 복귀 상태를 실시간 동기화하여 멀티플레이 환경에서도 모든 클라이언트가 동일한 물류 상태를 유지하도록 구현했습니다.


## 동작 확인

- 보유 재화와 인벤토리 슬롯 공간이 모두 확보된 상태에서만 수동 구매가 정상 처리됨을 확인했습니다.
- `AutoSeller` 및 `AutoBuyer` 가동 시 드론이 마켓을 왕복하며 아이템 및 재화를 자동 수송함을 확인했습니다.
- 가용 수치가 `-1`로 설정된 품목은 상점 UI에서 관련 버튼이 비활성화 처리됨을 확인했습니다.
