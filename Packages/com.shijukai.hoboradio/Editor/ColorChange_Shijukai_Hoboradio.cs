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

        // UI�p�̖��O���X�g���X�V
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

            // �f�t�H���g�Ɋ��Ɋ܂܂�Ă�����́A�܂��͒��o���s�������̂̓X�L�b�v
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

        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "�ʏ�J���[", "���胂�f��" });
        GUILayout.Space(5);

        if (selectedTab == 0)
        {
            if (defaultNames != null && defaultNames.Length > 0)
            {
                defaultIndex = EditorGUILayout.Popup("Preset", defaultIndex, defaultNames);
            }
            else
            {
                EditorGUILayout.HelpBox("�f�t�H���g�̃v���Z�b�g��������܂���B", MessageType.Warning);
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
                EditorGUILayout.HelpBox("����ł̃}�e���A����������܂���B\n�v���W�F�N�g�ɒǉ��p�b�P�[�W���C���|�[�g����Ă��邩�m�F���Ă��������B", MessageType.Info);
            }
        }

        GUILayout.Space(10);

        // ���s�\�ȏ�ԁi���X�g�ɒ��g������j���̂݃{�^����������悤�ɔz��
        bool canExecute = (selectedTab == 0 && defaultNames != null && defaultNames.Length > 0) ||
                          (selectedTab == 1 && specialNames != null && specialNames.Length > 0);

        using (new EditorGUI.DisabledScope(!canExecute))
        {
            if (GUILayout.Button("�u���������s"))
            {
                Execute();
            }
        }
    }

    void Execute()
    {
        if (rootObject == null)
        {
            Debug.LogError("[HoboRadio] Radio Root ���ݒ肳��Ă��܂���B");
            return;
        }

        // 1. Radio Root�ȉ��� "�S��" ��Renderer�iSkinnedMeshRenderer�܂ށj���擾
        var targetRenderers = rootObject.GetComponentsInChildren<Renderer>(true);
        if (targetRenderers.Length == 0)
        {
            Debug.LogError("[HoboRadio] �w�肳�ꂽ�I�u�W�F�N�g����Renderer�����������܂���B");
            return;
        }

        string selectedPrefix = (selectedTab == 0) ? defaultNames[defaultIndex] : specialNames[specialIndex];
        var targetSet = (selectedTab == 0) ? defaultLibrary[selectedPrefix] : specialLibrary[selectedPrefix];

        bool isChangedAny = false; // 1�ł��ύX���ꂽ���ǂ����̃t���O

        // 2. ���������S�Ă�Renderer���`�F�b�N����
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

                // �Y���p�[�c�����܂ރ}�e���A�����v���Z�b�g���猟��
                var foundMat = targetSet.FirstOrDefault(m => m.name.Contains(part));

                if (foundMat != null && newMats[i] != foundMat)
                {
                    newMats[i] = foundMat;
                    isModified = true;
                }
            }

            // �ύX��������Renderer�̂ݓK�p����
            if (isModified)
            {
                Undo.RecordObject(renderer, "Hoboradio change material");
                renderer.sharedMaterials = newMats;
                EditorUtility.SetDirty(renderer); // Unity�ɕύX���m���ɂ��m�点
                isChangedAny = true;
            }
        }

        // 3. ���ʂ̃��O�o��
        if (isChangedAny)
        {
            Debug.Log($"<color=cyan>[HoboRadio]</color> �v���Z�b�g '{selectedPrefix}' �Ƀ}�e���A�����X�V���܂����I");
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[HoboRadio]</color> �u�������ΏہiMain, Cover, Metal���܂ރ}�e���A���j��������Ȃ��������A���łɓ����}�e���A���ł��B");
        }
    }


}
