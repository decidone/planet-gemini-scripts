using Netcode.Transports.Facepunch;
using Newtonsoft.Json;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SteamManager : MonoBehaviour
{
    public string userName;
    public bool getData;
    public SteamId PlayerSteamId { get; set; }
    public SteamId opponentSteamId;
    bool clientReceive;
    int clientCallCount;
    private const int MaxChunkSize = 1024;
    private const int PacketRequestCallInterval = 10;   // 이 횟수마다 누락 패킷 재요청
    private const int RequiredStableFrames = 30;        // 이만큼 프레임이 유지되면 동기화 완료로 판단
    private const int SteamNetworkingTimeout = 20000;   // Steam 네트워킹 연결/응답 타임아웃(ms)
    public bool clientConnTry;
    SoundManager soundManager;

    #region Singleton
    public static SteamManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    #endregion

    private void Start()
    {
        PlayerSteamId = SteamClient.SteamId;
        soundManager = SoundManager.instance;   
    }

    private void Update()
    {
        if (clientReceive)
        {
            ClientConnectGet();
        }
    }

    private void OnEnable()
    {
        SteamMatchmaking.OnLobbyCreated += LobbyCreated;
        SteamMatchmaking.OnLobbyEntered += LobbyEntered;
        SteamMatchmaking.OnLobbyMemberDisconnected += LobbyMemberLeft;
        SteamMatchmaking.OnLobbyMemberLeave += LobbyMemberLeft;
        SteamFriends.OnGameLobbyJoinRequested += GameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined += ClientConnected;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= LobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= LobbyEntered;
        SteamMatchmaking.OnLobbyMemberDisconnected -= LobbyMemberLeft;
        SteamMatchmaking.OnLobbyMemberLeave -= LobbyMemberLeft;
        SteamFriends.OnGameLobbyJoinRequested -= GameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined -= ClientConnected;
    }

    private void LobbyCreated(Result result, Lobby lobby)
    {
        if (result == Result.OK)
        {
            MainGameSetting setting = MainGameSetting.instance;
            if (setting.accessLevel == 0)
            {
                lobby.SetPrivate();
            }
            else if (setting.accessLevel == 1)
            {
                lobby.SetFriendsOnly();
            }
            lobby.SetJoinable(true);
            lobby.SetData("mapSize", setting.mapSizeIndex.ToString());
            lobby.SetData("mapSeed", setting.randomSeed.ToString());
        }
        else
        {
            Debug.Log("Create Lobby Error");
        }
    }

    private void LobbyEntered(Lobby lobby)
    {
        LobbySaver.instance.currentLobby = lobby;
        userName = SteamClient.Name;
        Debug.Log("Entered : " + (lobby.Owner.Id != PlayerSteamId));
        if (lobby.Owner.Id != PlayerSteamId)
        {
            NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>().targetSteamId = lobby.Owner.Id;

            opponentSteamId = lobby.Owner.Id;
            AcceptP2P(opponentSteamId);
            Debug.Log("conn" + opponentSteamId);

            string data = lobby.GetData("mapSize");
            MainGameSetting.instance.MapSizeSet(int.Parse(data));
            data = lobby.GetData("mapSeed");
            MainGameSetting.instance.RandomSeedValue(int.Parse(data));

            TimeStopServerRpc();
            ClientConnectSend();
            StartCoroutine(DataSync());
        }
    }

    [ServerRpc (RequireOwnership = false)]
    public void TimeStopServerRpc()
    {
        TimeStopClientRpc();
    }

    [ClientRpc]
    public void TimeStopClientRpc()
    {
        LoadingPopup.instance.OpenUI("waiting for network connection");
        Time.timeScale = 0;
    }

    void ClientConnected(Lobby lobby, Friend friend)
    {
        if (friend.Id != PlayerSteamId)
        {
            Debug.Log("Client Conn : " + friend.Id);
            opponentSteamId = friend.Id;
            AcceptP2P(opponentSteamId);
            clientReceive = true;

            if (Chat.instance != null)
            {
                Chat.instance.SendMessageServerRpc(friend.Name + " joined!");
            }
        }
    }

    private void AcceptP2P(SteamId opponentId)
    {
        try
        {
            // For two players to send P2P packets to each other, they each must call this on the other player
            SteamNetworking.AcceptP2PSessionWithUser(opponentId);
        }
        catch
        {
            Debug.Log("Unable to accept P2P Session with user");
        }
    }

    // Accept로 연 P2P 세션을 나가기·상대 이탈·종료 시 닫아 세션이 남지 않게 함
    private void CloseP2P(SteamId opponentId)
    {
        try
        {
            SteamNetworking.CloseP2PSessionWithUser(opponentId);
        }
        catch
        {
            Debug.Log("Unable to close P2P Session with user");
        }
    }

    public void ClientConnectSend()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            string message = "ClientConnect";
            byte[] data = Encoding.UTF8.GetBytes(message);
            SteamNetworking.SendP2PPacket(opponentSteamId, data);
            clientCallCount = 0;
            Debug.Log("ClientConnectSend");
        }
    }

    public void ClientConnectGet()
    {
        bool packetAvailable = SteamNetworking.IsP2PPacketAvailable();
        if (packetAvailable)
        {
            var packet = SteamNetworking.ReadP2PPacket();
            string opponentDataSent = Encoding.UTF8.GetString(packet.Value.Data);
            Debug.Log(opponentDataSent);

            if (opponentDataSent == "ClientConnect")
            {
                LoadingPopup.instance.OpenUI("waiting for client");
                Time.timeScale = 0;
                Debug.Log("Time.timeScale = 0 and send;");
                SendP2PPacket();
            }
            else if (opponentDataSent == "DataGetEnd")
            {
                clientReceive = false;
            }
            else if (opponentDataSent == "LossPacket")
            {
                SendP2PPacket();
            }
        }
    }

    public void SendP2PPacket()
    {
        var message = GeminiNetworkManager.instance.RequestJson();

        // 맵, 게임 데이터를 합쳐서 보내고 청크의 [9]바이트로 구분
        byte[] data = Compression.Compress(message.Item1);
        byte[] mapData = message.Item2;

        int mapChunks = Mathf.CeilToInt((float)mapData.Length / MaxChunkSize);
        int totalChunks = mapChunks + Mathf.CeilToInt((float)data.Length / MaxChunkSize);

        // Send each chunk
        for (int i = 0; i < totalChunks; i++)
        {
            if (i < mapChunks)
            {
                int chunkSize = Mathf.Min(MaxChunkSize, mapData.Length - i * MaxChunkSize);
                byte[] chunk = new byte[chunkSize + 10]; // Extra 10 bytes for metadata (index, flag, total chunks, map or game data)
                byte[] index = BitConverter.GetBytes(i);
                Array.Copy(index, 0, chunk, 0, index.Length);
                byte[] total = BitConverter.GetBytes(totalChunks);
                Array.Copy(total, 0, chunk, 4, total.Length);
                chunk[8] = (byte)(i == totalChunks - 1 ? 1 : 0); // Last chunk flag
                chunk[9] = (byte)0; // map data = 0, game data = 1

                Array.Copy(mapData, i * MaxChunkSize, chunk, 10, chunkSize);

                bool success = SteamNetworking.SendP2PPacket(opponentSteamId, chunk, chunk.Length);
                if (success)
                {
                }
                else
                {
                    Debug.LogError($"Map Packet {i + 1}/{totalChunks} Send Failed!");
                }
            }
            else
            {
                int chunkSize = Mathf.Min(MaxChunkSize, data.Length - (i - mapChunks) * MaxChunkSize);
                byte[] chunk = new byte[chunkSize + 10]; // Extra 9 bytes for metadata (index, flag, total chunks)
                byte[] index = BitConverter.GetBytes(i);
                Array.Copy(index, 0, chunk, 0, index.Length);
                byte[] total = BitConverter.GetBytes(totalChunks);
                Array.Copy(total, 0, chunk, 4, total.Length);
                chunk[8] = (byte)(i == totalChunks - 1 ? 1 : 0); // Last chunk flag
                chunk[9] = (byte)1; // map data = 0, game data = 1

                Array.Copy(data, (i - mapChunks) * MaxChunkSize, chunk, 10, chunkSize);

                bool success = SteamNetworking.SendP2PPacket(opponentSteamId, chunk, chunk.Length);
                if (success)
                {
                }
                else
                {
                    Debug.LogError($"Packet {i + 1}/{totalChunks} Send Failed!");
                }
            }
        }
    }

    public bool ReceiveP2PPacket()
    {
        bool packetAvailable = SteamNetworking.IsP2PPacketAvailable();

        if (!packetAvailable)
        {
            return packetAvailable;
        }

        List<byte> receivedData = new List<byte>();
        List<byte> receivedMapData = new List<byte>();
        bool isLastChunkReceived = false;
        int totalChunks = 0;
        HashSet<int> receivedChunkIndices = new HashSet<int>();
        while (packetAvailable && !isLastChunkReceived)
        {
            clientConnTry = true;
            var packet = SteamNetworking.ReadP2PPacket();
            if (packet.HasValue)
            {
                byte[] data = packet.Value.Data;
                int chunkIndex = BitConverter.ToInt32(data, 0);
                totalChunks = BitConverter.ToInt32(data, 4);
                bool isLastChunk = data[8] == 1;  // Last chunk flag

                // Add the chunk data to the receivedData list (excluding the first 3 bytes)
                if (data[9] == 0)
                {
                    receivedMapData.AddRange(data.Skip(10));
                }
                else
                {
                    receivedData.AddRange(data.Skip(10));
                }
                receivedChunkIndices.Add(chunkIndex);

                // Check if it's the last chunk
                if (isLastChunk)
                {
                    isLastChunkReceived = true;
                }
            }
        }

        if (isLastChunkReceived)
        {
            if (receivedChunkIndices.Count == totalChunks)
            {
                Debug.Log("GetDataEnd");
                LoadManager.instance.SetMapSaveData(receivedMapData.ToArray());
                HandleOpponentDataPacket(receivedData.ToArray());
                string message = "DataGetEnd";
                byte[] data = Encoding.UTF8.GetBytes(message);
                SteamNetworking.SendP2PPacket(opponentSteamId, data);

                LoadingUICtrl.Instance.LoadScene("GameScene", false);
            }
            else
            {
                Debug.LogWarning($"Packet Loss: {receivedChunkIndices.Count}/{totalChunks} recive Call");
                RequestMissingPackets();
            }
        }

        clientCallCount++;

        if (clientCallCount > PacketRequestCallInterval)
        {
            clientCallCount = 0;
            RequestMissingPackets();
        }

        return packetAvailable;
    }

    // 누락된 패킷 요청 함수
    private void RequestMissingPackets()
    {
        Debug.Log("RequestMissingPackets");
        // 누락된 패킷 요청 로직 (호스트에게 요청 메시지를 보냄)
        string message = "LossPacket";
        byte[] data = Encoding.UTF8.GetBytes(message);
        SteamNetworking.SendP2PPacket(opponentSteamId, data);
        clientConnTry = false;
    }

    private void HandleOpponentDataPacket(byte[] dataPacket)
    {
        string opponentDataSent = Compression.Decompress(dataPacket);
        SaveData saveData = JsonConvert.DeserializeObject<SaveData>(opponentDataSent);
        LoadManager.instance.SetSaveData(saveData);
        getData = true;
        clientConnTry = false;
    }

    private async void GameLobbyJoinRequested(Lobby lobby, SteamId SteamId)
    {
        await lobby.Join();
    }

    public async void HostLobby()
    {
        await SteamMatchmaking.CreateLobbyAsync(2);
    }

    public async void JoinLobby(Lobby _lobby)
    {

        soundManager.PlayUISFX("ButtonClick");

        // 친구 로비인지 확인 로비가 사라진 경우도 여기에 잡힘
        bool isFriend = false;
        foreach (var friend in SteamFriends.GetFriends())
        {
            if (!friend.IsPlayingThisGame) continue;
            var gameInfo = friend.GameInfo;
            if (!gameInfo.HasValue) continue;
            var friendLobby = gameInfo.Value.Lobby;
            if (friendLobby.HasValue && friendLobby.Value.Id == _lobby.Id)
            {
                isFriend = true;
                break;
            }
        }

        if (!isFriend)
        {
            LobbiesListManager.instance.OpenPopup("This room is no longer available.");
            return;
        }

        var result = await _lobby.Join();
        switch (result)
        {
            case RoomEnter.Success:
                break;
            case RoomEnter.Full:
                LobbiesListManager.instance.OpenPopup("This room is full.");
                break;
            case RoomEnter.DoesntExist:
                LobbiesListManager.instance.OpenPopup("This room has been closed.");
                break;
            default:
                LobbiesListManager.instance.OpenPopup("Failed to join. Please try again.");
                break;
        }
    }

    public async void JoinLobbyWithID(ulong Id)
    {
        Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).RequestAsync();
        if (lobbies == null)
            return;

        foreach (Lobby lobby in lobbies)
        {
            if (lobby.Id == Id)
            {
                await lobby.Join();
                return;
            }
        }
        soundManager.PlayUISFX("ButtonClick");
    }

    public void LeaveLobby()
    {
        LobbySaver.instance.currentLobby?.Leave();
        LobbySaver.instance.currentLobby = null;
    }

    private void LobbyMemberLeft(Lobby lobby, Friend friend)
    {
        CloseP2P(friend.Id);   // 이탈한 상대와의 P2P 세션 종료

        if (!GameManager.instance.isHost)
        {
            if (GameManager.instance.isGameOver)
                return;

            Debug.Log("Host left");
            if (DisconnectedPopup.instance != null)
            {
                DisconnectedPopup.instance.OpenUI("Host Disconnected.");
            }
            else
            {
                LeaveGame();
            }
        }
        else if (lobby.Owner.Id != friend.Id)
        {
            Debug.Log("Client left");
            GameManager.instance.SetClientSyncPauseServerRpc(false);

            if (Chat.instance != null)
            {
                Chat.instance.SendMessageServerRpc(friend.Name + " has left.");
            }
            LoadingPopup.instance.CloseUI();
        }
    }

    public void LeaveGame()
    {
        CloseP2P(opponentSteamId);   // 상대와의 P2P 세션 종료
        LeaveLobby();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        GameManager.instance.DestroyAllDontDestroyOnLoadObjects();
        SceneManager.LoadScene("MainMenuScene");
    }

    public async void GetLobbiesList()
    {

        var results = await SteamFriendLobbyFetcher.instance.FetchFriendLobbiesAsync();
        LobbiesListManager.instance.OpenUI();
        LobbiesListManager.instance.DestroyLobbies();
        
        if (results.Count > 0)
        {
            foreach (var r in results)
            {
                LobbiesListManager.instance.DisplayLobby(r.lobby, r.profile);
            }

            LobbiesListManager.instance.NoLobbiesText(false);
        }
        else
        {
            LobbiesListManager.instance.NoLobbiesText(true);
        }
    }

    IEnumerator DataSync()
    {
        float time = 0.3f;

        while (!getData)
        {
            if (!clientConnTry)
            {
                bool packetAvailable = ReceiveP2PPacket();
                Debug.Log(packetAvailable + " : DataSync packetAvailable Check");
            }

            yield return new WaitForSecondsRealtime(time);
        }

        Debug.Log("ClientDataGet And StartClient");
        ConfigureNetworkTimeouts();
        NetworkManager.Singleton.StartClient();
        StartCoroutine(WaitForNetworkConnection());
    }

    IEnumerator WaitForNetworkConnection()
    {
        Debug.Log("Wait for Network connection");

        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return new WaitForEndOfFrame();
        }

        // 여기서 동기화 완료까지 대기
        int prevCount = 0;
        int stableFrame = 0;

        while (stableFrame < RequiredStableFrames)
        {
            int currentCount = NetworkManager.Singleton.SpawnManager.SpawnedObjects.Count;
            if (currentCount == prevCount)
                stableFrame++;
            else
            {
                stableFrame = 0;
                prevCount = currentCount;
            }
            yield return null;
        }

        GeminiNetworkManager.instance.ClientReadyServerRpc();
        GeminiNetworkManager.instance.ClientSpawnServerRPC();
        Debug.Log("Connected to Network");
    }

    public void ConfigureNetworkTimeouts()
    {
        NetworkManager.Singleton.NetworkConfig.ClientConnectionBufferTimeout = 30;
        NetworkManager.Singleton.NetworkConfig.LoadSceneTimeOut = 180;

        SteamNetworkingUtils.ConnectionTimeout = SteamNetworkingTimeout;
        SteamNetworkingUtils.Timeout = SteamNetworkingTimeout;
    }

    public void GetLobbiesListButtonSound()
    {
        soundManager.PlayUISFX("ButtonClick");
    }
}
