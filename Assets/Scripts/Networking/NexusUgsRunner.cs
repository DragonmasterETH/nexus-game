using System;
using System.Threading.Tasks;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Runs async UGS work off IMGUI menu threads.</summary>
    public sealed class NexusUgsRunner : MonoBehaviour
    {
        static NexusUgsRunner _instance;

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
