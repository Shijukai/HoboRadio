#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(HoboRadio_Controller))]
public class HoboRadio_ControllerEditor : Editor
{
    // 開発時にすべてのエディターで内部設定を表示したい場合は true に書き換えます
    public static bool isDebugMode = false;

    private Texture2D logoTexture;

    private void OnEnable()
    {
        logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.shijukai.hoboradio/Runtime/Material/UI/HoboRadio_Logo.png");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ヘッダー・ロゴ表示
        EditorGUILayout.Space(5);
        if (logoTexture != null)
        {
            float cropTop = 0.15f;
            float cropBottom = 0.2f;
            float croppedHeightRatio = 1f - cropTop - cropBottom;

            // トリミング後の正確な縦横比
            float aspect = (float)logoTexture.width / (logoTexture.height * croppedHeightRatio);
            float maxHeight = 120f;

            // Inspector上の描画枠を確保
            Rect totalRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 30f, maxHeight);

            // アスペクト比を維持した実際の描画サイズを計算
            float drawHeight = totalRect.height;
            float drawWidth = drawHeight * aspect;

            if (drawWidth > totalRect.width)
            {
                drawWidth = totalRect.width;
                drawHeight = drawWidth / aspect;
            }

            // 確保した領域の中央に配置
            float drawX = totalRect.x + (totalRect.width - drawWidth) * 0.5f;
            float drawY = totalRect.y + (totalRect.height - drawHeight) * 0.5f;
            Rect drawRect = new Rect(drawX, drawY, drawWidth, drawHeight);

            Rect texCoords = new Rect(0f, cropBottom, 1f, croppedHeightRatio);
            GUI.DrawTextureWithTexCoords(drawRect, logoTexture, texCoords);
        }
        else
        {
            EditorGUILayout.LabelField("Hobo Radio Controller", EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(10);

        // カード風ボックスUI領域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("基本設定 (Basic Settings)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        SerializedProperty isGlobalProp = serializedObject.FindProperty("isGlobal");
        EditorGUILayout.PropertyField(isGlobalProp);
        bool isGlobalChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("radioPowerOn"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentChannelIndex"));
        EditorGUILayout.EndVertical();

        // 開発者モード時のみ内部設定を表示
        if (isDebugMode)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("開発者モードが有効です。", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "isGlobal", "radioPowerOn", "currentChannelIndex");
        }

        serializedObject.ApplyModifiedProperties();

        if (isGlobalChanged)
        {
            CheckTapeSyncState(isGlobalProp.boolValue);
        }
    }

    private void CheckTapeSyncState(bool isGlobal)
    {
        if (EditorWindow.HasOpenInstances<HoboRadio_SyncSettingsWindow>()) return;

        HoboRadio_Controller[] radios = FindObjectsOfType<HoboRadio_Controller>(true);
        bool hasGlobal = false;
        bool hasLocal = false;

        foreach (var radio in radios)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(radio.gameObject)) continue;
            if ((radio.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            
            SerializedObject so = new SerializedObject(radio);
            SerializedProperty prop = so.FindProperty("isGlobal");
            if (prop != null)
            {
                if (prop.boolValue) hasGlobal = true;
                else hasLocal = true;
            }
        }

        HoboTape[] rawTapes = FindObjectsOfType<HoboTape>(true);
        List<HoboTape> validTapes = new List<HoboTape>();
        
        foreach (var tape in rawTapes)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(tape.gameObject)) continue;
            if ((tape.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            validTapes.Add(tape);
        }

        List<HoboTape> targetTapes = new List<HoboTape>();

        if (hasGlobal && hasLocal)
        {
            targetTapes.AddRange(validTapes);

            if (targetTapes.Count > 0)
            {
                HoboRadio_SyncSettingsWindow.ShowWindow(isGlobal, targetTapes, true);
            }
        }
        else
        {
            foreach (var tape in validTapes)
            {
                VRC.SDK3.Components.VRCObjectSync syncComp = tape.gameObject.GetComponent<VRC.SDK3.Components.VRCObjectSync>();

                if (isGlobal && syncComp == null)
                {
                    targetTapes.Add(tape);
                }
                else if (!isGlobal && syncComp != null)
                {
                    targetTapes.Add(tape);
                }
            }

            if (targetTapes.Count > 0)
            {
                HoboRadio_SyncSettingsWindow.ShowWindow(isGlobal, targetTapes, false);
            }
        }
    }
}

[CustomEditor(typeof(HoboTape))]
public class HoboTapeEditor : Editor
{
    private static bool showDeveloperSettings = false;
    private Texture2D logoTexture;

