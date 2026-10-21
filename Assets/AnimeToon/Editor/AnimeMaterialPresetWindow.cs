// Editor only. Place in an "Editor" folder.
// Tools > Anime Toon > Material Presets
//
//   Apply To Selection        gives every selected material (or every material in a selected folder / on a selected
//                             object) the chosen preset.
//   Apply By Material Name    picks the preset for each selected material from its name (glass, water, asphalt,
//                             curb, marking, building, shop, wall ...) and asks before changing anything.
//   Create Sample Materials   makes one ready material per preset in Assets/AnimeToon/Materials/Presets.
//
// Everything can be undone with Ctrl+Z.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public class AnimeMaterialPresetWindow : EditorWindow
    {
        AnimePreset preset = AnimePreset.BuildingPastel;
        bool keepColor = true;
        bool keepTexture = true;

        void OnEnable()
        {
            AnimeMaterialPresets.BuildingOutlines = EditorPrefs.GetBool("AnimeToon.BuildingOutlines", false);
        }

        [MenuItem("Tools/Anime Toon/Material Presets")]
        static void Open()
        {
            var window = GetWindow<AnimeMaterialPresetWindow>("Anime Materials");
            window.minSize = new Vector2(340f, 300f);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Select materials, a folder of materials, or objects in the scene, then apply a preset. " +
                "Ctrl+Z undoes it.", MessageType.Info);

            EditorGUILayout.Space();
            preset = (AnimePreset)EditorGUILayout.EnumPopup("Preset", preset);
            EditorGUILayout.LabelField(AnimeMaterialPresets.Describe(preset), EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            keepColor = EditorGUILayout.ToggleLeft("Keep the material's own color", keepColor);
            keepTexture = EditorGUILayout.ToggleLeft("Keep the material's own texture", keepTexture);

            bool outlines = EditorGUILayout.ToggleLeft("Building outlines (silhouette + corner lines)", AnimeMaterialPresets.BuildingOutlines);
            if (outlines != AnimeMaterialPresets.BuildingOutlines)
            {
                AnimeMaterialPresets.BuildingOutlines = outlines;
                EditorPrefs.SetBool("AnimeToon.BuildingOutlines", outlines);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply To Selection", GUILayout.Height(28f))) ApplyToSelection();

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply By Material Name", GUILayout.Height(28f))) ApplyByName();

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Sample Materials (all presets)")) AnimeMaterialPresets.CreateSamples();
        }

        void ApplyToSelection()
        {
            List<Material> materials = AnimeMaterialPresets.CollectSelection();
            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog("Anime Materials", "Nothing selected. Select materials, a folder or objects first.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Anime Materials",
                    "Apply '" + preset + "' to " + materials.Count + " material(s)?", "Apply", "Cancel"))
                return;

            int done = 0;
            foreach (Material m in materials)
                if (AnimeMaterialPresets.Apply(m, preset, keepColor, keepTexture)) done++;

            AssetDatabase.SaveAssets();
            Debug.Log("Anime materials: applied '" + preset + "' to " + done + " material(s).");
        }

        void ApplyByName()
        {
            List<Material> materials = AnimeMaterialPresets.CollectSelection();
            var plan = new List<KeyValuePair<Material, AnimePreset>>();
            foreach (Material m in materials)
                if (AnimeMaterialPresets.TryGuess(m.name, out AnimePreset guess))
                    plan.Add(new KeyValuePair<Material, AnimePreset>(m, guess));

            if (plan.Count == 0)
            {
                EditorUtility.DisplayDialog("Anime Materials",
                    "None of the " + materials.Count + " selected material(s) has a recognised name.\n" +
                    "Recognised words: glass, water, marking, lane, curb, sidewalk, asphalt, road, shop, market, building, tower, house, wall, fence.",
                    "OK");
                return;
            }

            var list = new StringBuilder();
            for (int i = 0; i < plan.Count && i < 14; i++)
                list.AppendLine(plan[i].Key.name + "  ->  " + plan[i].Value);
            if (plan.Count > 14) list.AppendLine("... and " + (plan.Count - 14) + " more");

            if (!EditorUtility.DisplayDialog("Anime Materials",
                    "Apply presets by name to " + plan.Count + " of " + materials.Count + " selected material(s)?\n\n" + list,
                    "Apply", "Cancel"))
                return;

            int done = 0;
            foreach (var entry in plan)
                if (AnimeMaterialPresets.Apply(entry.Key, entry.Value, keepColor, keepTexture)) done++;

            AssetDatabase.SaveAssets();
            Debug.Log("Anime materials: applied presets by name to " + done + " material(s).");
        }
    }
}
#endif
