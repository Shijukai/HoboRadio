#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HoboRadio_Controller))]
public class HoboRadio_ControllerEditor : Editor
{
    private static bool showAdvancedSettings = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 常に表示する項目
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isGlobal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("radioPowerOn"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentChannelIndex"));

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("詳細設定欄は開発・内部用です。不具合の原因になりますのでお手を触れないようにお願いします。", MessageType.Warning);

        // その他の設定項目を折りたたみ表示
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "詳細設定 (Advanced Settings)", true);
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertiesExcluding(serializedObject, "m_Script", "isGlobal", "radioPowerOn", "currentChannelIndex");
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