    private void OnEnable()
    {
        logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.shijukai.hoboradio/Runtime/Material/UI/HoboRadio_Logo.png");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);
        if (logoTexture != null)
        {
            float cropTop = 0.15f;
            float cropBottom = 0.2f;
            float croppedHeightRatio = 1f - cropTop - cropBottom;

            float aspect = (float)logoTexture.width / (logoTexture.height * croppedHeightRatio);
            float maxHeight = 120f;

            Rect totalRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 30f, maxHeight);

            float drawHeight = totalRect.height;
            float drawWidth = drawHeight * aspect;

            if (drawWidth > totalRect.width)
            {
                drawWidth = totalRect.width;
                drawHeight = drawWidth / aspect;
            }

            float drawX = totalRect.x + (totalRect.width - drawWidth) * 0.5f;
            float drawY = totalRect.y + (totalRect.height - drawHeight) * 0.5f;
            Rect drawRect = new Rect(drawX, drawY, drawWidth, drawHeight);

            Rect texCoords = new Rect(0f, cropBottom, 1f, croppedHeightRatio);
            GUI.DrawTextureWithTexCoords(drawRect, logoTexture, texCoords);
        }
        else
        {
            EditorGUILayout.LabelField("Hobo Tape", EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(10);

        SerializedProperty titleProp = serializedObject.FindProperty("tapeTitle");
        SerializedProperty artistProp = serializedObject.FindProperty("tapeArtist");
        SerializedProperty urlProp = serializedObject.FindProperty("tapeUrl");

        // 通常表示領域（表示専用・URL非表示）
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("テープ情報 (Tape Info)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("タイトル", string.IsNullOrEmpty(titleProp.stringValue) ? "(未設定)" : titleProp.stringValue);
        EditorGUILayout.LabelField("サークル・制作団体", string.IsNullOrEmpty(artistProp.stringValue) ? "(未設定)" : artistProp.stringValue);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 配布団体用プルダウン領域
        showDeveloperSettings = EditorGUILayout.Foldout(showDeveloperSettings, "Developer Only", true);
        if (showDeveloperSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(titleProp, new GUIContent("タイトル"));
            EditorGUILayout.PropertyField(artistProp, new GUIContent("サークル・制作団体"));
            EditorGUILayout.PropertyField(urlProp, new GUIContent("テープURL"));
            EditorGUI.indentLevel--;
        }

        // 一括デバッグモード有効時のみ完全隠蔽パラメータを表示
        if (HoboRadio_ControllerEditor.isDebugMode)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("開発者モードが有効です。", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "tapeTitle", "tapeArtist", "tapeUrl");
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[InitializeOnLoad]
public static class HoboRadio_HierarchyMonitor
{
    private static HashSet<int> knownInstanceIDs = new HashSet<int>();
    private static bool isInitialized = false;

    static HoboRadio_HierarchyMonitor()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
    }

    private static void OnSceneChanged(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
    {
        isInitialized = false;
        knownInstanceIDs.Clear();
    }

    private static void OnHierarchyChanged()
    {
        if (Application.isPlaying) return;

        HoboRadio_Controller[] rawRadios = Object.FindObjectsOfType<HoboRadio_Controller>(true);
        HoboTape[] rawTapes = Object.FindObjectsOfType<HoboTape>(true);

        List<GameObject> validObjects = new List<GameObject>();

        foreach (var r in rawRadios)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(r.gameObject)) continue;
            if (EditorUtility.IsPersistent(r.gameObject)) continue;
            if (r.gameObject.hideFlags != HideFlags.None) continue;
            if (!r.gameObject.scene.IsValid() || !r.gameObject.scene.isLoaded) continue;
            if (string.IsNullOrEmpty(r.gameObject.scene.path)) continue;
            validObjects.Add(r.gameObject);
        }

        foreach (var t in rawTapes)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
            if (EditorUtility.IsPersistent(t.gameObject)) continue;
            if (t.gameObject.hideFlags != HideFlags.None) continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
            if (string.IsNullOrEmpty(t.gameObject.scene.path)) continue;
            validObjects.Add(t.gameObject);
        }

        // 初回ロード時は現在のオブジェクトIDを記録して終了
        if (!isInitialized)
        {
            foreach (var go in validObjects)
            {
                knownInstanceIDs.Add(go.GetInstanceID());
            }
            isInitialized = true;
            return;
        }

        bool hasNewPlaced = false;
        HashSet<int> currentIDs = new HashSet<int>();

        foreach (var go in validObjects)
        {
            int id = go.GetInstanceID();

            if (!knownInstanceIDs.Contains(id))
            {
                if (IsSelectedOrChildOfSelected(go))
                {
                    hasNewPlaced = true;
                }
                // 選択状態に関わらず、一度検出したオブジェクトは既知として登録し後からの誤発火を防ぐ
                currentIDs.Add(id);
            }
            else
            {
                currentIDs.Add(id);
            }
        }

        knownInstanceIDs = currentIDs;

        if (hasNewPlaced && !EditorWindow.HasOpenInstances<HoboRadio_SyncSettingsWindow>())
        {
            CheckSyncStateOnPlaced();
        }
    }

    private static bool IsSelectedOrChildOfSelected(GameObject go)
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0) return false;

