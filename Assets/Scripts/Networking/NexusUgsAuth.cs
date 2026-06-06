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

        public static event Action OnAuthStateChanged;

        static void SetLastError(string message)
        {
            LastError = message ?? "";
            NexusUgsRunner.RunOnMainThread(() => OnAuthStateChanged?.Invoke());
        }

        public static async Task<bool> EnsureServicesInitializedAsync()
        {
            if (IsServicesInitialized)
                return true;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                IsServicesInitialized = true;
                SetLastError("");
                return true;
            }
            catch (Exception ex)
            {
                IsServicesInitialized = false;
                SetLastError(ex.Message);
                Debug.LogWarning($"[UGS] Initialize failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Try sign-in without throwing; updates <see cref="LastError"/> on failure.</summary>
        public static async Task<bool> TrySignInAsync()
        {
            if (IsSignedIn)
                return true;

            if (!await EnsureServicesInitializedAsync())
                return false;

            try
            {
                if (await NexusPlatformSignIn.TrySignInAsync())
                {
                    SetLastError("");
                    return true;
                }

                SetLastError(NexusPlatformSignIn.LastSignInError ?? $"{PlatformLabel} sign-in failed.");
                return false;
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                Debug.LogWarning($"[UGS] Sign-in failed: {ex.Message}");
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

            if (await TrySignInAsync())
                return true;

            return false;
        }

        public static string MultiplayerStatusLine()
        {
            if (IsReady)
                return NexusPlatformSignIn.MultiplayerStatusLine();

            if (!IsServicesInitialized && !string.IsNullOrEmpty(LastError))
                return $"UGS offline: {LastError}";

            if (!string.IsNullOrEmpty(LastError))
                return $"Not signed in: {LastError}";

            return NexusPlatformSignIn.MultiplayerStatusLine();
        }
    }
}
