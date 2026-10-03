#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using System.Collections.Generic;

public class HoboRadio_SyncSettingsWindow : EditorWindow
{
    private bool isGlobalMode;
    private List<HoboTape> targetTapes = new List<HoboTape>();
    private Dictionary<string, List<HoboTape>> groupedTapes = new Dictionary<string, List<HoboTape>>();
    private Dictionary<HoboTape, bool> tapeSelection = new Dictionary<HoboTape, bool>();
    private Vector2 scrollPosition;

    public static void ShowWindow(bool isGlobal, List<HoboTape> tapes)
    {
        HoboRadio_SyncSettingsWindow window = GetWindow<HoboRadio_SyncSettingsWindow>("同期設定の確認 (Hobo Radio)");
        window.minSize = new Vector2(400, 300);
        window.Initialize(isGlobal, tapes);
        window.Show();
    }

    private void Initialize(bool isGlobal, List<HoboTape> tapes)
    {
        isGlobalMode = isGlobal;
        targetTapes = tapes;
        groupedTapes.Clear();
        tapeSelection.Clear();

        foreach (var tape in targetTapes)
        {
            if (tape == null) continue;
            string parentName = tape.transform.parent != null ? tape.transform.parent.name : "Root";
            if (!groupedTapes.ContainsKey(parentName))
            {
                groupedTapes[parentName] = new List<HoboTape>();
            }
            groupedTapes[parentName].Add(tape);
            tapeSelection[tape] = true;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("同期設定の変更を検知しました", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (isGlobalMode)
        {
            EditorGUILayout.HelpBox("ラジオが Global (グローバル同期) に設定されています。\n以下のテープに VRCObjectSync を追加しますか？", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("ラジオが Local (ローカル動作) に設定されています。\n以下のテープから VRCObjectSync を削除しますか？", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.skin.box);
        
        foreach (var group in groupedTapes)
        {
            EditorGUILayout.LabelField($"親階層: {group.Key}", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var tape in group.Value)
            {
                tapeSelection[tape] = EditorGUILayout.ToggleLeft(tape.gameObject.name, tapeSelection[tape]);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("実行 (Apply)", GUILayout.Height(30)))
        {
            ExecuteChanges();
            Close();
        }
        if (GUILayout.Button("キャンセル (Cancel)", GUILayout.Height(30)))
        {
            Close();
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(10);
    }

    private void ExecuteChanges()
    {
        foreach (var tape in targetTapes)
        {
            if (tape == null || !tapeSelection[tape]) continue;

            VRCObjectSync syncObj = tape.gameObject.GetComponent<VRCObjectSync>();

            if (isGlobalMode)
            {
                if (syncObj == null)
                {
                    bool reverted = false;

                    // Prefabインスタンスであり、元のPrefabにVRCObjectSyncが存在する場合はRevert（元に戻す）する
                    if (PrefabUtility.IsPartOfPrefabInstance(tape.gameObject))
                    {
                        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(tape.gameObject);
                        if (prefabAsset != null)
                        {
                            VRCObjectSync assetSync = prefabAsset.GetComponent<VRCObjectSync>();
                            if (assetSync != null)
                            {
                                PrefabUtility.RevertRemovedComponent(tape.gameObject, assetSync, InteractionMode.UserAction);
                                reverted = true;
                            }
                        }
                    }

                    // PrefabのRevertで解決できなかった場合のみ新規追加する
                    if (!reverted)
                    {
                        Undo.AddComponent<VRCObjectSync>(tape.gameObject);
                    }
                }
            }
            else
            {
                if (syncObj != null)
                {
                    // 削除操作（Prefabインスタンスの場合は自動的に「Removed Component」のオーバーライドとして記録される）
                    Undo.DestroyObjectImmediate(syncObj);
                }
            }
        }
        Debug.Log("[HoboRadio] テープの同期設定を更新しました。");
    }
}
#endif
