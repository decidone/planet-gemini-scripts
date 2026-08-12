using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public class SteamFriendLobbyFetcher : MonoBehaviour
{
    public class FriendLobbyResult
    {
        public Lobby lobby;
        public FriendProfile profile;
    }

    public class FriendProfile
    {
        public SteamId steamId;
        public string name;
        public Texture2D avatar;
    }

    private readonly Dictionary<ulong, Texture2D> avatarCache = new();

    #region Singleton
    public static SteamFriendLobbyFetcher instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
    #endregion

    public async Task<List<FriendLobbyResult>> FetchFriendLobbiesAsync()
    {
        var results = new List<FriendLobbyResult>();

        foreach (var friend in SteamFriends.GetFriends())
        {
            if (!friend.IsPlayingThisGame) continue;

            var _gameInfo = friend.GameInfo;
            if (!_gameInfo.HasValue) continue;

            var _lobby = _gameInfo.Value.Lobby;
            if (!_lobby.HasValue) continue;

            var _profile = new FriendProfile
            {
                steamId = friend.Id,
                name = friend.Name,
                avatar = await GetOrLoadAvatar(friend.Id)
            };

            results.Add(new FriendLobbyResult
            {
                lobby = _lobby.Value,
                profile = _profile
            });
        }

        return results;
    }

    private async Task<Texture2D> GetOrLoadAvatar(SteamId id)
    {
        if (avatarCache.TryGetValue(id.Value, out var cached))
            return cached;

        try
        {
            var img = await SteamFriends.GetLargeAvatarAsync(id);
            if (!img.HasValue) return null;

            var tex = new Texture2D(
                (int)img.Value.Width,
                (int)img.Value.Height,
                TextureFormat.RGBA32, false
            );
            tex.LoadRawTextureData(img.Value.Data);
            tex.Apply();

            avatarCache[id.Value] = tex;
            return tex;
        }
        catch
        {
            return null;
        }
    }
}
