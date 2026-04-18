using UnityEngine;

namespace SUI.RazorUi
{
public class RazorCompileExample : MonoBehaviour
{
    [Header("Drop a .razor file here as TextAsset")]
    [SerializeField] private TextAsset razorFile;

    [SerializeField] private string rootNamespace = "Game.UI";

    [TextArea(8, 20)]
    [SerializeField] private string generatedPreview;

    [ContextMenu("Compile Razor")]
    public void CompileNow()
    {
        if (razorFile == null)
        {
            Debug.LogWarning("RazorCompileExample: assign a TextAsset (.razor) first.");
            return;
        }

        if (!RazorCompilerBridge.TryGenerate(razorFile.text, razorFile.name + ".razor", rootNamespace, out var generated, out var error))
        {
            Debug.LogError("Razor compile failed: " + error);
            return;
        }

        generatedPreview = generated;
        Debug.Log($"Razor compile succeeded for {razorFile.name}. Generated C# length: {generated.Length}");
    }
}
}

