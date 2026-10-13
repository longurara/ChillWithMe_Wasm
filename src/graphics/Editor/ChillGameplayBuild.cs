using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hlc.ChillPrototype;

public static class ChillGameplayBuild
{
    public static void Build()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        var ui=player.GetComponent<ChillGameplayUI>() ?? player.gameObject.AddComponent<ChillGameplayUI>(); ui.player=player;
        if (!player.story || !player.library || !player.music || !player.environment) throw new InvalidOperationException("Gameplay UI dependencies missing");
        var snapshot=ui.Snapshot();
        if (snapshot.story == null || snapshot.mode < 0 || snapshot.mode > 2) throw new InvalidOperationException("Gameplay UI snapshot invalid");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("HLC_CHILL_GAMEPLAY_UI_READY receiver="+player.gameObject.name);
        ChillBuild.BuildWebGL();
    }
}
