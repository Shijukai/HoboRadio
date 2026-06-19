using UnityEngine;
using UnityEditor;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using System.Collections.Generic;

public class Window_Shijukai_Hoboradio_ColorChange : EditorWindow 
{
    private GameObject rootObject;

    //Preset
    private Dictionary<string, List<Material>> defaultLibrary = new Dictionary<string, List<Material>>();
    private string[] defaultNames;
    private int defaultIndex = 0;
    private const string GUID_PRESET_FOLDER = "930d03a908037094d8800ab4e77a4c40";

    private Dictionary<string, List<Material>> specialLibrary = new Dictionary<string, List<Material>>();
    private string[] specialNames;
    private int specialIndex = 0;

    private int selectedTab = 0;

    //main script
    [MenuItem("Tools/Shijukai/Hoboradio_ColorChange")]
    static void Open()
    {
        GetWindow<Window_Shijukai_Hoboradio_ColorChange>("Hoboradio_ColorChange");
    }
    void OnEnable()
    {
        LoadDefaultPresets();
        LoadSpecialPresets();
    }

    void LoadDefaultPresets()
    {
        string currentPresetPath = AssetDatabase.GUIDToAssetPath(GUID_PRESET_FOLDER);
        string[] subFolders = System.IO.Directory.GetDirectories(currentPresetPath);

        defaultLibrary.Clear();

        foreach (var folder in subFolders)
        {
            string folderName = System.IO.Path.GetFileName(folder);
            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });

            var mats = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .ToList();

            defaultLibrary.Add(folderName, mats);
        }

        // UI用の名前リストを更新
        defaultNames = defaultLibrary.Keys.ToArray();

    }

    void LoadSpecialPresets()
    {
        specialLibrary.Clear();
        string[] mainGuids = AssetDatabase.FindAssets("t:Material Shijukai_Radio_Main", new[] { "Assets" });

        foreach (string guid in mainGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mainMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mainMat == null) continue;

            string setName = mainMat.name.Replace("Shijukai_Radio_Main_", "");

            // デフォルトに既に含まれているもの、または抽出失敗したものはスキップ
            if (string.IsNullOrEmpty(setName) || defaultLibrary.ContainsKey(setName)) continue;

            string folderPath = System.IO.Path.GetDirectoryName(path);
            string[] folderGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

            List<Material> setMaterials = folderGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && (m.name.Contains("Main") || m.name.Contains("Cover") || m.name.Contains("Metal")))
                .ToList();

            if (!specialLibrary.ContainsKey(setName))
            {
                specialLibrary.Add(setName, setMaterials);
            }
        }
        specialNames = specialLibrary.Keys.ToArray();
    }

    private void OnGUI()
    {
        GUILayout.Label("ほぼらじお カラー変更ツール",EditorStyles.boldLabel);

        rootObject = (GameObject)EditorGUILayout.ObjectField(
            "Radio_Local/Radio_Global",
            rootObject,
            typeof(GameObject),
            true
        );

        GUILayout.Space(10);

        //プリセット選択
        GUILayout.Label("プリセット選択", EditorStyles.boldLabel);

        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "通常カラー", "限定版カラー" });
        GUILayout.Space(5);

        if (selectedTab == 0)
        {
            if (defaultNames != null && defaultNames.Length > 0)
            {
                defaultIndex = EditorGUILayout.Popup("Preset", defaultIndex, defaultNames);
            }
            else
            {
                EditorGUILayout.HelpBox("デフォルトのプリセットが見つかりません。", MessageType.Warning);
            }
        }
        else
        {
            if (specialNames != null && specialNames.Length > 0)
            {
                specialIndex = EditorGUILayout.Popup("Special Preset", specialIndex, specialNames);
            }
            else
            {
                EditorGUILayout.HelpBox("限定版のマテリアルが見つかりません。\nプロジェクトに追加パッケージがインポートされているか確認してください。", MessageType.Info);
            }
        }

        GUILayout.Space(10);

        // 実行可能な状態（リストに中身がある）時のみボタンを押せるように配慮
        bool canExecute = (selectedTab == 0 && defaultNames != null && defaultNames.Length > 0) ||
                          (selectedTab == 1 && specialNames != null && specialNames.Length > 0);

        using (new EditorGUI.DisabledScope(!canExecute))
        {
            if (GUILayout.Button("置き換え実行"))
            {
                Execute();
            }
        }
    }

    void Execute()
    {
        if (rootObject == null)
        {
            Debug.LogError("[HoboRadio] 対象オブジェクトが設定されていません。");
            return;
        }

        // 1. Radio Root以下の "全て" のRenderer（SkinnedMeshRenderer含む）を取得
        var targetRenderers = rootObject.GetComponentsInChildren<Renderer>(true);
        if (targetRenderers.Length == 0)
        {
            Debug.LogError("[HoboRadio] 指定されたオブジェクト内にRendererが一つも見つかりません。");
            return;
        }

        string selectedPrefix = (selectedTab == 0) ? defaultNames[defaultIndex] : specialNames[specialIndex];
        var targetSet = (selectedTab == 0) ? defaultLibrary[selectedPrefix] : specialLibrary[selectedPrefix];

        bool isChangedAny = false;

        // 2. 見つかった全てのRendererをチェックする
        foreach (var renderer in targetRenderers)
        {
            Material[] newMats = (Material[])renderer.sharedMaterials.Clone();
            bool isModified = false;

            for (int i = 0; i < newMats.Length; i++)
            {
                if (newMats[i] == null) continue;
                string matName = newMats[i].name;

                string part = "";
                if (matName.Contains("Main")) part = "Main";
                else if (matName.Contains("Cover")) part = "Cover";
                else if (matName.Contains("Metal")) part = "Metal";

                if (string.IsNullOrEmpty(part)) continue;

                // 該当パーツ名を含むマテリアルをプリセットから検索
                var foundMat = targetSet.FirstOrDefault(m => m.name.Contains(part));

                if (foundMat != null && newMats[i] != foundMat)
                {
                    newMats[i] = foundMat;
                    isModified = true;
                }
            }

            // 変更があったRendererのみ適用する
            if (isModified)
            {
                Undo.RecordObject(renderer, "Hoboradio change material");
                renderer.sharedMaterials = newMats;
                EditorUtility.SetDirty(renderer);
                isChangedAny = true;
            }
        }

        // 3. 結果のログ出力
        if (isChangedAny)
        {
            Debug.Log($"<color=cyan>[HoboRadio]</color> プリセット '{selectedPrefix}' にマテリアルを更新しました！");
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[HoboRadio]</color> 置き換え対象（Main, Cover, Metalを含むマテリアル）が見つからなかったか、すでに同じマテリアルです。");
        }
    }


}
