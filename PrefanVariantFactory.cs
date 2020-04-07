#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;

public class PrefabVariantFactory : EditorWindow
{
    public static Component[] Components;
    public static PropertyInfo[][] PropertyInfos;
    public static string[] ComponentNames;
    public static List<List<string>> PropertyNames;
    static string AssetSavePath, VariantName;
    static GameObject OriginPrefab;
    static EditorWindow Window;
    static List<ModifyGroup> ModifyGroups;
    static Vector2 ScrollPos;
    static int ProductCount;

    [MenuItem("Tools/PrefabVariant工廠")]
    public static void NewWindow()
    {
        Window = GetWindow<PrefabVariantFactory>("PrefabVariant工廠");
    }

    private void OnDisable()
    {
        Window = null;
        OriginPrefab = null;
    }

    private void OnGUI()
    {
        OriginPrefabLayout();

        if (null == OriginPrefab)
            return;

        ProductCountField();
        AddGroupButton();
        ScrollPos = GUILayout.BeginScrollView(ScrollPos);
        for (int i = 0; i < ModifyGroups.Count; i++)
            ModifyGroups[i].OnGUI();
        GUILayout.EndScrollView();

        ExecuteLayout();
    }

    static void OnOriginPrefabChanged()
    {
        if (null == OriginPrefab)
            return;

        AssetSavePath = AssetDatabase.GetAssetPath(OriginPrefab).Replace("/" + OriginPrefab.name + ".prefab", "");
        VariantName = OriginPrefab.name;
        ModifyGroups = new List<ModifyGroup>() { CreateInstance<ModifyGroup>() };
        PropertyNames = new List<List<string>>();
        Components = OriginPrefab.GetComponents<Component>();
        ComponentNames = new string[Components.Length];
        PropertyInfos = new PropertyInfo[Components.Length][];

        for (int i = 0; i < Components.Length; i++)
        {
            PropertyNames.Add(new List<string>());
            ComponentNames[i] = Components[i].GetType().Name;
            PropertyInfos[i] = Components[i].GetType().GetProperties().Where(IsVaildType).ToArray();
            for (int j = 0; j < PropertyInfos[i].Length; j++)
                PropertyNames[i].Add(string.Format("({0}) {1}", PropertyInfos[i][j].PropertyType, PropertyInfos[i][j].Name));
        }
    }

    static bool IsVaildType(PropertyInfo p)
    {
        return (p.PropertyType.IsValueType && 
            p.PropertyType.Namespace != "UnityEngine" &&
            p.PropertyType != typeof(Vector2)) ||
               (!p.PropertyType.IsValueType && p.PropertyType != typeof(string));
    }

    void SavePrefabVariants()
    {
        for (int i = 0; i < ProductCount; i++)
        {
            GameObject newPrefab = PrefabUtility.InstantiatePrefab(OriginPrefab) as GameObject;
            for (int j = 0; j < ModifyGroups.Count; j++)
                ModifyGroups[j].SetProperty(i, ref newPrefab);

            newPrefab.name = string.Format("{0}{1}", VariantName, i.ToString());
            PrefabUtility.SaveAsPrefabAsset(newPrefab, string.Format("{0}/{1}.prefab", AssetSavePath, newPrefab.name));
            DestroyImmediate(newPrefab);
        }
        AssetDatabase.Refresh();
        Debug.Log("產生prefab variants成功");
    }

    public static void RemoveGroup(ModifyGroup group)
    {
        ModifyGroups.Remove(group);
    }

    static void SetAllGroupReplaceObjectsCount()
    {
        foreach (var group in ModifyGroups)
            group.SetReplaceObjectArraySize(ProductCount);
    }

    #region -- Layout --

    void OriginPrefabLayout()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("原始Prefab:");
        EditorGUI.BeginChangeCheck();
        OriginPrefab = EditorGUILayout.ObjectField(OriginPrefab, typeof(GameObject), false) as GameObject;
        if (EditorGUI.EndChangeCheck())
            OnOriginPrefabChanged();
        GUILayout.EndHorizontal();
    }

    void AddGroupButton()
    {
        if (GUILayout.Button("+", GUILayout.MaxWidth(50)))
            ModifyGroups.Add(CreateInstance<ModifyGroup>());
    }

    void ProductCountField()
    {
        GUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        GUILayout.Label("產生數量:");
        ProductCount = EditorGUILayout.IntField(ProductCount);
        if (EditorGUI.EndChangeCheck())
            SetAllGroupReplaceObjectsCount();

        GUILayout.EndHorizontal();
    }

    void ExecuteLayout()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("路徑:");
        AssetSavePath = GUILayout.TextField(AssetSavePath);
        if (AssetPathFolderPanelButton("Select Path", "", out string path))
            AssetSavePath = path;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("名稱(會在後方加上編號):");
        VariantName = GUILayout.TextField(VariantName);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("執行"))
            SavePrefabVariants();
    }

    bool AssetPathFolderPanelButton(string title, string defaultPath, out string path)
    {
        path = defaultPath;
        if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.MaxHeight(20f), GUILayout.MaxWidth(20f)))
        {
            path = EditorUtility.OpenFolderPanel(title, defaultPath, defaultPath);

            if (string.IsNullOrEmpty(path) || !path.Contains("Assets/"))
                return false;

            int rootIndex = path.IndexOf("Assets");
            path = path.Substring(rootIndex);
            return true;
        }
        return false;
    }
    #endregion
}
#endif
