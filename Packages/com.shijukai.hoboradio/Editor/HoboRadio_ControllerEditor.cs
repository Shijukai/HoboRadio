#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HoboRadio_Controller))]
public class HoboRadio_ControllerEditor : Editor
{
    // 開発時に内部設定を表示したい場合は true に書き換えます
    private const bool isDebugMode = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 常に表示する項目
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isGlobal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("radioPowerOn"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentChannelIndex"));

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
