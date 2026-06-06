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

        public static async Task<bool> TrySignInAsync()
        {
#if UNITY_ANDROID
            LastError = "";
            if (!IsConfigured)
            {
                LastError = "GPGS App ID or Web Client ID missing. Run Google Play Games Setup in Unity.";
                return false;
            }

            return await SignInWithPlayGamesAsync();
#else
            LastError = "Play Games sign-in only runs on Android device builds.";
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_ANDROID
        static Task<bool> SignInWithPlayGamesAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                GooglePlayGames.PlayGamesPlatform.Activate();

                GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        LastError = $"Play Games authenticate failed: {status}. Sign into Play Games on the device.";
                        Debug.LogWarning($"[UGS] {LastError}");
                        tcs.TrySetResult(false);
                        return;
                    }

                    GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, authCode =>
                    {
                        if (string.IsNullOrEmpty(authCode))
                        {
                            LastError =
                                "Play Games auth code empty. Add your keystore SHA-1 to the Android OAuth client in Google Cloud Console.";
                            Debug.LogWarning($"[UGS] {LastError}");
                            tcs.TrySetResult(false);
                            return;
                        }

                        CompleteUgsSignIn(authCode, tcs);
                    });
                });
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning($"[UGS] Play Games sign-in failed: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        static async void CompleteUgsSignIn(string authCode, TaskCompletionSource<bool> tcs)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
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
