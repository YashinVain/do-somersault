using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpGame.EditorTools
{
    // Opens the pool scene when the editor starts on an empty untitled scene.
    [InitializeOnLoad]
    static class OpenStartScene
    {
        const string SessionKey = "JumpGame.StartSceneChecked";

        static OpenStartScene()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += TryOpen;
        }

        static void TryOpen()
        {
            SessionState.SetBool(SessionKey, true);
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeSceneBuilder.ScenePath) == null) return;

            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
        }
    }
}
