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
    static Vector2 scrollPos;
    static int ProductCount;

    [MenuItem("RisingWin/PrefabVariant工廠")]
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
        if (null == OriginPrefab) return;

        ProductCountField();
        AddGroupButton();
        scrollPos = GUILayout.BeginScrollView(scrollPos);
        for (int i = 0; i < ModifyGroups.Count; i++)
            ModifyGroups[i].OnGUI();
        GUILayout.EndScrollView();

        ExecuteLayout();
    }

    static void OnOriginPrefabChanged()
    {
        if (null == OriginPrefab) return;
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
        return (p.PropertyType.IsValueType && p.PropertyType.Namespace != "UnityEngine" && p.PropertyType != typeof(Vector2)) ||
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

            if (string.IsNullOrEmpty(path)||!path.Contains("Assets/"))
                return false;

            int rootIndex = path.IndexOf("Assets");
            path = path.Substring(rootIndex);
            return true;
        }
        return false;
    }
    #endregion
}

[System.Serializable]
public class ModifyGroup : ScriptableObject
{
    int m_ComponentIndex;
    int m_PropertyIndex;

    float m_StartNum;
    float m_Interval;

    [SerializeField]
    Object[] m_ReplaceObjects;

    private Component component { get { return PrefabVariantFactory.Components[m_ComponentIndex]; } }
    private PropertyInfo property { get { return PrefabVariantFactory.PropertyInfos[m_ComponentIndex][m_PropertyIndex]; } }
    bool isValueType { get { return property.PropertyType.IsValueType; } }
    bool m_Fold;

    private void OnEnable()
    {
        m_ReplaceObjects = new Object[0];
    }

    void OnSelectComponentChanged()
    {
        m_PropertyIndex = 0;
    }

    public void SetProperty(int i, ref GameObject newPrefab)
    {
        object replaceObj = GetReplaceObject(i);
        try
        {
            replaceObj = System.Convert.ChangeType(replaceObj, property.PropertyType);
        }
        catch (System.InvalidCastException)
        {
            Debug.LogError(string.Format("產生失敗: 素材{0}與該屬性{1}型別不同", replaceObj.GetType(), property.PropertyType));
            DestroyImmediate(newPrefab);
            return;
        }
        System.Type type = component.GetType();
        var o = newPrefab.GetComponent(type);
        property.SetValue(o, replaceObj);
    }

    public void SetReplaceObjectArraySize(int size)
    {
        System.Array.Resize(ref m_ReplaceObjects, size);
    }

    #region -- Layout --
    public void OnGUI()
    {
        GUILayout.BeginVertical("Box");
        RemoveGroupButton();
        ComponentLayout();
        PropertyLayout();
        ReplaceObjectsLayout();
        GUILayout.EndVertical();
    }

    void ComponentLayout()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("欲修改的組件(Component):");
        EditorGUI.BeginChangeCheck();
        m_ComponentIndex = EditorGUILayout.Popup(m_ComponentIndex, PrefabVariantFactory.ComponentNames);
        if (EditorGUI.EndChangeCheck())
            OnSelectComponentChanged();
        GUILayout.EndHorizontal();
    }

    void PropertyLayout()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("欲更改的屬性(Property):");
        m_PropertyIndex = EditorGUILayout.Popup(m_PropertyIndex, PrefabVariantFactory.PropertyNames[m_ComponentIndex].ToArray());
        GUILayout.EndHorizontal();
    }

    void ReplaceObjectsLayout()
    {
        if (isValueType)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("起始數值:");
            m_StartNum = EditorGUILayout.FloatField(m_StartNum);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("每次增加:");
            m_Interval = EditorGUILayout.FloatField(m_Interval);
            GUILayout.EndHorizontal();
        }
        else
        {
            SerializedObject serializedObject = new SerializedObject(this);
            var property = serializedObject.FindProperty("m_ReplaceObjects");
            DrawPropertyArray(property, ref m_Fold);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    void RemoveGroupButton()
    {
        GUI.color = Color.gray;
        if (GUILayout.Button("-", GUILayout.MaxWidth(50)))
            PrefabVariantFactory.RemoveGroup(this);
        GUI.color = Color.white;
    }

    private void DrawPropertyArray(SerializedProperty property, ref bool fold)
    {
        fold = EditorGUILayout.Foldout(fold, property.displayName);
        if (fold)
        {
            SerializedProperty arraySizeProp = property.FindPropertyRelative("Array.size");
            EditorGUILayout.PropertyField(arraySizeProp);

            EditorGUI.indentLevel++;

            for (int i = 0; i < arraySizeProp.intValue; i++)
            {
                EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i));
            }

            EditorGUI.indentLevel--;
        }
    }
    #endregion

    object GetReplaceObject(int i)
    {
        if (isValueType)
            return m_StartNum + i * m_Interval;
        else
            return m_ReplaceObjects[i];
    }
}

#endif