using UnityEngine;
using UnityEditor;

public static class Menu_Shijukai_Hoboradio
{
    // --- GUID定義 ---
    private const string GUID_GLOBAL = "222e3ff0c0a19b4489da092102f0744b";
    private const string GUID_GLOBAL_UI = "d944dba58ccf4b34fbff0783c8f57422";
    private const string GUID_LOCAL = "9ceb1a35aad7f114aa5acc520e50f4f8";
    private const string GUID_LOCAL_UI = "b6c378c68dde3af4d9f8acfc4dedc578";
    private const string GUID_INFO = "722f3715e10fc76409088b329bc2ab68";

    // Item List Global===========================================
    [MenuItem("GameObject/HoboRadio/Global/Radio_Global", false, 10)]
    static void Create_Global_Radio(MenuCommand cmd)
        => Create(cmd, GUID_GLOBAL);

    [MenuItem("GameObject/HoboRadio/Global/Radio_Global_UI", false, 11)]
    static void Create_Global_Radio_UI(MenuCommand cmd)
        => Create(cmd, GUID_GLOBAL_UI);

    // Item List Local=============================================
    [MenuItem("GameObject/HoboRadio/Local/Radio_Local", false, 20)]
    static void Create_Local_Radio(MenuCommand cmd)
        => Create(cmd, GUID_LOCAL);

    [MenuItem("GameObject/HoboRadio/Local/Radio_Local_UI", false, 21)]
    static void Create_Local_Radio_UI(MenuCommand cmd)
        => Create(cmd, GUID_LOCAL_UI);

    // Item List Information=======================================
    [MenuItem("GameObject/HoboRadio/Radio_Information", false, 30)]
    static void Create_Radio_Information(MenuCommand cmd)
    {
        // Hierarchyで選択されているオブジェクトを取得
        GameObject selectedObj = cmd.context as GameObject ?? Selection.activeGameObject;

        if (selectedObj == null)
        {
            EditorUtility.DisplayDialog("HoboRadio", "Hierarchy上でラジオ本体を選択した状態で実行してください。", "OK");
            return;
        }

        GameObject radioContainer = null; // 外枠（Radio_Global等）
        GameObject targetRoot = null;      // Udon（HoboRadio_Controller）があるRoot

        // 選択対象からRadio_Rootを特定
        if (selectedObj.name == "Radio_Root" || selectedObj.GetComponent("HoboRadio_Controller") != null)
        {
            targetRoot = selectedObj;
            radioContainer = selectedObj.transform.parent != null ? selectedObj.transform.parent.gameObject : selectedObj;
        }
        else
        {
            radioContainer = selectedObj;
            Transform rootTrans = selectedObj.transform.Find("Radio_Root");
            if (rootTrans != null)
            {
                targetRoot = rootTrans.gameObject;
            }
            else
            {
                foreach (Transform child in selectedObj.transform)
                {
                    if (child.GetComponent("UdonBehaviour") != null)
                    {
                        targetRoot = child.gameObject;
                        break;
                    }
                }
            }
        }

        if (targetRoot == null)
        {
            EditorUtility.DisplayDialog("HoboRadio", "Radio_Rootが見つかりませんでした。対象を正しく選択してください。", "OK");
            return;
        }

        // プレハブの生成
        string path = AssetDatabase.GUIDToAssetPath(GUID_INFO);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Prefab not found: " + path);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // Radio_Globalと同じ階層に配置し、位置と回転を合わせる
        Transform finalParent = radioContainer.transform.parent;
        GameObjectUtility.SetParentAndAlign(instance, finalParent != null ? finalParent.gameObject : null);
        instance.transform.position = radioContainer.transform.position;
        instance.transform.rotation = radioContainer.transform.rotation;

        Undo.RegisterCreatedObjectUndo(instance, "Create Radio_Information");

        // --- 相互アタッチ処理 ---
        bool linkedToRadio = false;
        bool linkedToInfo = false;

        // 1. OLED(InfoFetcher) -> Radio_Root へのアタッチ (Info成功コードを採用)
        var infoMonos = instance.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mono in infoMonos)
        {
            if (mono == null) continue;
            SerializedObject so = new SerializedObject(mono);
            SerializedProperty prop = so.FindProperty("radioController");

            if (prop != null)
            {
                var targetUdon = targetRoot.GetComponent("HoboRadio_Controller");
                if (targetUdon != null)
                {
                    prop.objectReferenceValue = targetUdon;
                    so.ApplyModifiedProperties();
                    linkedToRadio = true;
                }
                break;
            }
        }

        // 2. Radio_Root(Controller) -> OLED(InfoFetcher) へのアタッチ (Radio成功コードを採用)
        Component infoUdon = null;
        Transform fetcherTransform = instance.transform.Find("Script/Root");

        // パスから取得
        if (fetcherTransform != null)
        {
            infoUdon = fetcherTransform.GetComponent("UdonBehaviour");
        }

        // パスで見つからない場合は予備として全探索
        if (infoUdon == null)
        {
            foreach (var m in infoMonos)
            {
                if (m != null && new SerializedObject(m).FindProperty("radioController") != null)
                {
                    infoUdon = m;
                    break;
                }
            }
        }

        if (infoUdon != null)
        {
            var rootMonos = targetRoot.GetComponents<MonoBehaviour>();
            foreach (var mono in rootMonos)
            {
                if (mono == null) continue;
                SerializedObject soRadio = new SerializedObject(mono);
                SerializedProperty propFetcher = soRadio.FindProperty("infoFetcher");

                if (propFetcher != null)
                {
                    propFetcher.objectReferenceValue = infoUdon;
                    soRadio.ApplyModifiedProperties();
                    linkedToInfo = true;
                    break;
                }
            }
        }

        // 実行結果のログ出力
        if (linkedToRadio && linkedToInfo)
        {
            Debug.Log($"<color=cyan>[HoboRadio]</color> {radioContainer.name} と {instance.name} の相互アタッチが完了しました。");
        }
        else
        {
            Debug.LogWarning($"[HoboRadio] アタッチ不完全: Radio->Info({linkedToInfo}), Info->Radio({linkedToRadio})");
        }

        Selection.activeObject = instance;
    }

    // 汎用生成処理
    static void Create(MenuCommand cmd, string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[HoboRadio] Prefab not found for GUID: {guid}. 名前変更ではなく「削除して作り直し」をしていませんか？");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        GameObjectUtility.SetParentAndAlign(instance, cmd.context as GameObject);
        Undo.RegisterCreatedObjectUndo(instance, "Create hoboradio Prefab");
        Selection.activeObject = instance;
    }
}