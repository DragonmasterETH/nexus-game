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
            AppleGameCenter,
            GooglePlayGames
        }

        public static PlatformKind ActivePlatform { get; private set; } = PlatformKind.Unknown;

        public static string PlatformLabel => ActivePlatform switch
        {
            PlatformKind.AppleGameCenter => "Game Center",
            PlatformKind.GooglePlayGames => "Google Play Games",
            PlatformKind.EditorDev => "Editor (dev)",
            _ => "platform account"
        };

        public static async Task<bool> TrySignInAsync()
        {
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
                return await NexusAppleGameCenterSignIn.TrySignInAsync();
            Debug.LogWarning("[UGS] Install Apple Game Kit and add scripting define NEXUS_APPLE_GAMEKIT (see AUTH_SETUP.md).");
            return false;
#elif UNITY_ANDROID
            ActivePlatform = PlatformKind.GooglePlayGames;
            if (NexusGooglePlayGamesSignIn.IsAvailable)
                return await NexusGooglePlayGamesSignIn.TrySignInAsync();
            Debug.LogWarning("[UGS] Import Google Play Games plugin (Assets/GooglePlayGames).");
            return false;
#else
            Debug.LogWarning("[UGS] Platform sign-in is not configured for this build target.");
            return false;
#endif
        }

        public static string MultiplayerStatusLine()
        {
#if UNITY_EDITOR
            if (AuthenticationService.Instance.IsSignedIn)
                return $"Signed in for dev testing ({PlatformLabel}).";
            return "Editor: link UGS in Project Settings, or use offline stub rooms.";
#elif UNITY_IOS
            if (AuthenticationService.Instance.IsSignedIn)
                return $"Signed in with {PlatformLabel}.";
            if (NexusAppleGameCenterSignIn.IsAvailable)
                return "Tap Multiplayer to sign in with Game Center.";
            return "Game Center: install Apple Game Kit + NEXUS_APPLE_GAMEKIT (see AUTH_SETUP.md).";
#elif UNITY_ANDROID
            if (AuthenticationService.Instance.IsSignedIn)
                return $"Signed in with {PlatformLabel}.";
            if (NexusGooglePlayGamesSignIn.IsAvailable)
                return "Tap Multiplayer to sign in with Google Play Games.";
            return "Play Games: import GPGS plugin and run Nexus → Multiplayer → Google Play Games Setup.";
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
            ActivePlatform = PlatformKind.GooglePlayGames;
#else
            ActivePlatform = PlatformKind.Unknown;
#endif
        }

#if UNITY_EDITOR
        static async Task<bool> SignInEditorAnonymousAsync()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                ActivePlatform = PlatformKind.EditorDev;
                Debug.Log($"[UGS] Editor dev sign-in (anonymous). PlayerId={AuthenticationService.Instance.PlayerId}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UGS] Editor anonymous sign-in failed: {ex.Message}");
                return false;
            }
        }
#endif
    }
}
