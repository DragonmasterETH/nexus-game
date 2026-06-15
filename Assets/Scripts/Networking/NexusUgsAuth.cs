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

        /// <summary>True when signed in with Game Center, Play Games, or Editor dev UGS.</summary>
        public static bool IsMultiplayerAuthorized =>
            IsReady && NexusPlatformSignIn.IsAuthorizedPlatformSignIn;
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
        public static async Task<bool> TrySignInAsync(bool interactive = false)
        {
            if (IsMultiplayerAuthorized)
                return true;

            if (!await EnsureServicesInitializedAsync())
                return false;

            if (IsSignedIn && !NexusPlatformSignIn.IsAuthorizedPlatformSignIn)
            {
                AuthenticationService.Instance.SignOut(true);
                NexusPlatformSignIn.MarkPlatformSignedIn(NexusPlatformSignIn.PlatformKind.Unknown);
            }

            try
            {
                if (await NexusPlatformSignIn.TrySignInAsync(interactive))
                {
                    SetLastError("");
                    NexusUgsRunner.RunOnMainThread(() => OnAuthStateChanged?.Invoke());
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
            if (IsMultiplayerAuthorized)
                return true;

            if (!await EnsureServicesInitializedAsync())
                return false;

            if (await TrySignInAsync())
                return true;

            return false;
        }

        public static string MultiplayerStatusLine()
        {
            if (IsMultiplayerAuthorized)
                return NexusPlatformSignIn.MultiplayerStatusLine();

            if (!IsServicesInitialized && !string.IsNullOrEmpty(LastError))
                return $"Sign-in unavailable: {LastError}";

            if (!string.IsNullOrEmpty(LastError))
                return $"Not signed in: {LastError}";

            return $"Sign in with {NexusPlatformSignIn.RequiredPlatformLabel} to play online.";
        }
    }
}
