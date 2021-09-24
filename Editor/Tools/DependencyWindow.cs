/*
  NoZ Unity Library

  Copyright(c) 2019 NoZ Games, LLC

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files(the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions :

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace NoZ.Tools
{
    internal class DependencyWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            DependencyWindow.OnDeleteAssets(deletedAssets.Select(p => AssetDatabase.GUIDToAssetPath(p)).ToArray());
        }
    }

    public class DependencyWindow : EditorWindow
    {
        private UnityObject[] _objects;
        private Vector2 _scrollPosition;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/NoZ/Dependencies")]
        static void Init()
        {
            GetWindow<DependencyWindow>().Show();
        }

        [MenuItem("Assets/Find Unused", true)]
        private static bool FindUnusedValidate() => Selection.objects != null && Selection.objects.Length > 0;

        [MenuItem("Assets/Find Unused", false, 25)]
        private static void FindUnused ()
        {
            FindUnused(Selection.objects.Select(o => AssetDatabase.GetAssetPath(o)).ToArray());
        }

        [MenuItem("Assets/Find References In Project", true)]
        private static bool FindReferencesValidate() => Selection.objects != null && Selection.objects.Length > 0;

        [MenuItem("Assets/Find References In Project", false, 25)]
        private static void FindReferences()
        {
            var paths = Selection.objects.Select(o => AssetDatabase.GetAssetPath(o)).Where(p => !string.IsNullOrEmpty(p)).ToArray();

            var references = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .Where(p =>
                    EditorUtility.CollectDependencies(new[] { AssetDatabase.LoadAssetAtPath(p, typeof(UnityObject)) })
                        .Select(o => AssetDatabase.GetAssetPath(o))
                        .Any(pp => paths.Contains(pp) && p != pp))
                .Distinct()
                .ToArray();

            var window = GetWindow<DependencyWindow>();
            window._objects =
                references
                    .Select(p => AssetDatabase.LoadAssetAtPath(p, typeof(UnityObject)))
                    .ToArray();
            window.Show();
        }

        private static void FindUnused (string[] paths)
        {
            var folders = paths.Where(p => AssetDatabase.IsValidFolder(p)).ToArray();
            var pathsInFolders = folders.Length > 0 ?
                AssetDatabase.FindAssets("", paths.Where(p => AssetDatabase.IsValidFolder(p)).ToArray())
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .ToArray() :
                new string[0];

            paths = pathsInFolders
                .Union(paths.Where(p => !AssetDatabase.IsValidFolder(p)))
                .Where(p => !(p.EndsWith(".cs") || p.EndsWith(".unity") || p.EndsWith(".hlsl") || p.EndsWith(".colors")))
                .ToArray();

            var dependencies = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .SelectMany(p => 
                    EditorUtility.CollectDependencies(new[] { AssetDatabase.LoadAssetAtPath(p, typeof(UnityObject)) })
                        .Select(o => AssetDatabase.GetAssetPath(o))
                        .Where(pp => pp != p))
                .Distinct()
                .ToArray();

            var unused = paths.Where(p => !dependencies.Contains(p)).ToArray();

            var window = GetWindow<DependencyWindow>();
            window._objects = 
                unused
                    .Select(p => AssetDatabase.LoadAssetAtPath(p, typeof(UnityObject)))
                    .ToArray();
            window.Show();
        }

        void OnGUI()
        {
            if (null == _objects)
                return;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach(var o in _objects)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(o, typeof(UnityObject), false);
                if(GUILayout.Button("Delete", GUILayout.Width(100)))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(o));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        internal static void OnDeleteAssets (string[] deletedAssets)
        {
            if (!HasOpenInstances<DependencyWindow>())
                return;

            var window = GetWindow<DependencyWindow>();
            if (window._objects != null)
                window._objects = window._objects.Where(o => !deletedAssets.Contains(AssetDatabase.GetAssetPath(o))).ToArray();
        }

        private static string GetGameObjectPath (Transform transform)
        {
            if (null == transform)
                return "";

            return GetGameObjectPath(transform.parent) + "/" + transform.name;
        }


        private static string GetFullPath (UnityObject obj)
        {
            var assetpath = AssetDatabase.GetAssetPath(obj);

            if (obj is GameObject gameObject)
                assetpath = assetpath + GetGameObjectPath(gameObject.transform);
            else if (obj is Transform transform)
                assetpath = assetpath + GetGameObjectPath(transform);
            else if (obj is Component component)
                assetpath = assetpath + GetGameObjectPath(component.transform) + "/" + component.GetType();

            return assetpath;
        }
    }
}
