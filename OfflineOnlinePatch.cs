using MelonLoader;
using UnityEngine;
using HarmonyLib;
using Il2CppLightReflectiveMirror;
using System;

[assembly: MelonInfo(typeof(OfflineOnlinePatch.MyMod), "Offline+Online Patch", "1.2.0", "Tinky Winky")]
[assembly: MelonGame(null, null)]

namespace OfflineOnlinePatch
{
    public static class ModPrefs
    {
        private static string _configPath;
        private static string _relayIP = "127.0.0.1";

        public static void Init()
        {
            _configPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "UserData", "OfflineOnlinePatch.cfg");

            if (System.IO.File.Exists(_configPath))
            {
                foreach (var line in System.IO.File.ReadAllLines(_configPath))
                {
                    if (line.StartsWith("RelayIP="))
                        _relayIP = line.Substring("RelayIP=".Length).Trim();
                }
                MelonLogger.Msg("Config loaded! RelayIP: " + _relayIP);
            }
            else
            {
                System.IO.File.WriteAllText(_configPath, "RelayIP=127.0.0.1" + System.Environment.NewLine);
                MelonLogger.Msg("Config created! RelayIP: " + _relayIP);
            }
        }

        public static string RelayIP => _relayIP;
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "Start")]
    public static class SWNetworkManagerStartPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            __instance.credentialsReady = true;
            __instance.automaticallyConnect = false;
            MelonLogger.Msg("SWNetworkManager credentials forced!");

            var lrm = __instance.GetComponent<LightReflectiveMirrorTransport>();
            if (lrm != null)
            {
                MelonLogger.Msg("Connecting to relay: " + ModPrefs.RelayIP);
                lrm.serverIP = ModPrefs.RelayIP;
                lrm.serverPort = 7778;
                lrm.ConnectToRelay();
            }
            else
            {
                MelonLogger.Msg("LRM not found on SWNetworkManager!");
            }

            MelonLoader.MelonCoroutines.Start(HideOverlayAfterTimeout(__instance));
            MelonLoader.MelonCoroutines.Start(KeepConnectedToRelayTrue(__instance));
        }

        static System.Collections.IEnumerator KeepConnectedToRelayTrue(Il2Cpp.SWNetworkManager swn)
        {
            // Keep connectedToRelay and lobbyServerListReady true so all
            // game modes are always accessible even when relay is offline
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (!swn.connectedToRelay)
                    swn.connectedToRelay = true;
                if (!swn.lobbyServerListReady)
                    swn.lobbyServerListReady = true;
                if (!swn.serverListReady)
                    swn.serverListReady = true;
            }
        }

        static System.Collections.IEnumerator HideOverlayAfterTimeout(Il2Cpp.SWNetworkManager swn)
        {
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;

                var lrm = swn.GetComponent<LightReflectiveMirrorTransport>();
                if (lrm != null && lrm._connectedToRelay)
                {
                    MelonLogger.Msg("Relay connected, overlay will hide naturally!");
                    yield break;
                }
            }

            MelonLogger.Msg("Relay connection timed out, hiding overlay anyway!");
            var loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (loginMenu != null) loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);
            if (overlay != null) overlay.transform.position = new Vector3(-9999f, -9999f, -9999f);
        }
    }

    [HarmonyPatch(typeof(LightReflectiveMirrorTransport), "OnConnectedToRelay")]
    public static class OnConnectedToRelayPatch
    {
        static void Postfix(LightReflectiveMirrorTransport __instance)
        {
            MelonLogger.Msg("LRM OnConnectedToRelay fired!");

            var lobbyGo = GameObject.Find("MainMenuMechanics/MENU/LobbyCanvas/LobbyParent/LOBBY");
            if (lobbyGo != null)
            {
                MelonLogger.Msg("Found existing LOBBY in scene!");
            }
            else
            {
                MelonLogger.Msg("Existing LOBBY not found, spawning from prefab...");
                var prefabs = Resources.FindObjectsOfTypeAll<Il2Cpp.CustomLobbySystem>();
                if (prefabs != null && prefabs.Count > 0)
                {
                    UnityEngine.Object.Instantiate(prefabs[0]);
                    MelonLogger.Msg("CustomLobbySystem spawned from prefab!");
                }
                else
                {
                    var go = new GameObject("CustomLobbySystem");
                    go.AddComponent<Il2Cpp.CustomLobbySystem>();
                    MelonLogger.Msg("CustomLobbySystem created fresh!");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "OnStartClient")]
    public static class SWNetworkManagerStartClientPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            MelonLogger.Msg("OnStartClient called, restoring overlay for splash!");

            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (overlay != null)
            {
                overlay.transform.position = new Vector3(640f, 360f, 0f);
                MelonLogger.Msg("ConnectingOverlay restored for join splash!");
            }

            var lrm = __instance.GetComponent<LightReflectiveMirrorTransport>();
            if (lrm != null && lrm._isServer)
            {
                MelonLogger.Msg("We are the server, checking relay for existing world...");
                MelonLoader.MelonCoroutines.Start(CheckAndRegisterWorld(lrm, __instance));
            }
        }

        static System.Collections.IEnumerator CheckAndRegisterWorld(LightReflectiveMirrorTransport lrm, Il2Cpp.SWNetworkManager swn)
        {
            // If not connected to relay, just play solo — no registration needed
            if (!lrm._connectedToRelay)
            {
                MelonLogger.Msg("Not connected to relay, playing Open World solo!");
                yield break;
            }

            // Request server list and wait for it to populate
            lrm.RequestServerList();
            yield return new WaitForSeconds(1f);

            // Check if a matching world already exists on the relay
            string currentData = lrm.extraServerData;
            bool existingWorldFound = false;

            foreach (var room in lrm.relayServerList)
            {
                if (room.serverData == currentData)
                {
                    existingWorldFound = true;
                    break;
                }
            }

            if (!existingWorldFound)
            {
                // No existing world found, register this one with the relay
                MelonLoader.MelonCoroutines.Start(LRMServerStartPatch.SendCreateRoom(lrm));
            }
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "OnDisconnectedFromRelay")]
    public static class SWNetworkManagerDisconnectPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            MelonLogger.Msg("SWNetworkManager OnDisconnectedFromRelay - hiding overlay!");

            var loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
            if (loginMenu != null) loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);

            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (overlay != null) overlay.transform.position = new Vector3(-9999f, -9999f, -9999f);
        }
    }

    [HarmonyPatch(typeof(LightReflectiveMirrorTransport), "OnDisconnectedFromRelay")]
    public static class OnDisconnectedFromRelayPatch
    {
        static void Postfix(LightReflectiveMirrorTransport __instance)
        {
            MelonLogger.Msg("Disconnected from relay! Attempting reconnect...");
            MelonLoader.MelonCoroutines.Start(ReconnectToRelay(__instance));
        }

        static System.Collections.IEnumerator ReconnectToRelay(LightReflectiveMirrorTransport lrm)
        {
            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            var loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
            if (overlay != null) overlay.transform.position = new Vector3(-9999f, -9999f, -9999f);
            if (loginMenu != null) loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);
            MelonLogger.Msg("Overlay hidden after disconnect!");

            int attempts = 0;
            while (attempts < 5)
            {
                yield return new WaitForSeconds(3f);
                attempts++;

                if (lrm._connectedToRelay)
                {
                    MelonLogger.Msg("Reconnected to relay!");
                    yield break;
                }

                MelonLogger.Msg("Reconnect attempt " + attempts + " of 5...");
                try
                {
                    lrm.serverIP = ModPrefs.RelayIP;
                    lrm.serverPort = 7778;
                    lrm.ConnectToRelay();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Reconnect error: " + e.Message);
                }
            }

            MelonLogger.Msg("Could not reconnect to relay after 5 attempts.");
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "OnStartHost")]
    public static class OnStartHostPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            MelonLogger.Msg("OnStartHost called!");
            var lrm = __instance.GetComponent<LightReflectiveMirrorTransport>();
            if (lrm != null)
            {
                MelonLogger.Msg("Sending CreateRoom from OnStartHost...");
                MelonLoader.MelonCoroutines.Start(LRMServerStartPatch.SendCreateRoom(lrm));
            }
        }
    }

    [HarmonyPatch(typeof(LightReflectiveMirrorTransport), "ServerStart")]
    public static class LRMServerStartPatch
    {
        static void Postfix(LightReflectiveMirrorTransport __instance)
        {
            MelonLogger.Msg("LRM ServerStart called!");

            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (overlay != null)
            {
                overlay.transform.position = new Vector3(640f, 360f, 0f);
                MelonLogger.Msg("ConnectingOverlay restored for game load splash!");
            }

            MelonLoader.MelonCoroutines.Start(SendCreateRoom(__instance));
        }

        public static System.Collections.IEnumerator SendCreateRoom(LightReflectiveMirrorTransport lrm)
        {
            yield return null;

            try
            {
                int position = 0;
                byte[] buffer = new byte[8192];

                buffer[position++] = 7;
                WriteInt(buffer, ref position, lrm.maxServerPlayers);
                WriteString(buffer, ref position, lrm.serverName);
                buffer[position++] = lrm.isPublicServer ? (byte)1 : (byte)0;
                WriteString(buffer, ref position, lrm.extraServerData);
                buffer[position++] = 0;
                WriteString(buffer, ref position, "");
                buffer[position++] = 0;
                WriteInt(buffer, ref position, lrm.serverPort);

                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> il2cppBuffer = buffer;
                var segment = new Il2CppSystem.ArraySegment<byte>(il2cppBuffer, 0, position);
                lrm.clientToServerTransport.ClientSend(segment, 0);

                MelonLogger.Msg("CreateRoom message sent! Length: " + position);
            }
            catch (Exception e)
            {
                MelonLogger.Msg("Error sending CreateRoom: " + e.Message);
            }
        }

        static void WriteInt(byte[] buffer, ref int position, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            buffer[position++] = bytes[0];
            buffer[position++] = bytes[1];
            buffer[position++] = bytes[2];
            buffer[position++] = bytes[3];
        }

        static void WriteString(byte[] buffer, ref int position, string value)
        {
            if (value == null) value = "";
            WriteInt(buffer, ref position, value.Length);
            foreach (char c in value)
            {
                byte[] charBytes = BitConverter.GetBytes(c);
                buffer[position++] = charBytes[0];
                buffer[position++] = charBytes[1];
            }
        }
    }

    public class MyMod : MelonMod
    {
        private bool _loginMenuHidden = false;

        public override void OnInitializeMelon()
        {
            ModPrefs.Init();
            MelonLogger.Msg("OfflinePatch initialized! Relay IP: " + ModPrefs.RelayIP);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _loginMenuHidden = false;
        }

        public override void OnUpdate()
        {
            if (!_loginMenuHidden)
            {
                if (Time.frameCount % 5 == 0)
                {
                    GameObject loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
                    if (loginMenu != null)
                    {
                        var lrm = UnityEngine.Object.FindObjectOfType<LightReflectiveMirrorTransport>();
                        bool isConnected = lrm != null && lrm._connectedToRelay;

                        if (!isConnected)
                        {
                            loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);
                            _loginMenuHidden = true;
                            MelonLogger.Msg("LoginMenu hidden (not connected to relay)!");
                        }
                    }
                }
            }
        }
    }
}
