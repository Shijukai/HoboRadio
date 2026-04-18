using UnityEngine;
using UnityEditor;

public static class Menu_Shijukai_Hoboradio
{
    //Prefab‚ÌƒpƒX’è‹`
    private const string BASE_PATH = "Packages/com.shijukai.hoboradio/Runtime/Prefab/";

    //Item List Global===========================================
    [MenuItem("GameObject/HoboRadio/Global/Radio_Global", false, 10)]
    static void Create_Global_Radio(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Global.prefab");

    [MenuItem("GameObject/HoboRadio/Global/Radio_Global_UI", false, 11)]
    static void Create_Global_Radio_UI(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Global_UI.prefab");

    [MenuItem("GameObject/HoboRadio/Global/Radio_Global_UI_Lite", false, 12)]
    static void Create_Global_Radio_UI_Lite(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Global_UI_Lite.prefab");

    //Item List Local=============================================
    [MenuItem("GameObject/HoboRadio/Local/Radio_Local", false, 20)]
    static void Create_Local_Radio(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Local.prefab");

    [MenuItem("GameObject/HoboRadio/Local/Radio_Local_UI", false, 21)]
    static void Create_Local_Radio_UI(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Local_UI.prefab");

    [MenuItem("GameObject/HoboRadio/Local/Radio_Local_UI_Lite", false, 22)]
    static void Create_Local_Radio_UI_Lite(MenuCommand cmd)
        => Create(cmd, BASE_PATH + "Radio_Local_UI_Lite.prefab");

    //General processing
    static void Create(MenuCommand cmd, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Prefab not found: " + path);
            return;
        }
        
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        GameObjectUtility.SetParentAndAlign(instance, cmd.context as GameObject);
        Undo.RegisterCreatedObjectUndo(instance, "Create hoboradio Prefab");
        Selection.activeObject = instance;
    }
}
