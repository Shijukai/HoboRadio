#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using System.Collections.Generic;

public class HoboRadio_SyncSettingsWindow : EditorWindow
{
    public enum TriggerType { Changed, Placed }

    private class TapeEntry
    {
        public HoboTape tape;
        public string displayName;
        public bool isSelected;
    }

    private bool isGlobalMode;
    private bool isMixedMode;
    private TriggerType currentTriggerType;
    private Dictionary<string, List<TapeEntry>> groupedEntries = new Dictionary<string, List<TapeEntry>>();
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo()
    {
        if (!VerifyCurrentState())
        {
            Debug.LogWarning("[HoboRadio] Undoによりラジオの同期設定が変更されたため、設定ウィンドウを終了しました。");
            Close();
            Repaint();
        }
    }

    public static void ShowWindow(bool isGlobal, List<HoboTape> tapes, bool isMixed = false, TriggerType triggerType = TriggerType.Changed)
    {
        HoboRadio_SyncSettingsWindow window = GetWindow<HoboRadio_SyncSettingsWindow>("同期設定の確認 (Hobo Radio)");
        window.minSize = new Vector2(400, 300);
        window.Initialize(isGlobal, tapes, isMixed, triggerType);
        window.Show();
    }

    private void Initialize(bool isGlobal, List<HoboTape> tapes, bool isMixed, TriggerType triggerType)
    {
        isGlobalMode = isGlobal;
        isMixedMode = isMixed;
        currentTriggerType = triggerType;
        groupedEntries.Clear();

        foreach (var tape in tapes)
        {
            if (tape == null) continue;

            Transform parentTransform;
            if (PrefabUtility.IsPartOfPrefabInstance(tape.gameObject))
            {
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(tape.gameObject);
                parentTransform = prefabRoot.transform.parent;
            }
            else
            {
                parentTransform = tape.transform.parent;
            }

            string parentName = parentTransform != null ? parentTransform.name : "Root";

            string cassetteName;
            if (PrefabUtility.IsPartOfPrefabInstance(tape.gameObject))
            {
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(tape.gameObject);
                cassetteName = prefabRoot.name;
            }
            else
            {
                cassetteName = tape.gameObject.name;
            }

            string title = string.IsNullOrEmpty(tape.tapeTitle) ? "(タイトル未設定)" : tape.tapeTitle;
            string displayName = $"{title} ({cassetteName})";

            // 混在モード以外は一括処理が前提のためデフォルトでチェックをON（true）にする
            bool selected = isMixed ? tape.GetComponent<VRCObjectSync>() != null : true;

            var entry = new TapeEntry
            {
                tape = tape,
                displayName = displayName,
                isSelected = selected
            };

            if (!groupedEntries.ContainsKey(parentName))
            {
                groupedEntries[parentName] = new List<TapeEntry>();
            }
            groupedEntries[parentName].Add(entry);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        string titleMessage = currentTriggerType == TriggerType.Placed
            ? "新規オブジェクトの配置を検知しました" 
            : "同期設定の変更を検知しました";

        EditorGUILayout.LabelField(titleMessage, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (isMixedMode)
        {
            EditorGUILayout.HelpBox("シーン内に Global と Local のラジオが混在しています。\n各テープの同期（VRCObjectSync）の有無を選択してください。\n\n・チェックを入れる：VRCObjectSyncを追加（位置同期を有効化）\n・チェックを外す：VRCObjectSyncを削除（位置同期を無効化）", MessageType.Warning);
        }
        else if (isGlobalMode)
        {
            EditorGUILayout.HelpBox("ラジオが Global (グローバル同期) に設定されています。\n以下のテープに VRCObjectSync を追加しますか？\n\n・チェックを入れる：VRCObjectSyncを追加（位置同期を有効化）\n・チェックを外す：追加せずスキップ", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("ラジオが Local (ローカル動作) に設定されています。\n以下のテープから VRCObjectSync を削除しますか？\n\n・チェックを入れる：VRCObjectSyncを削除（位置同期を無効化）\n・チェックを外す：削除せずスキップ", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.skin.box);
        try
        {
            GUIStyle strikeStyle = new GUIStyle(EditorStyles.label);
            strikeStyle.richText = true;

            foreach (var group in groupedEntries)
            {
                EditorGUILayout.LabelField($"親階層: {group.Key}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var entry in group.Value)
                {
                    if (entry.tape == null)
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ToggleLeft($"<s>{entry.displayName}</s> (削除済み)", entry.isSelected, strikeStyle);
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        entry.isSelected = EditorGUILayout.ToggleLeft(entry.displayName, entry.isSelected);
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(10);

        bool shouldClose = false;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("実行 (Apply)", GUILayout.Height(30)))
        {
            ExecuteChanges();
            shouldClose = true;
        }
        if (GUILayout.Button("キャンセル (Cancel)", GUILayout.Height(30)))
        {
            shouldClose = true;
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(10);

        if (shouldClose)
        {
            Close();
            GUIUtility.ExitGUI();
        }
    }

    private bool VerifyCurrentState()
    {
        List<HoboRadio_Controller> radios = HoboRadio_HierarchyMonitor.GetValidObjects<HoboRadio_Controller>();
        if (radios.Count == 0) return false;

        bool currentHasGlobal = false;
        bool currentHasLocal = false;

        foreach (var r in radios)
        {
            SerializedObject radioSO = new SerializedObject(r);
            SerializedProperty isGlobalProp = radioSO.FindProperty("isGlobal");
            if (isGlobalProp != null)
            {
                if (isGlobalProp.boolValue) currentHasGlobal = true;
                else currentHasLocal = true;
            }
        }

        bool currentIsMixed = currentHasGlobal && currentHasLocal;
        bool currentIsGlobal = currentHasGlobal;

        if (currentIsMixed != isMixedMode) return false;
        if (!currentIsMixed && currentIsGlobal != isGlobalMode) return false;

        return true;
    }

    private void ExecuteChanges()
    {
        if (!VerifyCurrentState())
        {
            Debug.LogWarning("[HoboRadio] ラジオの同期設定が変更されたため、適用を中止しました。");
            return;
        }

        Undo.SetCurrentGroupName("テープの同期設定を一括更新");
        int undoGroup = Undo.GetCurrentGroup();
        bool hasChanged = false;

        foreach (var group in groupedEntries.Values)
        {
            foreach (var entry in group)
            {
                if (entry.tape == null) continue;

                if (!isMixedMode && !entry.isSelected) continue;

                VRCObjectSync syncObj = entry.tape.gameObject.GetComponent<VRCObjectSync>();
                bool shouldHaveSync = isMixedMode ? entry.isSelected : isGlobalMode;

                if (shouldHaveSync)
                {
                    if (syncObj == null)
                    {
                        bool reverted = false;

                        if (PrefabUtility.IsPartOfPrefabInstance(entry.tape.gameObject))
                        {
                            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(entry.tape.gameObject);
                            if (prefabAsset != null)
                            {
                                VRCObjectSync assetSync = prefabAsset.GetComponent<VRCObjectSync>();
                                if (assetSync != null)
                                {
                                    PrefabUtility.RevertRemovedComponent(entry.tape.gameObject, assetSync, InteractionMode.UserAction);
                                    reverted = true;
                                }
                            }
                        }

                        if (!reverted)
                        {
                            Undo.AddComponent<VRCObjectSync>(entry.tape.gameObject);
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
        }
        
        Undo.CollapseUndoOperations(undoGroup);

        if (hasChanged)
        {
            Debug.Log("[HoboRadio] テープの同期設定を更新しました。");
        }
    }
}
#endif
