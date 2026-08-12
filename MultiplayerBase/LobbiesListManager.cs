using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static SteamFriendLobbyFetcher;

public class LobbiesListManager : MonoBehaviour
{
    public GameObject lobbiesList;
    public GameObject lobbyDataItemPrefab;
    public GameObject lobbyListContent;
    SoundManager soundManager;

    public InputField lobbyIdInputField;
    public Button joinBtn;
    public GameObject noLobbiesText;
    public GameObject lobbyPopupObj;
    public Text popupText;
    public Button popupBtn;

    public List<GameObject> listOfLobbies = new List<GameObject>();

    #region Singleton
    public static LobbiesListManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        soundManager = SoundManager.instance;
        Debug.Log("lobbies awake");
    }
    #endregion

    private void Start()
    {
        joinBtn.onClick.AddListener(() => JoinWithLobbyID());
        popupBtn.onClick.AddListener(() => ClosePopup());
    }

    public void OpenUI()
    {
        lobbiesList.SetActive(true);
        MainManager.instance.OpenedUISet(gameObject);
    }

    public void CloseUI()
    {
        lobbiesList.SetActive(false);
        MainManager.instance.ClosedUISet();
        if(soundManager)
            soundManager.PlayUISFX("ButtonClick");
    }

    public void DisplayLobby(Lobby lobby, FriendProfile profile)
    {
        GameObject createdItem = Instantiate(lobbyDataItemPrefab);
        createdItem.GetComponent<LobbyDataEntry>().SetLobbyData(lobby, profile);
        createdItem.transform.SetParent(lobbyListContent.transform);
        createdItem.transform.localScale = Vector3.one;

        listOfLobbies.Add(createdItem);
    }

    public void NoLobbiesText(bool isLobby)
    {
        noLobbiesText.SetActive(isLobby);
    }

    public void OpenPopup(string message)
    {
        lobbyPopupObj.SetActive(true);
        popupText.text = message;
    }

    public void ClosePopup()
    {
        lobbyPopupObj.SetActive(false);
        popupText.text = string.Empty;
        SteamManager.instance.GetLobbiesList();
    }

    public void DestroyLobbies()
    {
        if (listOfLobbies.Count == 0) return;

        foreach (GameObject lobbyItem in listOfLobbies)
        {
            Destroy(lobbyItem);
        }
        listOfLobbies.Clear();
    }

    public void JoinWithLobbyID()
    {
        ulong Id;
        if (!ulong.TryParse(lobbyIdInputField.text, out Id))
            return;

        SteamManager.instance.JoinLobbyWithID(Id);
    }
}
