#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// With a Definition assigned, the car's own base values are not used, so they are hidden here instead of
    /// being shown as editable numbers that do nothing. Without a Definition the normal fields are shown.
    /// </summary>
    [CustomEditor(typeof(VehicleStats))]
    public class VehicleStatsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            var definition = serializedObject.FindProperty("definition");

            if (definition.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(definition);
                EditorGUILayout.HelpBox(
                    "Base values come from this Vehicle Definition. Edit them in the asset (changes also apply while playing). " +
                    "Modifiers/upgrades are added on top of them at runtime.",
                    MessageType.Info);
                if (GUILayout.Button("Select Definition Asset"))
                {
                    Selection.activeObject = definition.objectReferenceValue;
                    EditorGUIUtility.PingObject(definition.objectReferenceValue);
                }
            }
            else
            {
                DrawPropertiesExcluding(serializedObject, "m_Script");
                EditorGUILayout.HelpBox(
                    "No Definition assigned: this component's own base values are used. " +
                    "Drag the car's Vehicle Definition into the Definition field to use one shared source of truth.",
                    MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
