using Unity.Netcode;
using UnityEngine;

public class DevMenu : MonoBehaviour
{
    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm.IsClient || nm.IsServer) return;
        if (GUILayout.Button("Host")) nm.StartHost();
        if (GUILayout.Button("Client")) nm.StartClient();
        if (GUILayout.Button("Server")) nm.StartServer();
    }
}