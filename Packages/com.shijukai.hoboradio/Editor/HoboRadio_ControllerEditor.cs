#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HoboRadio_Controller))]
public class HoboRadio_ControllerEditor : Editor
{
    // 開発時に内部設定を表示したい場合は true に書き換えます
    private const bool isDebugMode = false;

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
            float aspect = (float)logoTexture.width / logoTexture.height;
            float width = EditorGUIUtility.currentViewWidth - 30f;
            float height = Mathf.Min(width / aspect, 80f);
            Rect rect = GUILayoutUtility.GetRect(width, height);
            GUI.DrawTexture(rect, logoTexture, ScaleMode.ScaleToFit);
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

        EditorGUILayout.PropertyField(serializedObject.FindProperty("isGlobal"));
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
    }
}
#endif
