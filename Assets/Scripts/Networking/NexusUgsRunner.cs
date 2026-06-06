using System;
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

        static async Task WarmUpAsync()
        {
            await NexusUgsAuth.EnsureServicesInitializedAsync();
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
