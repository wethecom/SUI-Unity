using RoslynCSharp.Compiler;
using UnityEditor.Compilation;
using UnityEngine;

namespace RoslynCSharp.Example
{
#pragma warning disable 0219

    internal class Ex20_OutputLocation
    {
        private ScriptDomain domain = null;
        private const string sourceFile = "path/to/source/file.cs";

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Start()
        {
            // Create domain
            domain = ScriptDomain.CreateDomain("Example Domain");
            
            // Setup output location
            domain.RoslynCompilerService.OutputDirectory = "Assemblies";    // <project_path>/Assemblies
            domain.RoslynCompilerService.OutputName = "MyAssembly";

            bool originalGenerateInMemory = RoslynCSharp.Settings.GenerateInMemory;

            // Need to disable generate in memory in order to produce an output assembly.
            // This can be done via the settings menu too (Note this will change the value in settings also)
            RoslynCSharp.Settings.GenerateInMemory = false;

            // Compile and load code
            ScriptAssembly assembly = domain.CompileAndLoadFile(sourceFile, ScriptSecurityMode.UseSettings);

            // Reset settings asset
            RoslynCSharp.Settings.GenerateInMemory = originalGenerateInMemory;

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
