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

        /// <summary>Platform name shown when multiplayer requires sign-in.</summary>
        public static string RequiredPlatformLabel
        {
            get
            {
#if UNITY_IOS
                return "Game Center";
#elif UNITY_ANDROID
                return "Google Play Games";
#elif UNITY_EDITOR
                return "Unity Gaming Services";
#else
                return PlatformLabel;
#endif
            }
        }

        /// <summary>True when signed in with Game Center, Play Games, or Editor dev anonymous.</summary>
        public static bool IsAuthorizedPlatformSignIn
        {
            get
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return false;

#if UNITY_EDITOR
                return ActivePlatform == PlatformKind.EditorDev;
#elif UNITY_IOS
                return HasLinkedAppleGameCenterIdentity();
#elif UNITY_ANDROID
                return HasLinkedGooglePlayGamesIdentity();
#else
                return false;
#endif
            }
        }

        /// <summary>Called after a successful platform → UGS sign-in completes.</summary>
        public static void MarkPlatformSignedIn(PlatformKind kind)
        {
            ActivePlatform = kind;
        }

        public static async Task<bool> TrySignInAsync(bool interactive = false)
        {
            LastSignInError = "";

            if (AuthenticationService.Instance.IsSignedIn)
            {
                await RefreshPlayerInfoAsync();
                if (IsAuthorizedPlatformSignIn)
                {
                    ResolveActivePlatformFromBuild();
                    return true;
                }

#if !UNITY_EDITOR
                AuthenticationService.Instance.SignOut(true);
                ActivePlatform = PlatformKind.Unknown;
#endif
            }

#if UNITY_EDITOR
            return await SignInEditorAnonymousAsync();
#elif UNITY_IOS
            ActivePlatform = PlatformKind.AppleGameCenter;
            if (NexusAppleGameCenterSignIn.IsAvailable)
            {
                if (await NexusAppleGameCenterSignIn.TrySignInAsync(interactive))
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
                if (await NexusGooglePlayGamesSignIn.TrySignInAsync(interactive))
                    return true;

                LastSignInError = NexusGooglePlayGamesSignIn.LastError ?? "Google Play Games sign-in failed.";
                return false;
            }

            LastSignInError = NexusGooglePlayGamesSignIn.LastError ??
                              "GPGS not configured — run Nexus → Multiplayer → Google Play Games Setup.";
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
                return "Signing in with Game Center at launch…";
            return "Game Center: install Apple Game Kit + NEXUS_APPLE_GAMEKIT.";
#elif UNITY_ANDROID
            if (AuthenticationService.Instance.IsSignedIn && IsAuthorizedPlatformSignIn)
                return $"Signed in with {PlatformLabel}. Live rooms enabled.";

            return "Sign in with Google Play Games to play online.";
#else
            return "Platform sign-in is not available on this build.";
#endif
        }

        static void ResolveActivePlatformFromBuild()
        {
#if UNITY_EDITOR
            ActivePlatform = PlatformKind.EditorDev;
#elif UNITY_IOS
            if (HasLinkedAppleGameCenterIdentity())
                ActivePlatform = PlatformKind.AppleGameCenter;
#elif UNITY_ANDROID
            if (HasLinkedGooglePlayGamesIdentity())
                ActivePlatform = PlatformKind.GooglePlayGames;
#else
            ActivePlatform = PlatformKind.Unknown;
#endif
        }

        static async Task RefreshPlayerInfoAsync()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                return;

            try
            {
                await AuthenticationService.Instance.GetPlayerInfoAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UGS] GetPlayerInfoAsync failed: {ex.Message}");
            }
        }

#if UNITY_ANDROID
        static bool HasLinkedGooglePlayGamesIdentity()
        {
            var info = AuthenticationService.Instance.PlayerInfo;
            if (info == null)
                return false;

            string id = info.GetGooglePlayGamesId();
            return !string.IsNullOrEmpty(id);
        }
#elif UNITY_IOS
        static bool HasLinkedAppleGameCenterIdentity()
        {
            var info = AuthenticationService.Instance.PlayerInfo;
            if (info == null)
                return false;

            string id = info.GetAppleGameCenterId();
            return !string.IsNullOrEmpty(id);
        }
#endif

#if UNITY_EDITOR
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
                MarkPlatformSignedIn(PlatformKind.EditorDev);
                return true;
            }

            return false;
        }
#endif
    }
}
