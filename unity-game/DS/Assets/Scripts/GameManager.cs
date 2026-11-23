using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public Transform[] spawnPoints;
    public bool MatchOver;                                   // server-side only
    public bool MatchActive => IsServer && NetworkManager.ConnectedClientsIds.Count == 2 && !MatchOver;

    NetworkVariable<ulong> winnerId = new(ulong.MaxValue);   // MaxValue = no winner yet

    void Awake() { Instance = this; }

    public override void OnNetworkSpawn()
    {
        if (IsServer) NetworkManager.OnClientConnectedCallback += OnConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
    }

    void OnConnected(ulong id)
    {
        int count = NetworkManager.ConnectedClientsIds.Count;
        if (count > 2) { NetworkManager.DisconnectClient(id); return; }   // 2 players max

        var player = NetworkManager.ConnectedClients[id].PlayerObject;
        Vector3 pos = spawnPoints[count - 1].position;
        player.transform.position = pos;
        player.GetComponent<NetworkTransform>().Teleport(pos, Quaternion.identity, player.transform.localScale);
    }

    public void EndMatch(ulong loserId)
    {
        if (MatchOver) return;
        MatchOver = true;
        foreach (var id in NetworkManager.ConnectedClientsIds)
            if (id != loserId) winnerId.Value = id;
    }

    void OnGUI()
    {
        if (winnerId.Value == ulong.MaxValue) return;
        string msg = winnerId.Value == NetworkManager.LocalClientId ? "YOU WIN" : "YOU LOSE";
        GUI.Label(new Rect(Screen.width / 2 - 50, 50, 200, 30), msg);
    }
}