using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SUI.Runtime.Editor
{
public static class SuiRuntimeMenu
{
    [MenuItem("GameObject/UI/SUI Runtime Host", false, 10)]
    private static void CreateSuiRuntimeHost(MenuCommand menuCommand)
    {
        var hostObject = new GameObject("SUI Runtime Host");
        GameObjectUtility.SetParentAndAlign(hostObject, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(hostObject, "Create SUI Runtime Host");

        hostObject.AddComponent<SuiRuntimeHost>();

        Selection.activeObject = hostObject;
        EditorSceneManager.MarkSceneDirty(hostObject.scene);
    }
}
}
