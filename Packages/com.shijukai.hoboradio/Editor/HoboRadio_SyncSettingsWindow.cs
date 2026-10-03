#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using System.Collections.Generic;

public class HoboRadio_SyncSettingsWindow : EditorWindow
{
    public enum TriggerType { Changed, Placed }

    private bool isGlobalMode;
    private bool isMixedMode;
    private TriggerType currentTriggerType;
    private List<HoboTape> targetTapes = new List<HoboTape>();
    private Dictionary<string, List<HoboTape>> groupedTapes = new Dictionary<string, List<HoboTape>>();
    private Dictionary<HoboTape, bool> tapeSelection = new Dictionary<HoboTape, bool>();
    private Vector2 scrollPosition;

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
        targetTapes = tapes;
        groupedTapes.Clear();
        tapeSelection.Clear();

        foreach (var tape in targetTapes)
        {
            if (tape == null) continue;

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(tape.gameObject);
            Transform rootTransform = prefabRoot != null ? prefabRoot.transform : tape.transform.root;
            string parentName = rootTransform.parent != null ? rootTransform.parent.name : "Root";

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

        foreach (var group in groupedTapes)
        {
            EditorGUILayout.LabelField($"親階層: {group.Key}", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var tape in group.Value)
            {
                if (tape == null) continue;

                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(tape.gameObject);
                Transform rootTransform = prefabRoot != null ? prefabRoot.transform : tape.transform.root;
                string cassetteName = rootTransform.name;
                string title = string.IsNullOrEmpty(tape.tapeTitle) ? "(タイトル未設定)" : tape.tapeTitle;
                string displayName = $"{title} ({cassetteName})";

                tapeSelection[tape] = EditorGUILayout.ToggleLeft(displayName, tapeSelection[tape]);
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
