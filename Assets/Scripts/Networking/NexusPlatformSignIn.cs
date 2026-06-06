using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Platform account sign-in for UGS. Production: Game Center (iOS) / Play Games (Android).
    /// </summary>
    public static class NexusPlatformSignIn
    {
        public enum PlatformKind
        {
            Unknown,
            EditorDev,
            AnonymousFallback,
            AppleGameCenter,
            GooglePlayGames
        }

        public static PlatformKind ActivePlatform { get; private set; } = PlatformKind.Unknown;

        /// <summary>Most recent sign-in failure (shown in multiplayer UI).</summary>
        public static string LastSignInError { get; private set; } = "";

        public static string PlatformLabel => ActivePlatform switch
        {
            PlatformKind.AppleGameCenter => "Game Center",
            PlatformKind.GooglePlayGames => "Google Play Games",
            PlatformKind.AnonymousFallback => "UGS (anonymous)",
            PlatformKind.EditorDev => "Editor (dev)",
            _ => "platform account"
        };

        public static async Task<bool> TrySignInAsync()
        {
            LastSignInError = "";

            if (AuthenticationService.Instance.IsSignedIn)
            {
                ResolveActivePlatformFromBuild();
                return true;
            }

#if UNITY_EDITOR
            return await SignInEditorAnonymousAsync();
#elif UNITY_IOS
            ActivePlatform = PlatformKind.AppleGameCenter;
            if (NexusAppleGameCenterSignIn.IsAvailable)
            {
                if (await NexusAppleGameCenterSignIn.TrySignInAsync())
                    return true;
                LastSignInError = NexusAppleGameCenterSignIn.LastError ?? "Game Center sign-in failed.";
                return false;
            }

            LastSignInError = "Install Apple Game Kit + NEXUS_APPLE_GAMEKIT (see AUTH_SETUP.md).";
            return false;
#elif UNITY_ANDROID
            ActivePlatform = PlatformKind.GooglePlayGames;
            if (NexusGooglePlayGamesSignIn.IsConfigured && NexusGooglePlayGamesSignIn.IsAvailable)
            {
                if (await NexusGooglePlayGamesSignIn.TrySignInAsync())
                    return true;

                LastSignInError = NexusGooglePlayGamesSignIn.LastError ?? "Google Play Games sign-in failed.";
                Debug.LogWarning($"[UGS] Play Games failed: {LastSignInError}. Trying anonymous UGS sign-in…");
            }
            else
            {
                LastSignInError = NexusGooglePlayGamesSignIn.LastError ??
                                  "GPGS not configured — run Nexus → Multiplayer → Google Play Games Setup.";
                Debug.LogWarning($"[UGS] {LastSignInError} Trying anonymous UGS sign-in…");
            }

            if (await SignInAnonymousAsync())
            {
                ActivePlatform = PlatformKind.AnonymousFallback;
                return true;
            }

            return false;
#else
            LastSignInError = "Platform sign-in is not configured for this build target.";
            return false;
#endif
        }

        public static string MultiplayerStatusLine()
        {
#if UNITY_EDITOR
            if (AuthenticationService.Instance.IsSignedIn)
                return $"Signed in ({PlatformLabel}). Live rooms enabled.";
            return "Editor: link UGS in Project Settings → Services, then open Multiplayer.";
#elif UNITY_IOS
            if (AuthenticationService.Instance.IsSignedIn)
                return $"Signed in with {PlatformLabel}. Live rooms enabled.";
            if (NexusAppleGameCenterSignIn.IsAvailable)
                return "Open Multiplayer to sign in with Game Center.";
            return "Game Center: install Apple Game Kit + NEXUS_APPLE_GAMEKIT.";
#elif UNITY_ANDROID
            if (AuthenticationService.Instance.IsSignedIn)
            {
                if (ActivePlatform == PlatformKind.AnonymousFallback)
                    return "Signed in (anonymous dev). Enable Play Games in UGS dashboard for production.";
                return $"Signed in with {PlatformLabel}. Live rooms enabled.";
            }

            return "Open Multiplayer to sign in (Play Games or anonymous fallback).";
#else
            return "Platform sign-in is not available on this build.";
#endif
        }

        static void ResolveActivePlatformFromBuild()
        {
#if UNITY_EDITOR
            ActivePlatform = PlatformKind.EditorDev;
#elif UNITY_IOS
            ActivePlatform = PlatformKind.AppleGameCenter;
#elif UNITY_ANDROID
            if (ActivePlatform == PlatformKind.Unknown)
                ActivePlatform = PlatformKind.GooglePlayGames;
#else
            ActivePlatform = PlatformKind.Unknown;
#endif
        }

#if UNITY_EDITOR || UNITY_ANDROID
        static async Task<bool> SignInAnonymousAsync()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[UGS] Anonymous sign-in. PlayerId={AuthenticationService.Instance.PlayerId}");
                return true;
            }
            catch (System.Exception ex)
            {
                LastSignInError = string.IsNullOrEmpty(LastSignInError)
                    ? $"Anonymous sign-in failed: {ex.Message}"
                    : LastSignInError + "\nAnonymous fallback also failed: " + ex.Message;
                Debug.LogWarning($"[UGS] Anonymous sign-in failed: {ex.Message}");
                return false;
            }
        }
#endif

#if UNITY_EDITOR
        static async Task<bool> SignInEditorAnonymousAsync()
        {
            if (await SignInAnonymousAsync())
            {
                ActivePlatform = PlatformKind.EditorDev;
                return true;
            }

            return false;
        }
#endif
    }
}
