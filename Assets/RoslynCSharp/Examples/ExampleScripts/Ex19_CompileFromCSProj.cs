using RoslynCSharp.Compiler;
using RoslynCSharp.Project;
using UnityEngine;

namespace RoslynCSharp.Example
{
#pragma warning disable 0219

    /// <summary>
    /// An example script that shows how to use the compiler service to compile and load a C# project file. This method has the added benefits of reporting the file name in any compiler errors.
    /// </summary>
    public class Ex19_CompileFromCSProj : MonoBehaviour
    {
        private ScriptDomain domain = null;
        private const string projectFile = "path/to/source/example.csproj";

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Start()
        {
            // Create domain
            domain = ScriptDomain.CreateDomain("Example Domain");


            // Load csproj including source files, references, defines and output info
            CSharpProject project = CSharpProject.ParseText(projectFile);

            // Compile and load code
            ScriptAssembly assembly = domain.CompileAndLoadCSharpProject(project, ScriptSecurityMode.UseSettings);


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
