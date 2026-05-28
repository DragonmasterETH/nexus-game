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
        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static async Task<bool> TrySignInAsync()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            return await SignInWithPlayGamesAsync();
#else
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_ANDROID || UNITY_EDITOR
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
                        Debug.LogWarning($"[UGS] Google Play Games authenticate failed: {status}");
                        tcs.TrySetResult(false);
                        return;
                    }

                    GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, authCode =>
                    {
                        if (string.IsNullOrEmpty(authCode))
                        {
                            Debug.LogWarning("[UGS] Google Play Games auth code was empty.");
                            tcs.TrySetResult(false);
                            return;
                        }

                        CompleteUgsSignIn(authCode, tcs);
                    });
                });
            }
            catch (Exception ex)
            {
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
                Debug.LogWarning($"[UGS] SignInWithGooglePlayGamesAsync failed: {ex.Message}");
                tcs.TrySetResult(false);
            }
        }
#endif
    }
}
