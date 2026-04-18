using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace SUI.RazorUi
{
public class RazorUiDocumentView : MonoBehaviour
{
    [Serializable]
    public struct ValueBinding
    {
        public string key;
        public string value;
    }

    [Serializable]
    public struct ActionBinding
    {
        public string actionName;
        public UnityEvent onClick;
    }

    [Header("Razor")]
    [SerializeField] private TextAsset razorFile;
    [SerializeField] private string rootNamespace = "Game.UI";
    [SerializeField] private bool compileWithSandboxRazor = true;

    [Header("Bindings")]
    [SerializeField] private List<ValueBinding> values = new List<ValueBinding>();
    [SerializeField] private List<ActionBinding> actions = new List<ActionBinding>();

    [Header("Output")]
    [SerializeField] private UIDocument uiDocument;
    [TextArea(6, 20)]
    [SerializeField] private string generatedCSharp;
    [SerializeField] private string lastError;

    private readonly Dictionary<string, string> valueMap = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, Action> actionMap = new Dictionary<string, Action>(StringComparer.Ordinal);

    private void Reset()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        RenderNow();
    }

    [ContextMenu("Render Razor UI")]
    public void RenderNow()
    {
        lastError = string.Empty;

        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            lastError = "RazorUiDocumentView requires a UIDocument on the same GameObject.";
            Debug.LogError(lastError);
            return;
        }

        if (razorFile == null)
        {
            lastError = "Assign a .razor TextAsset first.";
            Debug.LogWarning(lastError);
            uiDocument.rootVisualElement?.Clear();
            return;
        }

        BuildBindingMaps();

        if (compileWithSandboxRazor)
        {
            if (RazorCompilerBridge.TryGenerate(razorFile.text, razorFile.name + ".razor", rootNamespace, out var generated, out var compileError))
            {
                generatedCSharp = generated;
            }
            else
            {
                lastError = "Razor compile failed: " + compileError;
                Debug.LogWarning(lastError);
            }
        }

        if (!RazorUiToolkitRenderer.TryRender(razorFile.text, uiDocument.rootVisualElement, valueMap, actionMap, out var renderError))
        {
            lastError = "Razor render failed: " + renderError;
            Debug.LogError(lastError);
            return;
        }

        Debug.Log($"Rendered Razor UI from {razorFile.name} with {valueMap.Count} values and {actionMap.Count} actions.");
    }

    private void BuildBindingMaps()
    {
        valueMap.Clear();
        actionMap.Clear();

        foreach (var binding in values)
        {
            if (string.IsNullOrWhiteSpace(binding.key))
            {
                continue;
            }

            valueMap[binding.key] = binding.value ?? string.Empty;
        }

        foreach (var binding in actions)
        {
            if (string.IsNullOrWhiteSpace(binding.actionName))
            {
                continue;
            }

            var actionName = binding.actionName;
            actionMap[actionName] = () => binding.onClick?.Invoke();
        }
    }
}
}

