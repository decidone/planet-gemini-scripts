# NGO와 Facepunch Steamworks 기반 멀티플레이 환경 구축

Netcode for GameObjects(NGO)와 Facepunch Steamworks를 활용해 별도 전용 서버 없이 Steam 로비 기반의 호스트-클라이언트 멀티플레이 환경을 구축했습니다. 호스트가 로비를 생성하면 클라이언트는 Steam 오버레이 초대나 게임 내 로비 목록을 통해 참여하며, 데이터 전송용 P2P 세션과 NGO 세션이 순차적으로 연결됩니다.

관련 원본: [`SteamManager.cs`](./SteamManager.cs) · [`GeminiNetworkManager.cs`](./GeminiNetworkManager.cs) · [`SteamFriendLobbyFetcher.cs`](./SteamFriendLobbyFetcher.cs) · [`LobbiesListManager.cs`](./LobbiesListManager.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/20d693f7-e22e-4665-88b3-b2af361be183" />


## 구성 요소와 역할

| 구성 요소                                            | 역할                                                              |
| ------------------------------------------------ | --------------------------------------------------------------- |
| Netcode for GameObjects                          | 호스트-클라이언트 세션 및 RPC 기반 동기화를 담당하는 네트워크 프레임워크입니다.                  |
| Steamworks + Facepunch Transport                 | Steam 로비 매치메이킹 및 NGO 트래픽을 Steam P2P 네트워크로 전달하는 트랜스포트입니다.        |
| `SteamManager`                                   | 로비 생성·참여, P2P 세션 수락·종료, Steam 이벤트를 처리하는 관리 클래스입니다.              |
| `GeminiNetworkManager`                           | 직접 전송이 불가능한 GameObject 및 ScriptableObject를 정수 인덱스로 매핑하여 동기화합니다. |
| `SteamFriendLobbyFetcher` / `LobbiesListManager` | 게임을 플레이 중인 친구의 열린 로비를 조회하여 프로필/아바타와 함께 목록 UI로 구성합니다.            |


## 동작 흐름

```
로비 생성 (호스트) — CreateLobby → OnLobbyCreated
접근 수준(비공개/친구/공개) + SetJoinable + 맵 설정 데이터 저장
↓
친구 참여 — 오버레이 초대(OnGameLobbyJoinRequested)
  또는 친구 로비 목록 조회(GetLobbiesList)에서 선택 입장
↓
로비 입장 — OnLobbyEntered
Transport.targetSteamId = 호스트 SteamId(연결 대상 지정) + AcceptP2P(채널 1 세션 수락)
↓
채널 1 P2P(SendP2PPacket)로 대용량 세이브·맵 전송 (DataSync)
↓
StartClient() → 채널 2 FacepunchTransport(SteamNetworkingSockets 릴레이)로 NGO 세션 연결
↓
나가기 / 상대 이탈 / 종료 — CloseP2P(채널 1) + NetworkManager.Shutdown(채널 2)
```

### 1. 로비 생성 · 접근 설정

호스트가 로비를 생성할 때 접근 권한을 설정하고 참여 가능 상태로 변경한 뒤, 맵 크기 및 시드 정보를 로비 데이터에 저장합니다.

```csharp
// SteamManager.LobbyCreated
if (setting.accessLevel == 0)      lobby.SetPrivate();
else if (setting.accessLevel == 1) lobby.SetFriendsOnly();
lobby.SetJoinable(true);
lobby.SetData("mapSize", setting.mapSizeIndex.ToString());
lobby.SetData("mapSeed", setting.randomSeed.ToString());
```

### 2. 친구 참여

Steam 오버레이 초대 수락 시 발생 이벤트 콜백을 처리하여 해당 로비로 입장을 진행합니다.

```csharp
// SteamManager.OnEnable
SteamFriends.OnGameLobbyJoinRequested += GameLobbyJoinRequested;   // 친구 방 참여 요청
```

### 2-1. 친구 로비 목록 조회

초대 수락 외에도 게임 내에서 친구들이 개설한 로비 목록을 직접 조회하여 선택 입장할 수 있습니다. 동일한 게임을 플레이 중인 친구 목록을 순회하여 로비 존재 여부를 확인하고, 프로필 정보와 함께 UI에 출력합니다.

```csharp
// SteamManager.GetLobbiesList → SteamFriendLobbyFetcher.FetchFriendLobbiesAsync
foreach (var friend in SteamFriends.GetFriends()) {
    if (!friend.IsPlayingThisGame) continue;
    var lobby = friend.GameInfo?.Lobby;
    if (lobby.HasValue)
        results.Add(new FriendLobbyResult { lobby = lobby.Value, profile = ... });
}
// LobbiesListManager가 목록으로 표시 → 선택 시 lobby.Join()
```

### 3. 입장 · 용도별 두 P2P 채널

로비 입장 시 호스트의 SteamID를 트랜스포트 대상 주소로 지정하고, `SteamNetworking` P2P 세션을 수락합니다. 본 시스템은 목적에 따라 분리된 두 개의 P2P 채널을 활용합니다.

- 채널 1 (`SteamNetworking` / `SendP2PPacket`): 접속 직후 대용량 세이브, 맵, 청사진 데이터를 청크 단위로 직접 전송합니다. NGO RPC 패킷 크기 제한을 우회하기 위한 커스텀 벌크 전송 방식입니다.
- 채널 2 (`FacepunchTransport` / `SteamNetworkingSockets` 릴레이): NGO의 실시간 게임 상태 동기화 트래픽을 처리합니다.

```csharp
// SteamManager.LobbyEntered (클라) — 연결 대상 지정 + 채널 1 세션 수락
GetComponent<FacepunchTransport>().targetSteamId = lobby.Owner.Id;   // 연결 "대상"만 지정
AcceptP2P(opponentSteamId);   // = SteamNetworking.AcceptP2PSessionWithUser (채널 1)
```

클라이언트는 채널 1을 통해 대용량 데이터 수신을 완수한 뒤 `StartClient()`를 호출하며, 이 시점에 채널 2(NGO 트랜스포트)가 연결됩니다.

```csharp
// SteamManager.DataSync — 채널 1 수신 완료 후에야 NGO 연결
while (!getData) ReceiveP2PPacket();      // 채널 1로 세이브·맵 다 받을 때까지 대기
NetworkManager.Singleton.StartClient();   // → FacepunchTransport.ConnectRelay = 채널 2 연결
```

### 4. 종료 · 세션 정리

채널 1(`SteamNetworking`) 세션은 명시적으로 닫지 않으면 메모리에 남으므로, 게임 퇴장·상대 이탈·종료 시 `CloseP2PSessionWithUser`로 직접 해제합니다. 반면 채널 2(NGO)는 `NetworkManager.Shutdown()`이 세션과 FacepunchTransport 연결까지 함께 정리하므로 별도의 수동 종료가 필요 없습니다.

```csharp
// SteamManager.LeaveGame
CloseP2P(opponentSteamId);            // 채널 1: SteamNetworking 세션 수동 해제
NetworkManager.Singleton.Shutdown();  // 채널 2: NGO 세션·FacepunchTransport 정리
```


## 설계 포인트

- **호스트-클라이언트 멀티플레이 아키텍처:** 별도의 전용 서버 운용 없이 Steam 로비와 P2P 네트워크를 조합하고 방을 생성한 호스트가 서버 역할을 겸하도록 구성하여 인프라 비용 없는 멀티플레이를 구현했습니다.
- **용도별 P2P 채널 분리:** 대용량 세이브 및 맵 데이터는 `SteamNetworking`을 통해 청크 단위로 직접 전송하여 NGO RPC의 패킷 크기 제한을 회피하고, 실시간 게임 동기화는 FacepunchTransport로 처리하도록 네트워크 역할을 분리했습니다.
- **로비 데이터 기반 환경 동기화:** 플레이어 접속 전 맵 시드 및 크기 정보를 Steam 로비 데이터에 등록하여 로비에 입장하지 않고서도 해당 데이터에 접근이 가능하도록 구현했습니다.
- **인게임 친구 로비 탐색:** Steam 오버레이 초대 방식 외에도 인게임에서 현재 플레이 중인 친구의 열린 로비를 조회하고 프로필 정보와 함께 목록화하여 즉시 참여할 수 있도록 구성했습니다.
- **P2P 세션 수명주기 관리:** `AcceptP2PSessionWithUser`로 생성된 P2P 세션을 플레이어 이탈 및 종료 시점에 `CloseP2PSessionWithUser`로 명시적으로 닫아 메모리와 네트워크 세션 잔류를 방지하도록 설계했습니다.


## 동작 확인

- 호스트가 생성한 로비에 스팀 오버레이 초대 및 로비 목록 선택을 통해 정상적으로 접속되는 것을 확인했습니다.
- 스팀 친구가 개설하여 플레이 중인 로비가 UI 목록에 표시되는 것을 확인했습니다.
- 클라이언트 입장 시 대용량 데이터 전송용 P2P 세션과 NGO 동기화 세션이 순차적으로 연결되는 것을 확인했습니다.
- 전달된 데이터를 바탕으로 호스트와 클라이언트가 동일한 맵을 생성하는 것을 확인했습니다.
