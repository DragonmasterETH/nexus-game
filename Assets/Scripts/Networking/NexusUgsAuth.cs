using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace NexusGame
{
    /// <summary>UGS init + platform sign-in (Game Center / Play Games; Editor uses anonymous for dev).</summary>
    public static class NexusUgsAuth
    {
        public static bool IsServicesInitialized { get; private set; }
        public static bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public static bool IsReady => IsServicesInitialized && IsSignedIn;
        public static string LastError { get; private set; } = "";
        public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
        public static string PlatformLabel => NexusPlatformSignIn.PlatformLabel;

        public static async Task<bool> EnsureServicesInitializedAsync()
        {
            if (IsServicesInitialized)
                return true;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                IsServicesInitialized = true;
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                IsServicesInitialized = false;
                LastError = ex.Message;
                Debug.LogWarning($"[UGS] Initialize failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Initialize UGS (if needed) and sign in with the platform account for this build.</summary>
        public static async Task<bool> EnsureReadyAsync()
        {
            if (IsReady)
                return true;

            if (!await EnsureServicesInitializedAsync())
                return false;

            try
            {
                if (await NexusPlatformSignIn.TrySignInAsync())
                {
                    LastError = "";
                    return true;
                }

                LastError = $"{PlatformLabel} sign-in not available yet.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning($"[UGS] Sign-in failed: {ex.Message}");
                return false;
            }
        }

        public static string MultiplayerStatusLine()
        {
            if (!IsServicesInitialized && !string.IsNullOrEmpty(LastError))
                return $"UGS offline ({LastError}). Stub rooms still work.";

            return NexusPlatformSignIn.MultiplayerStatusLine();
        }
    }
}
