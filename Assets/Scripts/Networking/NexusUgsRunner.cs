using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Runs async UGS work off IMGUI menu threads.</summary>
    public sealed class NexusUgsRunner : MonoBehaviour
    {
        static NexusUgsRunner _instance;
        readonly Queue<Action> _mainThreadQueue = new();

        public static NexusUgsRunner Instance => _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            var go = new GameObject("NexusUgsRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<NexusUgsRunner>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GooglePlayGames.PlayGamesPlatform.Activate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGS] PlayGamesPlatform.Activate at startup failed: {ex.Message}");
            }
#endif
            _ = WarmUpAsync();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            while (_mainThreadQueue.Count > 0)
                _mainThreadQueue.Dequeue()?.Invoke();

            NexusLobbyService.TickPresence();
        }

        public static void RunOnMainThread(Action action)
        {
            if (action == null)
                return;

            if (_instance == null)
            {
                action();
                return;
            }

            lock (_instance._mainThreadQueue)
                _instance._mainThreadQueue.Enqueue(action);
        }

        /// <summary>Run on main thread after a few frames (lets Unity dismiss overlays before Android UI).</summary>
        public void RunDeferred(Action action, int frameDelay = 3)
        {
            if (action == null)
                return;
            StartCoroutine(DeferredRoutine(action, Mathf.Max(1, frameDelay)));
        }

        static IEnumerator DeferredRoutine(Action action, int frameDelay)
        {
            for (int i = 0; i < frameDelay; i++)
                yield return null;
            action?.Invoke();
        }

        static async Task WarmUpAsync()
        {
            await NexusUgsAuth.EnsureServicesInitializedAsync();

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (_instance == null)
                return;

            _instance.Run(async () =>
            {
                bool ok = await NexusUgsAuth.TrySignInAsync(interactive: false);
                if (ok)
                    Debug.Log($"[UGS] Platform sign-in at launch ({NexusPlatformSignIn.PlatformLabel}).");
                else
                    Debug.Log("[UGS] Platform sign-in at launch skipped or failed; multiplayer will retry.");
            });
#endif
        }

        public void Run(Func<Task> work)
        {
            if (work == null)
                return;
            _ = RunSafe(work);
        }

        static async Task RunSafe(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                NexusLobbyService.ReportAsyncFailure(ex);
            }
        }
    }
}
