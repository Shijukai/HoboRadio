#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using System.Collections.Generic;

public class HoboRadio_SyncSettingsWindow : EditorWindow
{
    private bool isGlobalMode;
    private bool isMixedMode;
    private List<HoboTape> targetTapes = new List<HoboTape>();
    private Dictionary<string, List<HoboTape>> groupedTapes = new Dictionary<string, List<HoboTape>>();
    private Dictionary<HoboTape, bool> tapeSelection = new Dictionary<HoboTape, bool>();
    private Vector2 scrollPosition;

    public static void ShowWindow(bool isGlobal, List<HoboTape> tapes, bool isMixed = false)
    {
        HoboRadio_SyncSettingsWindow window = GetWindow<HoboRadio_SyncSettingsWindow>("同期設定の確認 (Hobo Radio)");
        window.minSize = new Vector2(400, 300);
        window.Initialize(isGlobal, tapes, isMixed);
        window.Show();
    }

    private void Initialize(bool isGlobal, List<HoboTape> tapes, bool isMixed)
    {
        isGlobalMode = isGlobal;
        isMixedMode = isMixed;
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
            if (isMixedMode)
            {
                tapeSelection[tape] = tape.GetComponent<VRCObjectSync>() != null;
            }
            else
            {
                tapeSelection[tape] = true;
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("同期設定の変更を検知しました", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (isMixedMode)
        {
            EditorGUILayout.HelpBox("シーン内に Global と Local のラジオが混在しています。\n各テープの同期（VRCObjectSync）の有無を選択してください。", MessageType.Warning);
        }
        else if (isGlobalMode)
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
                if (isMixedMode)
                {
                    tapeSelection[tape] = EditorGUILayout.ToggleLeft($"{tape.gameObject.name} (VRCObjectSyncを有効化)", tapeSelection[tape]);
                }
                else
                {
                    tapeSelection[tape] = EditorGUILayout.ToggleLeft(tape.gameObject.name, tapeSelection[tape]);
                }
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
        Undo.SetCurrentGroupName("テープの同期設定を一括更新");
        int undoGroup = Undo.GetCurrentGroup();
        bool hasChanged = false;

        foreach (var tape in targetTapes)
        {
            if (tape == null) continue;

            // 混在モード以外でチェックが外れている場合は処理をスキップ
            if (!isMixedMode && !tapeSelection[tape]) continue;

            VRCObjectSync syncObj = tape.gameObject.GetComponent<VRCObjectSync>();
            bool shouldHaveSync = isMixedMode ? tapeSelection[tape] : isGlobalMode;

            if (shouldHaveSync)
            {
                if (syncObj == null)
                {
                    bool reverted = false;

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

                    if (!reverted)
                    {
                        Undo.AddComponent<VRCObjectSync>(tape.gameObject);
                    }
                    hasChanged = true;
                }
            }
            else
            {
                if (syncObj != null)
                {
                    Undo.DestroyObjectImmediate(syncObj);
                    hasChanged = true;
                }
            }
        }
        
        if (hasChanged)
        {
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[HoboRadio] テープの同期設定を更新しました。");
        }
        else
        {
            Undo.RevertAllDownToGroup(undoGroup);
        }
    }
}
#endif