        Transform t = go.transform;
        while (t != null)
        {
            foreach (var sel in selectedObjects)
            {
                if (t.gameObject == sel) return true;
            }
            t = t.parent;
        }
        return false;
    }

    private static void CheckSyncStateOnPlaced()
    {
        HoboRadio_Controller[] radios = Object.FindObjectsOfType<HoboRadio_Controller>(true);
        HoboTape[] tapes = Object.FindObjectsOfType<HoboTape>(true);

        if (radios.Length == 0) return;

        bool hasGlobal = false;
        bool hasLocal = false;

        foreach (var r in radios)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(r.gameObject)) continue;
            SerializedObject radioSO = new SerializedObject(r);
            SerializedProperty isGlobalProp = radioSO.FindProperty("isGlobal");
            if (isGlobalProp != null)
            {
                if (isGlobalProp.boolValue) hasGlobal = true;
                else hasLocal = true;
            }
        }

        List<HoboTape> targetTapes = new List<HoboTape>();

        if (hasGlobal && hasLocal)
        {
            foreach (var t in tapes)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
                targetTapes.Add(t);
            }

            if (targetTapes.Count > 0)
            {
                HoboRadio_SyncSettingsWindow.ShowWindow(true, targetTapes, true, HoboRadio_SyncSettingsWindow.TriggerType.Placed);
            }
        }
        else
        {
            bool isGlobal = hasGlobal;
            foreach (var t in tapes)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
                VRC.SDK3.Components.VRCObjectSync syncComp = t.gameObject.GetComponent<VRC.SDK3.Components.VRCObjectSync>();

                if ((isGlobal && syncComp == null) || (!isGlobal && syncComp != null))
                {
                    targetTapes.Add(t);
                }
            }

            if (targetTapes.Count > 0)
            {
                HoboRadio_SyncSettingsWindow.ShowWindow(isGlobal, targetTapes, false, HoboRadio_SyncSettingsWindow.TriggerType.Placed);
            }
        }
    }
}
#endif
