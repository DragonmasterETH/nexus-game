using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Apple Game Center → UGS Authentication.
    /// Enable after installing Apple Game Kit: add scripting define <c>NEXUS_APPLE_GAMEKIT</c> for iOS.
    /// </summary>
    public static class NexusAppleGameCenterSignIn
    {
        public static string LastError { get; private set; } = "";

        public static bool IsAvailable
        {
            get
            {
#if UNITY_IOS && NEXUS_APPLE_GAMEKIT
                return true;
#else
                return false;
#endif
            }
        }

        public static async Task<bool> TrySignInAsync()
        {
#if UNITY_IOS && NEXUS_APPLE_GAMEKIT
            return await SignInWithGameKitAsync();
#else
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_IOS && NEXUS_APPLE_GAMEKIT
        static async Task<bool> SignInWithGameKitAsync()
        {
            try
            {
                // Apple.GameKit — install from https://github.com/apple/unityplugins (GameKit package)
                var localPlayer = Apple.GameKit.GKLocalPlayer.Local;
                if (!localPlayer.IsAuthenticated)
                {
                    var player = await Apple.GameKit.GKLocalPlayer.Authenticate();
                    Debug.Log($"[UGS] GameKit authenticated: {player?.DisplayName}");
                    localPlayer = Apple.GameKit.GKLocalPlayer.Local;
                }

                var fetchItems = await localPlayer.FetchItems();
                string signature = Convert.ToBase64String(fetchItems.GetSignature());
                string teamPlayerId = localPlayer.TeamPlayerId;
                string salt = Convert.ToBase64String(fetchItems.GetSalt());
                string publicKeyUrl = fetchItems.PublicKeyUrl;
                ulong timestamp = (ulong)fetchItems.Timestamp;

                await AuthenticationService.Instance.SignInWithAppleGameCenterAsync(
                    signature, teamPlayerId, publicKeyUrl, salt, timestamp);

                Debug.Log($"[UGS] UGS signed in with Game Center. PlayerId={AuthenticationService.Instance.PlayerId}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning($"[UGS] Game Center sign-in failed: {ex.Message}");
                return false;
            }
        }
#endif
    }
}
