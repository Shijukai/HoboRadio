using UnityEngine;
using UnityEditor;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;

public class Window_Shijukai_Hoboradio_ColorChange : EditorWindow 
{
    private GameObject rootObject;

    //Preset
    private GameObject[] presetPrefabs;
    private string[] presetNames;
    private int selectedIndex = 0;

    //Manual select Prefab
    private GameObject manualPrefab;

    // --- フォルダのGUID ---
    private const string GUID_PRESET_FOLDER = "328e0b48beab9664c9b4a8f52108e56b";

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
        string currentPresetPath = AssetDatabase.GUIDToAssetPath(GUID_PRESET_FOLDER);

        if (string.IsNullOrEmpty(currentPresetPath))
        {
            Debug.LogWarning("[HoboRadio] Preset folder not found via GUID.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { currentPresetPath });

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

        Transform newSliderKnob = instance.transform.Find("Armature/Radio_Root/Slider");

        if (newSliderKnob == null)
        {
            Debug.LogError($"[HoboRadio] 新しいプレハブ内にスライダーボーンが見つかりません。パスを確認してください: Armature/Radio_Root/Slider");
        }

        Animator newAnimator = instance.GetComponent<Animator>();

        Undo.DestroyObjectImmediate(original);

        if (rootObject != null)
        {
            // Root以下のすべてのMonoBehaviour（UdonSharp含む）を取得
            MonoBehaviour[] monos = rootObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mono in monos)
            {
                if (mono == null) continue;

                SerializedObject so = new SerializedObject(mono);

                bool isModified = false;

                SerializedProperty prop = so.FindProperty("radioAnimator");

                // radioAnimatorプロパティを持っているスクリプト（HoboRadio_Controller）を見つけたら
                if (prop != null && newAnimator != null)
                {
                    prop.objectReferenceValue = newAnimator;
                    isModified = true;
                    Debug.Log($"<color=cyan>[HoboRadio]</color> Animatorを {mono.gameObject.name} に再アタッチしました。");
                }

                // knob（スライダーボーン）の参照を更新
                SerializedProperty knobProp = so.FindProperty("knob");
                if (knobProp != null && newSliderKnob != null)
                {
                    knobProp.objectReferenceValue = newSliderKnob;
                    isModified = true;
                    Debug.Log($"<color=cyan>[HoboRadio]</color> {mono.gameObject.name} の knob を再アタッチしました。");
                }

                if (isModified)
                {
                    so.ApplyModifiedProperties();

                    if (mono is UdonSharpBehaviour usb)
                    {
                        UdonSharpEditorUtility.ApplyProxyModifications(usb);
                    }

                    EditorUtility.SetDirty(mono);
                    Debug.Log($"<color=cyan>[HoboRadio]</color> {mono.GetType().Name} の参照を更新しました。");
                }
            }
        }
    }
}
