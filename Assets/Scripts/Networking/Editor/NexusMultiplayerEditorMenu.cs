#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NexusGame.Editor
{
    /// <summary>Shortcuts for multiplayer / GPGS setup (GPGS hides its menu unless Android is the active build target).</summary>
    public static class NexusMultiplayerEditorMenu
    {
        [MenuItem("Nexus/Multiplayer/Google Play Games Setup...", false, 100)]
        public static void OpenGooglePlayGamesSetup()
        {
            var windowType = System.Type.GetType(
                "GooglePlayGames.Editor.GPGSAndroidSetupUI, Google.Play.Games.Editor");
            if (windowType == null)
            {
                Debug.LogError(
                    "Google Play Games Editor assembly not found. Import the plugin under Assets/GooglePlayGames.");
                return;
            }

            EditorWindow.GetWindow(windowType, true, "Google Play Games - Android Setup");
        }

        [MenuItem("Nexus/Multiplayer/Switch Build Target to Android", false, 101)]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("Build target is already Android. GPGS menu: Window → Google Play Games → Setup → Android setup...");
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
            Debug.Log("Switched active build target to Android. Use Nexus → Multiplayer → Google Play Games Setup...");
        }
    }
}
#endif
