#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

[System.Serializable]
public class ModifyGroup : ScriptableObject
{
    int m_ComponentIndex;
    int m_PropertyIndex;

    float m_StartNum;
    float m_Interval;

    [SerializeField]
    Object[] m_ReplaceObjects;

    private Component Component { get { return PrefabVariantFactory.Components[m_ComponentIndex]; } }
    private PropertyInfo Property { get { return PrefabVariantFactory.PropertyInfos[m_ComponentIndex][m_PropertyIndex]; } }
    private bool IsValueType { get { return Property.PropertyType.IsValueType; } }
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
            replaceObj = System.Convert.ChangeType(replaceObj, Property.PropertyType);
        }
        catch (System.InvalidCastException)
        {
            Debug.LogError(string.Format("產生失敗: 素材{0}與該屬性{1}型別不同", replaceObj.GetType(), Property.PropertyType));
            DestroyImmediate(newPrefab);
            return;
        }
        System.Type type = Component.GetType();
        var o = newPrefab.GetComponent(type);
        Property.SetValue(o, replaceObj);
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
        if (IsValueType)
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
        if (IsValueType)
            return m_StartNum + i * m_Interval;
        else
            return m_ReplaceObjects[i];
    }
} 
#endif
