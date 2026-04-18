using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace RoslynCSharp.Editor
{
    internal static class AssemblyReferenceUtility
    {
        // Methods
        public static void ShowFileAssembleSelectionDialog(Action<string> onSelected)
        {
            // Show the file select dialog
            string path = EditorUtility.OpenFilePanel("Open Assembly File", "Assets", "dll");

            if (string.IsNullOrEmpty(path) == false)
            {
                // Check for file exists
                if (File.Exists(path) == false)
                {
                    Debug.LogError("Assembly file does not exist: " + path);
                    return;
                }

                // Use relative path if possible
                string relativePath = path.Replace('\\', '/');
                relativePath = FileUtil.GetProjectRelativePath(relativePath);

                if (string.IsNullOrEmpty(relativePath) == false && File.Exists(relativePath) == true)
                    path = relativePath;

                // Get the path
                if(onSelected != null)
                    onSelected(path);
            }
        }

        public static void ShowLoadedAssemblySelectionContextMenu(Action<Assembly> onSelected)
        {
            GenericMenu menu = new GenericMenu();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string menuName = asm.FullName;

                // Check for Unity assemblies
                if (menuName.StartsWith("Unity") == true)
                {
                    menuName = "Unity Assemblies/" + menuName;
                }
                // Check for system assemblies
                else if (menuName.StartsWith("System") == true || menuName.StartsWith("mscorlib") == true || menuName.StartsWith("netstandard") == true)
                {
                    menuName = "System Assemblies/" + menuName;
                }
                // Check for roslyn assemblies
                else if(menuName.StartsWith("Roslyn") == true || menuName.StartsWith("Trivial") == true)
                {
                    menuName = "Roslyn Assemblies/" + menuName;
                }

                        
                // Add an item
                menu.AddItem(new GUIContent(menuName), false, (object value) =>
                {
                    // Get the selected assembly
                    Assembly selectedAsm = (Assembly)value;

                    // Trigger event
                    if(onSelected != null)
                        onSelected(selectedAsm);
                }, asm);
            }

            // SHow the menu
            menu.ShowAsContext();
        }
    }
}
