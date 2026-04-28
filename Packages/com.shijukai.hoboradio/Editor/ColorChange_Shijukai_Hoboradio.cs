using UnityEngine;
using UnityEditor;
using System.Linq;

public class Window_Shijukai_Hoboradio_ColorChange : EditorWindow 
{
    private GameObject rootObject;

    //Preset
    private GameObject[] presetPrefabs;
    private string[] presetNames;
    private int selectedIndex = 0;

    //Manual select Prefab
    private GameObject manualPrefab;

    private const string PRESET_PATH = "Packages/com.shijukai.hoboradio/Runtime/ColorOptions";

    //main script
    [MenuItem("Tools/Shijukai/Hoboradio_ColorChange")]
    static void Open()
    {
        GetWindow<Window_Shijukai_Hoboradio_ColorChange>("Hoboradio_ColorChange");
    }
    void OnEnable()
    {
        LoadPresets();
    }

    void LoadPresets()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PRESET_PATH });

        presetPrefabs = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .Where(p  => p != null)
            .ToArray();

        presetNames = presetPrefabs
            .Select(p => p.name)
            .ToArray();

    }
    private void OnGUI()
    {
        GUILayout.Label("ほぼらじお カラー変更ツール",EditorStyles.boldLabel);

        rootObject = (GameObject)EditorGUILayout.ObjectField(
            "Radio Root",
            rootObject,
            typeof(GameObject),
            true
        );

        GUILayout.Space(10);

        //プリセット選択
        GUILayout.Label("プリセット選択", EditorStyles.boldLabel);

        if(presetNames != null && presetNames.Length > 0)
        {
            selectedIndex = EditorGUILayout.Popup("Preset",selectedIndex, presetNames);
        }
        else
        {
            EditorGUILayout.HelpBox("プリセットが見つかりません", MessageType.Warning);
        }

        GUILayout.Space(10);

        //限定アセット指定
        GUILayout.Label("または手動指定", EditorStyles.boldLabel);

        manualPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab",
            manualPrefab,
            typeof(GameObject),
            false
        );

        GUILayout.Space(10);

        if(GUILayout.Button("置き換え実行"))
        {
            Execute();
        }
    }

    void Execute()
    {
        if (rootObject == null)
        {
            EditorGUILayout.HelpBox("対象を指定してください", MessageType.Warning);
            Debug.LogError("対象（Radio Root）が未指定です");
            return;
        }
        
        GameObject prefabToUse = null;

        //アセット指定を優先
        if (manualPrefab != null)
        {
            prefabToUse = manualPrefab;
        }
        else if (presetPrefabs != null && presetPrefabs.Length > 0)
        {
            prefabToUse = presetPrefabs[selectedIndex];
        }

        if (prefabToUse == null)
        {
            EditorGUILayout.HelpBox("Prefabが選択されていません", MessageType.Warning);
            Debug.LogError("Prefabが選択されていません");
            return;
        }

        Transform target = rootObject.transform.Find("Radio_Root/Radio_Mesh");

        if(target == null)
        {
            EditorGUILayout.HelpBox("Radio_Root/Radio_Mesh が見つかりません", MessageType.Error);
            Debug.LogError("Radio_Root/Radio_Mesh が見つかりません");
            return;
        }

        Replace(target.gameObject, prefabToUse);
    }

    void Replace(GameObject original, GameObject prefab)
    {
        Transform parent = original.transform.parent;
        int siblingIndex = original.transform.GetSiblingIndex();

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Radio_Mesh";

        Transform t = instance.transform;
        t.SetParent(parent);
        t.localPosition = original.transform.localPosition;
        t.localRotation = original.transform.localRotation;
        t.localScale = original.transform.localScale;
        t.SetSiblingIndex(siblingIndex);

        Undo.RegisterCreatedObjectUndo(instance, "Hoboradio change color");
        Undo.DestroyObjectImmediate(original);
    }
}
