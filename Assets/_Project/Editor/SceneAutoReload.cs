using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace JumpGame.EditorTools
{
    // When the open scene file is rewritten from outside (scene builder run in another editor, git pull),
    // reload it instead of keeping the stale copy in memory. Our own saves and unsaved edits are left alone.
    class SceneAutoReload : AssetPostprocessor
    {
        static double lastSave;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorSceneManager.sceneSaved += _ => lastSave = EditorApplication.timeSinceStartup;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) || active.isDirty) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup - lastSave < 3.0) return;

            foreach (var path in imported)
            {
                if (path != active.path) continue;
                string scenePath = path;
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scenePath);
                return;
            }
        }
    }
}
