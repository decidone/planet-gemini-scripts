# Unity 런타임 취약점 대응을 위한 엔진 업그레이드 및 재빌드

2025년 공개된 Unity 런타임 취약점(CVE-2025-59489)에 대응하기 위해 엔진을 Unity 6(`6000.0.59f2`)으로 전환하고 전체 프로젝트를 재빌드했습니다. 전환 과정에서 발생한 엔진 및 NGO API 변화에 대응하여, 비권장(Deprecated) 메서드 교체 및 RPC 상속 구조 개선 작업을 진행했습니다.


## 배경

- **보안 취약점 대응:** 로컬 환경의 인자 주입 악용 가능성(CVE-2025-59489)을 차단하기 위해 패치 버전 에디터 기반의 전면 재빌드를 진행했습니다.
- **엔진 메이저 업그레이드:** 재빌드 과정에서 Unity 6 전환을 함께 병행하여, 신규 API 및 NGO 버전 변경에 맞춘 코드 구조 개선을 진행했습니다.


## 1. 비권장 메서드 교체

Unity 6에서 경고가 발생하는 비권장 메서드 `FindObjectsOfType` 대신 `FindObjectsByType`을 도입하고, 정렬이 불필요한 곳에 `FindObjectsSortMode.None` 옵션을 지정해 불필요한 정렬 연산 비용을 제거했습니다.

```csharp
// Before (Unity 2021)
Structure[] scripts = FindObjectsOfType<Structure>();
// After (Unity 6) — 정렬이 불필요한 곳은 None으로 지정해 정렬 비용도 제거
Structure[] scripts = Object.FindObjectsByType<Structure>(FindObjectsSortMode.None);
// 단일 탐색: FindObjectOfType<T>() → Object.FindFirstObjectByType<T>()
```


## 2. RPC 상속 구조 개선 (`base` RPC 호출 → 일반 메서드 오버라이드)

Unity 6 전환 및 NGO 2.x(`2.5.1`) 업그레이드 후 파생 클래스가 상위 클래스의 `virtual` RPC를 오버라이드하고 `base.`로 호출하는 코드가 정상 동작하지 않는 버그가 발생했습니다. 이에 RPC 오버라이드를 제거하고, 상위 클래스의 단일 RPC로 데이터를 전송하며 파생 클래스에서는 일반 메서드를 오버라이드하여 동작하는 구조로 변경했습니다.

```csharp
// Before — 파생 클래스가 상위의 virtual RPC를 오버라이드하고 base로 호출 (업그레이드 후 오작동)
public override void ClientConnectSyncServerRpc() {
    base.ClientConnectSyncServerRpc();
}

// After — RPC는 오버라이드하지 않고, 상위의 RPC 하나로 데이터를 묶어 보냄.
//         파생 클래스는 일반 메서드(ApplyExtraSync)를 오버라이드해 자기 데이터만 채움
ClientConnectSyncClientRpc(data);                                  // 상위: 단일 RPC로 데이터 전송
protected override void ApplyExtraSync(StructureSyncData data) { } // 파생: 일반 메서드 오버라이드
```


## 동작 확인

- 보안 패치 버전의 Unity 6 환경에서 프로젝트가 정상 빌드 및 실행됨을 검증했습니다.
- 비권장 메서드 교체를 통해 컴파일 경고가 완전히 제거되었음을 확인했습니다.
- RPC 구조 개선 후 클라이언트 접속 시 건물 동기화 패킷이 정상 송수신됨을 확인했습니다.
