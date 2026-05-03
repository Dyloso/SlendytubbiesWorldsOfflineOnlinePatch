using MelonLoader;
using UnityEngine;
using HarmonyLib;
using Il2CppLightReflectiveMirror;
using System;

[assembly: MelonInfo(typeof(OfflineSingleplayer.MyMod), "Offline Singleplayer Patch", "1.0.0", "Tinky Winky")]
[assembly: MelonGame(null, null)]

namespace OfflineSingleplayer
{
    // Forces the game into offline mode by bypassing the relay connection
    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "Start")]
    public static class SWNetworkManagerStartPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            __instance.credentialsReady = true;
            __instance.automaticallyConnect = false;

            MelonLoader.MelonCoroutines.Start(HideOverlayAfterTimeout(__instance));
            MelonLoader.MelonCoroutines.Start(KeepFlagsTrue(__instance));
        }

        static System.Collections.IEnumerator KeepFlagsTrue(Il2Cpp.SWNetworkManager swn)
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (swn == null) yield break;
                if (!swn.connectedToRelay) swn.connectedToRelay = true;
                if (!swn.lobbyServerListReady) swn.lobbyServerListReady = true;
                if (!swn.serverListReady) swn.serverListReady = true;
            }
        }

        static System.Collections.IEnumerator HideOverlayAfterTimeout(Il2Cpp.SWNetworkManager swn)
        {
            yield return new WaitForSeconds(0.5f);
            var loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (loginMenu != null) loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);
            if (overlay != null) overlay.transform.position = new Vector3(-9999f, -9999f, -9999f);
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "OnStartClient")]
    public static class SWNetworkManagerStartClientPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (overlay != null) overlay.transform.position = new Vector3(640f, 360f, 0f);
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SWNetworkManager), "OnClientSceneChanged")]
    public static class OnClientSceneChangedPatch
    {
        static void Postfix(Il2Cpp.SWNetworkManager __instance)
        {
            var overlay = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay");
            if (overlay != null) overlay.transform.position = new Vector3(-9999f, -9999f, -9999f);
        }
    }

    public class MyMod : MelonMod
    {
        private bool _loginMenuHidden = false;
        private static bool _isWarping = false;
        private static int _lastPlayerCount = 0;
        private static string _pendingDestination = "";
        private static bool _listenersAdded = false;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Offline Singleplayer Patch initialized!");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _loginMenuHidden = false;
            _listenersAdded = false;

            if (sceneName == "Menu")
            {
                _isWarping = false;
                _lastPlayerCount = 0;
            }
        }

        public override void OnUpdate()
        {
            // Hide login menu
            if (!_loginMenuHidden && Time.frameCount % 5 == 0)
            {
                GameObject loginMenu = GameObject.Find("NetworkManager(Clone)/ConnectingCanvas/ConnectingOverlay/LoginMenu");
                if (loginMenu != null)
                {
                    loginMenu.transform.position = new Vector3(-9999f, -9999f, -9999f);
                    _loginMenuHidden = true;
                }
            }

            // Add click listeners to fast travel buttons when near helicopter
            if (!_listenersAdded && Time.frameCount % 30 == 0)
            {
                var transport = UnityEngine.Object.FindObjectOfType<Il2Cpp.TeleportTransport>();
                if (transport != null && transport.withinTrigger)
                {
                    var btns = UnityEngine.Object.FindObjectsOfType<Il2Cpp.TeleportTransportButton>();
                    if (btns.Count > 0)
                    {
                        foreach (var b in btns)
                        {
                            var uiBtn = b.gameObject.GetComponent<UnityEngine.UI.Button>();
                            if (uiBtn != null)
                            {
                                string dest = b.locationName;
                                uiBtn.onClick.AddListener(new System.Action(() =>
                                {
                                    _pendingDestination = dest;
                                }));
                            }
                        }
                        _listenersAdded = true;
                    }
                }
                else
                {
                    _listenersAdded = false;
                }
            }

            // Detect warps by monitoring PlayerController count
            if (!_isWarping && Time.frameCount % 10 == 0)
            {
                var players = UnityEngine.Object.FindObjectsOfType<Il2Cpp.PlayerController>();
                bool localPlayerExists = players.Count > 0;

                if (localPlayerExists)
                {
                    _lastPlayerCount = 1;
                }
                else if (!localPlayerExists && _lastPlayerCount == 1)
                {
                    string destinationScene = _pendingDestination;
                    if (string.IsNullOrEmpty(destinationScene))
                    {
                        var zones = UnityEngine.Object.FindObjectsOfType<Il2Cpp.TeleportZone>();
                        foreach (var zone in zones)
                        {
                            if (zone.canTeleport)
                            {
                                destinationScene = zone.sceneName;
                                break;
                            }
                        }
                    }

                    _pendingDestination = "";
                    _lastPlayerCount = 0;
                    _isWarping = true;
                    var swn = UnityEngine.Object.FindObjectOfType<Il2Cpp.SWNetworkManager>();
                    if (swn != null)
                        MelonLoader.MelonCoroutines.Start(ReconnectAfterWarp(swn, destinationScene));
                }
            }
        }

        static System.Collections.IEnumerator ReconnectAfterWarp(Il2Cpp.SWNetworkManager swn, string destinationScene)
        {
            var returnBtn = GameObject.Find("MECHANICS/Canvas/PauseMenu/PauseMenuMain/MenuButtons/ReturnButton");
            if (returnBtn != null)
            {
                var btn = returnBtn.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.Invoke();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }

            Il2Cpp.MainMenu mm = null;
            float elapsed = 0f;
            while (mm == null && elapsed < 10f)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
                mm = UnityEngine.Object.FindObjectOfType<Il2Cpp.MainMenu>();
            }

            if (mm != null && !string.IsNullOrEmpty(destinationScene))
            {
                mm.OpenWorld(destinationScene);
                yield return new WaitForSeconds(0.5f);
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu")
                {
                    var mm2 = UnityEngine.Object.FindObjectOfType<Il2Cpp.MainMenu>();
                    if (mm2 != null) mm2.OpenWorld(destinationScene);
                }
            }

            _isWarping = false;
            _lastPlayerCount = 0;
        }
    }
}
