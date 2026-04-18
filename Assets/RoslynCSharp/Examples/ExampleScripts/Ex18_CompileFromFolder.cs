using UnityEngine;
using RoslynCSharp.Compiler;// Required for access to compiler types such as 'CompilationError'
using System.IO;

namespace RoslynCSharp.Example
{
#pragma warning disable 0219

    /// <summary>
    /// An example script that shows how to use the compiler service to compile and load a folder containing one or more C# source files. This method has the added benefits of reporting the file name in any compiler errors.
    /// </summary>
    public class Ex18_CompileFromFolder : MonoBehaviour
    {
        private ScriptDomain domain = null;
        private const string sourceFolder = "some/folder/path";

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Start()
        {
            // Create domain
            domain = ScriptDomain.CreateDomain("Example Domain");


            // Compile and load code - include all source files in sub folders
            ScriptAssembly assembly = domain.CompileAndLoadDirectory(sourceFolder, "*.cs", SearchOption.AllDirectories, ScriptSecurityMode.UseSettings);


            // Check for compiler errors
            if (domain.CompileResult.Success == false)
            {
                // Get all errors
                foreach (CompilationError error in domain.CompileResult.Errors)
                {
                    if (error.IsError == true)
                    {
                        Debug.LogError(error.ToString());
                    }
                    else if (error.IsWarning == true)
                    {
                        Debug.LogWarning(error.ToString());
                    }
                }
            }
        }
    }
}