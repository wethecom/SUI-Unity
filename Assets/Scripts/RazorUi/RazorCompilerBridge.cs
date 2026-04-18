using System;
using System.Reflection;

namespace SUI.RazorUi
{
public static class RazorCompilerBridge
{
    private const string ProcessorTypeName = "Sandbox.Razor.RazorProcessor, Sandbox.Razor";
    private static readonly Type ProcessorType = Type.GetType(ProcessorTypeName, throwOnError: false);
    private static readonly MethodInfo GenerateMethod = ProcessorType?.GetMethod(
        "GenerateFromSource",
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        types: new[] { typeof(string), typeof(string), typeof(string), typeof(bool) },
        modifiers: null);

    public static bool IsAvailable => GenerateMethod != null;

    public static bool TryGenerate(string razorSource, string fileName, string rootNamespace, out string generatedCode, out string error)
    {
        generatedCode = string.Empty;
        error = string.Empty;

        if (!IsAvailable)
        {
            error = "Sandbox.Razor assembly is not loaded. Run Tools/Sync-SandboxRazor.ps1 to copy DLLs into Assets/Plugins/SandboxRazor.";
            return false;
        }

        try
        {
            var result = GenerateMethod.Invoke(null, new object[] { razorSource, fileName, rootNamespace, true });
            generatedCode = result as string ?? string.Empty;
            return true;
        }
        catch (TargetInvocationException tie)
        {
            error = tie.InnerException?.Message ?? tie.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
}

