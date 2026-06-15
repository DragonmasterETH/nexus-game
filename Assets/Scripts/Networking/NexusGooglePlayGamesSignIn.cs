using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Google Play Games → UGS Authentication (requires GPGS plugin in Assets/GooglePlayGames).
    /// </summary>
    public static class NexusGooglePlayGamesSignIn
    {
        public static string LastError { get; private set; } = "";

        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsConfigured
        {
            get
            {
#if UNITY_ANDROID
                return GooglePlayGames.GameInfo.ApplicationIdInitialized() &&
                       GooglePlayGames.GameInfo.WebClientIdInitialized();
#else
                return false;
#endif
            }
        }

        public static async Task<bool> TrySignInAsync(bool interactive = false)
        {
#if UNITY_ANDROID
            LastError = "";
            if (!IsConfigured)
            {
                LastError = "GPGS App ID or Web Client ID missing. Run Google Play Games Setup in Unity.";
                return false;
            }

            return await SignInWithPlayGamesAsync(interactive);
#else
            LastError = "Play Games sign-in only runs on Android device builds.";
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_ANDROID
        static Task<bool> SignInWithPlayGamesAsync(bool interactive)
        {
            var tcs = new TaskCompletionSource<bool>();
            NexusUgsRunner.EnsureExists();

            void BeginPlayGamesAuth()
            {
                try
                {
                    GooglePlayGames.PlayGamesPlatform.Activate();

                    void onPlayGamesReady(GooglePlayGames.BasicApi.SignInStatus status)
                    {
                        Debug.Log($"[UGS] Play Games authenticate status={status} interactive={interactive}");
                        if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                        {
                            LastError = PlayGamesStatusMessage(status, interactive);
                            Debug.LogWarning($"[UGS] {LastError}");
                            tcs.TrySetResult(false);
                            return;
                        }

                        RequestAuthCodeAndCompleteUgs(tcs);
                    }

                    if (interactive)
                    {
                        Debug.Log("[UGS] ManuallyAuthenticate — opening Play Games sign-in UI");
                        GooglePlayGames.PlayGamesPlatform.Instance.ManuallyAuthenticate(onPlayGamesReady);
                    }
                    else
                    {
                        GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(silentStatus =>
                        {
                            if (silentStatus == GooglePlayGames.BasicApi.SignInStatus.Success)
                                RequestAuthCodeAndCompleteUgs(tcs);
                            else
                                tcs.TrySetResult(false);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Debug.LogWarning($"[UGS] Play Games sign-in failed: {ex.Message}");
                    tcs.TrySetResult(false);
                }
            }

            if (interactive)
            {
                // Dismiss Unity overlays first; fullscreen IMGUI modals break the Play Games activity handoff.
                NexusUgsRunner.Instance.RunDeferred(BeginPlayGamesAuth, 4);
            }
            else
            {
                NexusUgsRunner.RunOnMainThread(BeginPlayGamesAuth);
            }

            return tcs.Task;
        }

        static void RequestAuthCodeAndCompleteUgs(TaskCompletionSource<bool> tcs)
        {
            Debug.Log("[UGS] Play Games authenticated — requesting server auth code");
            GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, authCode =>
            {
                if (string.IsNullOrEmpty(authCode))
                {
                    LastError =
                        "Play Games auth code empty. Add your build keystore SHA-1 to the Android OAuth client " +
                        "(package com.clankergames.nexus) in Google Cloud Console, and enable Google Play Games in Unity Dashboard → Authentication.";
                    Debug.LogWarning($"[UGS] {LastError}");
                    tcs.TrySetResult(false);
                    return;
                }

                CompleteUgsSignIn(authCode, tcs);
            });
        }

        static string PlayGamesStatusMessage(GooglePlayGames.BasicApi.SignInStatus status, bool interactive)
        {
            return status switch
            {
                GooglePlayGames.BasicApi.SignInStatus.Canceled when interactive =>
                    "Play Games did not finish sign-in after Continue. Verify Google Cloud OAuth has the correct " +
                    "SHA-1 for this build's keystore and package com.clankergames.nexus, and that Google Play Games is enabled in Unity Dashboard → Authentication.",
                GooglePlayGames.BasicApi.SignInStatus.Canceled =>
                    "Play Games sign-in required. Tap Try Again to open the sign-in screen.",
                GooglePlayGames.BasicApi.SignInStatus.InternalError =>
                    "Play Games sign-in failed (internal error). Update the Play Games app, check network, and retry.",
                _ => $"Play Games authenticate failed: {status}. Sign into Play Games on the device."
            };
        }

        static async void CompleteUgsSignIn(string authCode, TaskCompletionSource<bool> tcs)
        {
            try
            {
                Debug.Log("[UGS] Signing into Unity Authentication with Play Games auth code");
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
                await AuthenticationService.Instance.GetPlayerInfoAsync();
                NexusPlatformSignIn.MarkPlatformSignedIn(NexusPlatformSignIn.PlatformKind.GooglePlayGames);
                Debug.Log($"[UGS] UGS signed in with Play Games. PlayerId={AuthenticationService.Instance.PlayerId}");
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                LastError =
                    "UGS rejected Play Games token. In Unity Dashboard → Authentication, add Google Play Games with your Web Client ID + secret. " +
                    ex.Message;
                Debug.LogWarning($"[UGS] SignInWithGooglePlayGamesAsync failed: {ex.Message}");
                tcs.TrySetResult(false);
            }
        }
#endif
    }
}
