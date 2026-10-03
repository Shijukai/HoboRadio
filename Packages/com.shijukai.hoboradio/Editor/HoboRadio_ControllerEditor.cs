#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

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

        if (isGlobalChanged)
        {
            CheckTapeSyncState(isGlobalProp.boolValue);
        }

        // 開発者モード時のみ内部設定を表示
        if (isDebugMode)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("開発者モードが有効です。", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "isGlobal", "radioPowerOn", "currentChannelIndex");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CheckTapeSyncState(bool isGlobal)
    {
        HoboTape[] allTapes = FindObjectsOfType<HoboTape>();
        System.Collections.Generic.List<HoboTape> targetTapes = new System.Collections.Generic.List<HoboTape>();

        foreach (var tape in allTapes)
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
            HoboRadio_SyncSettingsWindow.ShowWindow(isGlobal, targetTapes);
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
#endif
