using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using SUI.RazorUi;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SUI.Runtime
{
public enum SuiBindingValueType
{
    String = 0,
    Bool = 1,
    Int = 2,
    Float = 3
}

public class SuiRuntimeHost : MonoBehaviour
{
    [Serializable]
    public struct ValueBinding
    {
        public string key;
        public SuiBindingValueType valueType;
        public string value;
    }

    [Serializable]
    public struct ActionBinding
    {
        public string actionName;
        public UnityEngine.Events.UnityEvent onTriggered;
    }

    [Header("Source")]
    [SerializeField] private UnityEngine.Object razorFile;
    [SerializeField] private UnityEngine.Object styleSheetFile;
    [SerializeField] private string rootNamespace = "Game.UI";
    [SerializeField] private bool compileWithSandboxRazor = true;

    [Header("Bindings")]
    [SerializeField] private List<ValueBinding> values = new List<ValueBinding>();
    [SerializeField] private List<ActionBinding> actions = new List<ActionBinding>();

    [Header("Script Bridge")]
    [SerializeField] private bool includeSiblingMonoBehaviours = true;
    [SerializeField] private List<MonoBehaviour> scriptTargets = new List<MonoBehaviour>();

    [Header("Panel")]
    [SerializeField] private SuiPanel panel = default;

    [Header("Modal")]
    [SerializeField] private bool enableModalBehavior = true;
    [SerializeField] private bool closeModalOnBackdropClick = false;
    [SerializeField] private Color defaultModalBackdropColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Debug")]
    [TextArea(6, 20)]
    [SerializeField] private string generatedCSharp;
    [SerializeField] private string lastError;
    [SerializeField] private bool propagateControlStateToTokens = true;

    [Header("Diagnostics")]
    [SerializeField] private bool enablePerformanceCounters = false;
    [SerializeField] private int documentRebuildCount;
    [SerializeField] private int layoutPassCount;
    [SerializeField] private int renderPassCount;
    [SerializeField] private double lastDocumentRebuildMs;

    private readonly Dictionary<string, string> tokenMap = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, Action> actionMap = new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly Dictionary<string, SuiBindingValueType> bindingTypeMap = new Dictionary<string, SuiBindingValueType>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> textStateMap = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> boolStateMap = new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> intStateMap = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> floatStateMap = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<int> invalidNodeIdSet = new HashSet<int>();
    private SuiStyleSheet parsedStyleSheet;
    private int styleSheetContentHash;

    private readonly List<SuiNode> documentNodes = new List<SuiNode>();
    private readonly List<string> focusOrder = new List<string>();
    private readonly Dictionary<string, SuiNode> focusNodeMap = new Dictionary<string, SuiNode>(StringComparer.Ordinal);
    private readonly Dictionary<int, SuiNode> nodeByIdMap = new Dictionary<int, SuiNode>();
    private readonly Dictionary<string, Vector2> windowPositionOverrides = new Dictionary<string, Vector2>(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> windowOpenOverrides = new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> scrollYOverrides = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, ScriptActionBinding> scriptActionMap = new Dictionary<string, ScriptActionBinding>(StringComparer.Ordinal);
    private SuiImmediateRenderer immediateRenderer;
    private bool documentDirty = true;
    private bool layoutDirty = true;
    private Rect cachedRootRect;
    private Vector2Int cachedScreenSize;
    private string pendingFocusControlName;
    private string focusedControlName;
    private int hoveredMouseNodeId = -1;
    private int pressedMouseNodeId = -1;
    private int hoveredPseudoNodeId = -1;
    private int activePseudoNodeId = -1;
    private int draggingWindowNodeId = -1;
    private string draggingWindowKey;
    private Vector2 draggingWindowPointerOffset;
    private int pendingDragNodeId = -1;
    private Vector2 pendingDragPointerStart;
    private int draggingUiNodeId = -1;
    private int dragHoverDropNodeId = -1;

    private struct ScriptActionBinding
    {
        public MonoBehaviour Target;
        public MethodInfo Method;
    }

    private void Awake()
    {
        if (panel.size == default)
        {
            panel = SuiPanel.Default;
        }

        BuildMaps();
        immediateRenderer = CreateRenderer();
        cachedScreenSize = new Vector2Int(Screen.width, Screen.height);
        cachedRootRect = panel.Resolve(Screen.width, Screen.height);
    }

    private void OnEnable()
    {
        MarkDocumentDirty();
    }

    [ContextMenu("Rebuild SUI Document")]
    public void RebuildDocument()
    {
        MarkDocumentDirty();
    }

    [ContextMenu("Reset Perf Counters")]
    public void ResetPerfCounters()
    {
        documentRebuildCount = 0;
        layoutPassCount = 0;
        renderPassCount = 0;
        lastDocumentRebuildMs = 0;
    }

    private void Update()
    {
        if (immediateRenderer == null)
        {
            immediateRenderer = CreateRenderer();
        }

        EnsureUpToDate();
    }

    private void EnsureUpToDate()
    {
        var cssHash = GetStyleSheetHash();
        if (cssHash != styleSheetContentHash)
        {
            styleSheetContentHash = cssHash;
            MarkDocumentDirty();
        }

        var currentScreen = new Vector2Int(Screen.width, Screen.height);
        if (currentScreen != cachedScreenSize)
        {
            cachedScreenSize = currentScreen;
            layoutDirty = true;
        }

        var currentRoot = panel.Resolve(Screen.width, Screen.height);
        if (currentRoot != cachedRootRect)
        {
            cachedRootRect = currentRoot;
            layoutDirty = true;
        }

        if (documentDirty)
        {
            RebuildDocumentNow();
        }

        if (UpdateRuntimeVisibility())
        {
            RebuildFocusOrder();
            layoutDirty = true;
        }

        if (layoutDirty && documentNodes.Count > 0)
        {
            SuiLayoutEngine.Layout(documentNodes, cachedRootRect);
            ApplyWindowPositionOverrides();
            ApplyScrollOffsets();
            layoutDirty = false;
            if (enablePerformanceCounters)
            {
                layoutPassCount++;
            }
        }
    }

    private void RebuildDocumentNow()
    {
        var timer = default(Stopwatch);
        if (enablePerformanceCounters)
        {
            timer = Stopwatch.StartNew();
        }

        lastError = string.Empty;
        var razorText = GetAssetText(razorFile);
        if (string.IsNullOrWhiteSpace(razorText))
        {
            lastError = "Assign a Razor source asset (.razor, .txt, or TextAsset).";
            documentNodes.Clear();
            focusOrder.Clear();
            focusNodeMap.Clear();
            nodeByIdMap.Clear();
            hoveredMouseNodeId = -1;
            pressedMouseNodeId = -1;
            hoveredPseudoNodeId = -1;
            activePseudoNodeId = -1;
            draggingWindowNodeId = -1;
            draggingWindowKey = null;
            documentDirty = false;
            layoutDirty = false;
            return;
        }

        BuildMaps();

        if (compileWithSandboxRazor)
        {
            var fileName = razorFile != null ? razorFile.name + ".razor" : "Document.razor";
            if (RazorCompilerBridge.TryGenerate(razorText, fileName, rootNamespace, out var generated, out var error))
            {
                generatedCSharp = generated;
            }
            else
            {
                lastError = "Razor compile warning: " + error;
            }
        }

        try
        {
            documentNodes.Clear();
            EnsureStyleSheetParsed();
            var parsed = SuiMarkupParser.Parse(razorText, tokenMap, parsedStyleSheet);
            documentNodes.AddRange(parsed);
            RebuildFocusOrder();
            RebuildNodeLookup();
            immediateRenderer?.ClearTransientCaches();
            documentDirty = false;
            layoutDirty = true;

            if (enablePerformanceCounters)
            {
                timer.Stop();
                documentRebuildCount++;
                lastDocumentRebuildMs = timer.Elapsed.TotalMilliseconds;
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            documentNodes.Clear();
            focusOrder.Clear();
            focusNodeMap.Clear();
            nodeByIdMap.Clear();
            hoveredMouseNodeId = -1;
            pressedMouseNodeId = -1;
            hoveredPseudoNodeId = -1;
            activePseudoNodeId = -1;
            draggingWindowNodeId = -1;
            draggingWindowKey = null;
            documentDirty = false;
            layoutDirty = false;

            if (enablePerformanceCounters)
            {
                timer.Stop();
                lastDocumentRebuildMs = timer.Elapsed.TotalMilliseconds;
            }
        }
    }

    private void OnGUI()
    {
        var activeModal = GetActiveModalWindow();
        HandleKeyboardFocus(Event.current, activeModal);
        HandleMouseEvents(Event.current, activeModal);

        if (documentNodes.Count == 0)
        {
            return;
        }

        DrawModalBackdrop(activeModal);
        immediateRenderer.Render(documentNodes);
        ApplyPendingFocus();
        SyncFocusedControl();

        if (enablePerformanceCounters)
        {
            renderPassCount++;
        }
    }

    private void BuildMaps()
    {
        tokenMap.Clear();
        actionMap.Clear();
        scriptActionMap.Clear();
        bindingTypeMap.Clear();

        foreach (var entry in values)
        {
            if (string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            var key = entry.key.Trim();
            var rawValue = entry.value ?? string.Empty;
            var valueType = entry.valueType;
            bindingTypeMap[key] = valueType;

            switch (valueType)
            {
                case SuiBindingValueType.Bool:
                    if (!boolStateMap.ContainsKey(key))
                    {
                        boolStateMap[key] = ParseBool(rawValue, false);
                    }

                    textStateMap[key] = boolStateMap[key] ? "true" : "false";
                    break;
                case SuiBindingValueType.Int:
                    if (!intStateMap.ContainsKey(key))
                    {
                        intStateMap[key] = ParseInt(rawValue, 0);
                    }

                    textStateMap[key] = intStateMap[key].ToString(CultureInfo.InvariantCulture);
                    break;
                case SuiBindingValueType.Float:
                    if (!floatStateMap.ContainsKey(key))
                    {
                        floatStateMap[key] = ParseFloat(rawValue, 0f);
                    }

                    textStateMap[key] = floatStateMap[key].ToString("0.###", CultureInfo.InvariantCulture);
                    break;
                default:
                    if (!textStateMap.ContainsKey(key))
                    {
                        textStateMap[key] = rawValue;
                    }

                    break;
            }
        }

        foreach (var pair in textStateMap)
        {
            tokenMap[pair.Key] = pair.Value;
        }

        foreach (var pair in boolStateMap)
        {
            tokenMap[pair.Key] = pair.Value ? "true" : "false";
        }

        foreach (var pair in intStateMap)
        {
            tokenMap[pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var pair in floatStateMap)
        {
            tokenMap[pair.Key] = pair.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        foreach (var entry in actions)
        {
            if (string.IsNullOrWhiteSpace(entry.actionName))
            {
                continue;
            }

            var actionName = entry.actionName;
            actionMap[actionName] = () =>
            {
                entry.onTriggered?.Invoke();
                MarkDocumentDirty();
            };
        }

        BuildScriptActionMap();
    }

    public void SetToken(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        tokenMap[key] = value;
        textStateMap[key] = value;
        ApplyBindingTypeFromText(key, value);
        MarkDocumentDirty();
    }

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (intStateMap.TryGetValue(key, out value))
        {
            return true;
        }

        if (textStateMap.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            intStateMap[key] = value;
            return true;
        }

        return false;
    }

    public bool TryGetFloat(string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (floatStateMap.TryGetValue(key, out value))
        {
            return true;
        }

        if (textStateMap.TryGetValue(key, out var raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            floatStateMap[key] = value;
            return true;
        }

        return false;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        if (boolStateMap.TryGetValue(key, out var value))
        {
            return value;
        }

        if (textStateMap.TryGetValue(key, out var raw))
        {
            return ParseBool(raw, fallback);
        }

        return fallback;
    }

    public string GetString(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        return textStateMap.TryGetValue(key, out var value) ? value : fallback;
    }

    public void SetInt(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        intStateMap[key] = value;
        textStateMap[key] = value.ToString(CultureInfo.InvariantCulture);
        UpdateTokenForKey(key);
        MarkDocumentDirty();
    }

    public void SetFloat(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        floatStateMap[key] = value;
        textStateMap[key] = value.ToString("0.###", CultureInfo.InvariantCulture);
        UpdateTokenForKey(key);
        MarkDocumentDirty();
    }

    public void SetBool(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        boolStateMap[key] = value;
        textStateMap[key] = value ? "true" : "false";
        UpdateTokenForKey(key);
        MarkDocumentDirty();
    }

    public void OpenModal(string idOrName)
    {
        SetModalOpen(idOrName, true);
    }

    public void CloseModal(string idOrName)
    {
        SetModalOpen(idOrName, false);
    }

    public void SetModalOpen(string idOrName, bool isOpen)
    {
        if (!TryResolveWindowKey(idOrName, out var key))
        {
            return;
        }

        windowOpenOverrides[key] = isOpen;
        if (!isOpen)
        {
            pendingFocusControlName = string.Empty;
            focusedControlName = string.Empty;
        }

        layoutDirty = true;
    }

    public void CloseTopModal()
    {
        var activeModal = GetActiveModalWindow();
        if (activeModal == null)
        {
            return;
        }

        var key = GetWindowKey(activeModal);
        if (!string.IsNullOrWhiteSpace(key))
        {
            windowOpenOverrides[key] = false;
        }

        InvokeNodeAction(activeModal.GetAttribute("onclose"));
        pendingFocusControlName = string.Empty;
        focusedControlName = string.Empty;
        layoutDirty = true;
    }

    public bool IsModalOpen(string idOrName)
    {
        if (!TryResolveWindowKey(idOrName, out var key))
        {
            return false;
        }

        if (windowOpenOverrides.TryGetValue(key, out var overrideOpen))
        {
            return overrideOpen;
        }

        if (TryFindWindowNodeByKey(key, out var windowNode))
        {
            return IsWindowOpen(windowNode);
        }

        return false;
    }

    private SuiImmediateRenderer CreateRenderer()
    {
        return new SuiImmediateRenderer(
            actionMap,
            GetTextState,
            SetTextState,
            GetBoolState,
            SetBoolState,
            GetControlName,
            ResolveNodeStyle,
            TryInvokeMappedOrScriptAction);
    }

    private string GetTextState(string key)
    {
        return textStateMap.TryGetValue(key, out var value) ? value : null;
    }

    private void SetTextState(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = value ?? string.Empty;
        textStateMap[key] = normalized;
        ApplyBindingTypeFromText(key, normalized);
        if (propagateControlStateToTokens)
        {
            UpdateTokenForKey(key);
            MarkDocumentDirty();
        }
    }

    private bool GetBoolState(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && boolStateMap.TryGetValue(key, out var value) && value;
    }

    private void SetBoolState(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        boolStateMap[key] = value;
        textStateMap[key] = value ? "true" : "false";
        if (propagateControlStateToTokens)
        {
            UpdateTokenForKey(key);
            MarkDocumentDirty();
        }
    }

    private void MarkDocumentDirty()
    {
        documentDirty = true;
        layoutDirty = true;
    }

    private SuiStyle ResolveNodeStyle(SuiNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (parsedStyleSheet == null)
        {
            return node.Style;
        }

        var style = node.Style.Clone();
        var isDisabled = node.IsDisabled;
        var isInvalid = !isDisabled && IsNodeInvalid(node);
        UpdateNodeValidationAttribute(node, isInvalid);
        var isHover = !isDisabled && node.NodeId == hoveredPseudoNodeId;
        var isActive = !isDisabled && node.NodeId == activePseudoNodeId;
        var isFocus = !isDisabled && IsNodeFocused(node);
        parsedStyleSheet.ApplyPseudoDeclarations(node.Tag, node.Attributes, isHover, isActive, isFocus, isDisabled, isInvalid, style);
        return style;
    }

    private void EnsureStyleSheetParsed()
    {
        var cssText = GetAssetText(styleSheetFile);
        if (string.IsNullOrWhiteSpace(cssText))
        {
            parsedStyleSheet = null;
            return;
        }

        parsedStyleSheet = SuiStyleSheet.Parse(cssText);
    }

    private int GetStyleSheetHash()
    {
        var cssText = GetAssetText(styleSheetFile);
        if (string.IsNullOrEmpty(cssText))
        {
            return 0;
        }

        return cssText.GetHashCode();
    }

    private string GetControlName(SuiNode node)
    {
        return $"sui_{node.NodeId}";
    }

    private void RebuildFocusOrder()
    {
        focusOrder.Clear();
        focusNodeMap.Clear();
        for (var i = 0; i < documentNodes.Count; i++)
        {
            CollectFocusable(documentNodes[i]);
        }

        if (!string.IsNullOrWhiteSpace(focusedControlName) && focusOrder.Contains(focusedControlName))
        {
            pendingFocusControlName = focusedControlName;
        }
    }

    private void RebuildNodeLookup()
    {
        nodeByIdMap.Clear();
        for (var i = 0; i < documentNodes.Count; i++)
        {
            CollectNodeLookup(documentNodes[i]);
        }
    }

    private void CollectNodeLookup(SuiNode node)
    {
        if (node == null)
        {
            return;
        }

        nodeByIdMap[node.NodeId] = node;
        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectNodeLookup(node.Children[i]);
        }
    }

    private void CollectFocusable(SuiNode node)
    {
        if (node == null || !node.RuntimeVisible)
        {
            return;
        }

        if (node.IsFocusable && !node.IsDisabled)
        {
            var controlName = GetControlName(node);
            focusOrder.Add(controlName);
            focusNodeMap[controlName] = node;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectFocusable(node.Children[i]);
        }
    }

    private void HandleKeyboardFocus(Event evt, SuiNode activeModal)
    {
        if (evt == null || evt.type != EventType.KeyDown)
        {
            return;
        }

        if (focusOrder.Count == 0)
        {
            return;
        }

        if (evt.keyCode == KeyCode.Tab)
        {
            var direction = evt.shift ? -1 : 1;
            MoveFocus(direction, activeModal);
            evt.Use();
            return;
        }

        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            if (ActivateFocusedControl(activeModal))
            {
                evt.Use();
            }
            return;
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            if (activeModal != null)
            {
                InvokeNodeAction(activeModal.GetAttribute("onclose"), activeModal.GetAttribute("onescape"));
            }

            pendingFocusControlName = string.Empty;
            focusedControlName = string.Empty;
            evt.Use();
        }
    }

    private void MoveFocus(int direction, SuiNode activeModal)
    {
        var scopedFocusOrder = GetScopedFocusOrder(activeModal);
        if (scopedFocusOrder.Count == 0)
        {
            return;
        }

        var currentName = GUI.GetNameOfFocusedControl();
        var index = scopedFocusOrder.IndexOf(currentName);
        if (index < 0)
        {
            index = scopedFocusOrder.IndexOf(focusedControlName);
        }

        if (index < 0)
        {
            pendingFocusControlName = scopedFocusOrder[0];
            focusedControlName = pendingFocusControlName;
            return;
        }

        var next = (index + direction + scopedFocusOrder.Count) % scopedFocusOrder.Count;
        pendingFocusControlName = scopedFocusOrder[next];
        focusedControlName = pendingFocusControlName;
    }

    private void ApplyPendingFocus()
    {
        if (pendingFocusControlName == null)
        {
            return;
        }

        if (pendingFocusControlName.Length == 0)
        {
            GUI.FocusControl(string.Empty);
        }
        else
        {
            GUI.FocusControl(pendingFocusControlName);
        }

        pendingFocusControlName = null;
    }

    private void SyncFocusedControl()
    {
        var current = GUI.GetNameOfFocusedControl();
        if (!string.IsNullOrWhiteSpace(current))
        {
            focusedControlName = current;
        }
    }

    private bool ActivateFocusedControl(SuiNode activeModal)
    {
        if (focusNodeMap.Count == 0)
        {
            return false;
        }

        var currentName = GUI.GetNameOfFocusedControl();
        if (string.IsNullOrWhiteSpace(currentName))
        {
            currentName = focusedControlName;
        }

        if (string.IsNullOrWhiteSpace(currentName) || !focusNodeMap.TryGetValue(currentName, out var node) || node == null)
        {
            return false;
        }

        if (activeModal != null && !IsDescendantOrSelf(node, activeModal))
        {
            return false;
        }

        if (node.IsDisabled)
        {
            return false;
        }

        if (node.IsButton)
        {
            return InvokeNodeAction(node.GetAttribute("onclick"), node.GetAttribute("@onclick"));
        }

        if (node.IsInput || node.IsCheckbox)
        {
            return InvokeNodeAction(node.GetAttribute("onsubmit"), node.GetAttribute("onchange"), node.GetAttribute("oninput"));
        }

        return false;
    }

    private bool InvokeNodeAction(params string[] candidateNames)
    {
        if (candidateNames == null)
        {
            return false;
        }

        for (var i = 0; i < candidateNames.Length; i++)
        {
            var raw = candidateNames[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var actionName = raw.Trim('"', '\'', '{', '}', ' ');
            if (TryInvokeMappedOrScriptAction(actionName))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleMouseEvents(Event evt, SuiNode activeModal)
    {
        if (evt == null || documentNodes.Count == 0)
        {
            return;
        }

        // Keep hover pseudo-state responsive even when Unity doesn't emit
        // continuous MouseMove events in Game view.
        var pseudoTarget = FindTopmostNode(evt.mousePosition, activeModal);
        hoveredPseudoNodeId = pseudoTarget?.NodeId ?? -1;

        var isMouseEvent =
            evt.type == EventType.MouseMove ||
            evt.type == EventType.MouseDrag ||
            evt.type == EventType.MouseDown ||
            evt.type == EventType.MouseUp ||
            evt.type == EventType.ScrollWheel;

        if (!isMouseEvent)
        {
            return;
        }

        var target = FindTopmostMouseEventNode(evt.mousePosition, activeModal);
        var currentHoverId = target?.NodeId ?? -1;

        var genericTarget = FindTopmostNode(evt.mousePosition, activeModal);

        if (target != null && target.IsDisabled)
        {
            target = null;
            currentHoverId = -1;
        }

        if (genericTarget != null && genericTarget.IsDisabled)
        {
            genericTarget = null;
        }

        var pointerOutsideModal = activeModal != null && !activeModal.LayoutRect.Contains(evt.mousePosition);
        if (pointerOutsideModal)
        {
            hoveredPseudoNodeId = -1;
            currentHoverId = -1;
            target = null;
            genericTarget = null;
        }

        if (currentHoverId != hoveredMouseNodeId)
        {
            if (hoveredMouseNodeId >= 0 && nodeByIdMap.TryGetValue(hoveredMouseNodeId, out var previous))
            {
                InvokeNodeAction(previous.GetAttribute("onmouseleave"));
            }

            if (target != null)
            {
                InvokeNodeAction(target.GetAttribute("onmouseenter"));
            }

            hoveredMouseNodeId = currentHoverId;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            if (pointerOutsideModal)
            {
                if (closeModalOnBackdropClick && activeModal != null)
                {
                    InvokeNodeAction(activeModal.GetAttribute("onclose"), activeModal.GetAttribute("onbackdropclick"));
                }

                evt.Use();
                return;
            }

            var draggableWindow = FindDraggableWindow(genericTarget);
            if (draggableWindow != null)
            {
                BeginWindowDrag(draggableWindow, evt.mousePosition);
                evt.Use();
                return;
            }

            var draggableNode = FindDraggableNode(genericTarget);
            if (draggableNode != null)
            {
                BeginPendingUiDrag(draggableNode, evt.mousePosition);
                evt.Use();
                return;
            }

            activePseudoNodeId = hoveredPseudoNodeId;
            pressedMouseNodeId = target?.NodeId ?? -1;
            if (target != null && InvokeNodeAction(target.GetAttribute("onmousedown")))
            {
                evt.Use();
            }
            return;
        }

        if (evt.type == EventType.ScrollWheel)
        {
            var scrollNode = FindNearestScrollContainer(genericTarget);
            if (scrollNode != null)
            {
                var delta = evt.delta.y;
                var speed = ParseFloatOrDefault(scrollNode.GetAttribute("scroll-speed"), 24f);
                ScrollNodeBy(scrollNode, delta * Mathf.Max(1f, speed));
                evt.Use();
                return;
            }
        }

        if (evt.type == EventType.MouseDrag)
        {
            if (TryPromotePendingDrag(evt.mousePosition))
            {
                evt.Use();
                return;
            }

            if (draggingUiNodeId >= 0)
            {
                UpdateUiDrag(evt.mousePosition, activeModal);
                evt.Use();
                return;
            }
        }

        if (evt.type == EventType.MouseDrag && draggingWindowNodeId >= 0)
        {
            if (nodeByIdMap.TryGetValue(draggingWindowNodeId, out var dragWindow) && dragWindow != null)
            {
                if (activeModal != null && !IsDescendantOrSelf(dragWindow, activeModal))
                {
                    draggingWindowNodeId = -1;
                    draggingWindowKey = null;
                    evt.Use();
                    return;
                }

                var newPos = evt.mousePosition - draggingWindowPointerOffset;
                SetWindowPositionOverride(dragWindow, newPos);
                evt.Use();
                return;
            }
        }

        if (evt.type == EventType.MouseUp && evt.button == 0)
        {
            if (pendingDragNodeId >= 0 || draggingUiNodeId >= 0)
            {
                EndUiDrag(evt.mousePosition, activeModal);
                evt.Use();
                return;
            }

            draggingWindowNodeId = -1;
            draggingWindowKey = null;
            activePseudoNodeId = -1;
            var used = false;
            if (target != null)
            {
                used = InvokeNodeAction(target.GetAttribute("onmouseup"));
                if (pressedMouseNodeId == target.NodeId)
                {
                    used = InvokeNodeAction(target.GetAttribute("onclick"), target.GetAttribute("@onclick")) || used;
                }
            }

            pressedMouseNodeId = -1;
            if (used)
            {
                evt.Use();
            }
        }
    }

    private SuiNode FindTopmostMouseEventNode(Vector2 mousePosition)
    {
        var rootOrder = BuildNodeOrderByZ(documentNodes, descending: true);
        for (var i = 0; i < rootOrder.Count; i++)
        {
            var found = FindTopmostMouseEventNode(documentNodes[rootOrder[i]], mousePosition);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private SuiNode FindTopmostMouseEventNode(Vector2 mousePosition, SuiNode scopeRoot)
    {
        if (scopeRoot != null)
        {
            return FindTopmostMouseEventNode(scopeRoot, mousePosition);
        }

        return FindTopmostMouseEventNode(mousePosition);
    }

    private SuiNode FindTopmostNode(Vector2 mousePosition)
    {
        var rootOrder = BuildNodeOrderByZ(documentNodes, descending: true);
        for (var i = 0; i < rootOrder.Count; i++)
        {
            var found = FindTopmostNode(documentNodes[rootOrder[i]], mousePosition);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private SuiNode FindTopmostNode(Vector2 mousePosition, SuiNode scopeRoot)
    {
        if (scopeRoot != null)
        {
            return FindTopmostNode(scopeRoot, mousePosition);
        }

        return FindTopmostNode(mousePosition);
    }

    private SuiNode FindTopmostNode(SuiNode node, Vector2 mousePosition)
    {
        if (node == null || !node.RuntimeVisible)
        {
            return null;
        }

        var canSearchChildren = !node.IsScrollContainer || GetContentRect(node).Contains(mousePosition);
        if (canSearchChildren)
        {
            var childOrder = BuildChildOrderByZ(node, descending: true);
            for (var i = 0; i < childOrder.Count; i++)
            {
                var found = FindTopmostNode(node.Children[childOrder[i]], mousePosition);
                if (found != null)
                {
                    return found;
                }
            }
        }

        if (node.LayoutRect.Contains(mousePosition))
        {
            return node;
        }

        return null;
    }

    private SuiNode FindTopmostMouseEventNode(SuiNode node, Vector2 mousePosition)
    {
        if (node == null || !node.RuntimeVisible)
        {
            return null;
        }

        var canSearchChildren = !node.IsScrollContainer || GetContentRect(node).Contains(mousePosition);
        if (canSearchChildren)
        {
            var childOrder = BuildChildOrderByZ(node, descending: true);
            for (var i = 0; i < childOrder.Count; i++)
            {
                var found = FindTopmostMouseEventNode(node.Children[childOrder[i]], mousePosition);
                if (found != null)
                {
                    return found;
                }
            }
        }

        if (!node.LayoutRect.Contains(mousePosition))
        {
            return null;
        }

        if (node.IsButton || node.IsInput || node.IsCheckbox)
        {
            return null;
        }

        if (HasMouseAction(node))
        {
            return node;
        }

        return null;
    }

    private static List<int> BuildChildOrderByZ(SuiNode node, bool descending)
    {
        var order = new List<int>(node.Children.Count);
        for (var i = 0; i < node.Children.Count; i++)
        {
            order.Add(i);
        }

        order.Sort((a, b) =>
        {
            var aNode = node.Children[a];
            var bNode = node.Children[b];
            var az = aNode != null && aNode.Style != null ? aNode.Style.ZIndex : 0;
            var bz = bNode != null && bNode.Style != null ? bNode.Style.ZIndex : 0;
            var cmp = az.CompareTo(bz);
            if (cmp == 0)
            {
                cmp = a.CompareTo(b);
            }

            return descending ? -cmp : cmp;
        });

        return order;
    }

    private static List<int> BuildNodeOrderByZ(IList<SuiNode> nodes, bool descending)
    {
        var order = new List<int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            order.Add(i);
        }

        order.Sort((a, b) =>
        {
            var aNode = nodes[a];
            var bNode = nodes[b];
            var az = aNode != null && aNode.Style != null ? aNode.Style.ZIndex : 0;
            var bz = bNode != null && bNode.Style != null ? bNode.Style.ZIndex : 0;
            var cmp = az.CompareTo(bz);
            if (cmp == 0)
            {
                cmp = a.CompareTo(b);
            }

            return descending ? -cmp : cmp;
        });

        return order;
    }

    private static bool HasMouseAction(SuiNode node)
    {
        if (node == null)
        {
            return false;
        }

        if (node.IsDisabled)
        {
            return false;
        }

        return
            !string.IsNullOrWhiteSpace(node.GetAttribute("onclick")) ||
            !string.IsNullOrWhiteSpace(node.GetAttribute("@onclick")) ||
            !string.IsNullOrWhiteSpace(node.GetAttribute("onmousedown")) ||
            !string.IsNullOrWhiteSpace(node.GetAttribute("onmouseup")) ||
            !string.IsNullOrWhiteSpace(node.GetAttribute("onmouseenter")) ||
            !string.IsNullOrWhiteSpace(node.GetAttribute("onmouseleave"));
    }

    private void BeginPendingUiDrag(SuiNode node, Vector2 pointerPosition)
    {
        if (node == null)
        {
            return;
        }

        pendingDragNodeId = node.NodeId;
        pendingDragPointerStart = pointerPosition;
    }

    private bool TryPromotePendingDrag(Vector2 pointerPosition)
    {
        if (pendingDragNodeId < 0)
        {
            return false;
        }

        if (!nodeByIdMap.TryGetValue(pendingDragNodeId, out var node) || node == null || !node.RuntimeVisible)
        {
            pendingDragNodeId = -1;
            return false;
        }

        if ((pointerPosition - pendingDragPointerStart).sqrMagnitude < 16f)
        {
            return false;
        }

        StartUiDrag(node);
        pendingDragNodeId = -1;
        return true;
    }

    private void StartUiDrag(SuiNode node)
    {
        if (node == null)
        {
            return;
        }

        draggingUiNodeId = node.NodeId;
        dragHoverDropNodeId = -1;
        SetDragStateTokens(node, null);
        InvokeNodeAction(node.GetAttribute("ondragstart"));
    }

    private void UpdateUiDrag(Vector2 pointerPosition, SuiNode activeModal)
    {
        if (draggingUiNodeId < 0)
        {
            return;
        }

        if (!nodeByIdMap.TryGetValue(draggingUiNodeId, out var dragNode) || dragNode == null || !dragNode.RuntimeVisible)
        {
            ResetUiDragState(clearTokens: true);
            return;
        }

        var topNode = FindTopmostNode(pointerPosition, activeModal);
        var dropNode = FindDropTargetNode(topNode);
        if (dropNode != null && dropNode.NodeId == draggingUiNodeId)
        {
            dropNode = null;
        }

        var nextDropId = dropNode?.NodeId ?? -1;
        if (nextDropId != dragHoverDropNodeId)
        {
            if (dragHoverDropNodeId >= 0 && nodeByIdMap.TryGetValue(dragHoverDropNodeId, out var previousDrop) && previousDrop != null)
            {
                InvokeNodeAction(previousDrop.GetAttribute("ondragleave"));
            }

            if (dropNode != null)
            {
                InvokeNodeAction(dropNode.GetAttribute("ondragenter"));
            }

            dragHoverDropNodeId = nextDropId;
            SetDragStateTokens(dragNode, dropNode);
        }

        InvokeNodeAction(dragNode.GetAttribute("ondrag"));
        if (dropNode != null)
        {
            InvokeNodeAction(dropNode.GetAttribute("ondragover"));
        }
    }

    private void EndUiDrag(Vector2 pointerPosition, SuiNode activeModal)
    {
        if (pendingDragNodeId >= 0 && draggingUiNodeId < 0)
        {
            pendingDragNodeId = -1;
            return;
        }

        if (draggingUiNodeId < 0)
        {
            ResetUiDragState(clearTokens: true);
            return;
        }

        var dragNode = nodeByIdMap.TryGetValue(draggingUiNodeId, out var source) ? source : null;
        var topNode = FindTopmostNode(pointerPosition, activeModal);
        var dropNode = FindDropTargetNode(topNode);
        if (dropNode != null && dropNode.NodeId == draggingUiNodeId)
        {
            dropNode = null;
        }

        if (dropNode != null)
        {
            SetDragStateTokens(dragNode, dropNode);
            InvokeNodeAction(dropNode.GetAttribute("ondrop"));
            InvokeNodeAction(dragNode != null ? dragNode.GetAttribute("ondropcomplete") : null);
            InvokeNodeAction(dropNode.GetAttribute("ondragleave"));
        }

        if (dragNode != null)
        {
            InvokeNodeAction(dragNode.GetAttribute("ondragend"));
        }

        ResetUiDragState(clearTokens: true);
    }

    private void ResetUiDragState(bool clearTokens)
    {
        pendingDragNodeId = -1;
        draggingUiNodeId = -1;
        dragHoverDropNodeId = -1;
        if (clearTokens)
        {
            ClearDragStateTokens();
        }
    }

    private static SuiNode FindDraggableNode(SuiNode startNode)
    {
        var current = startNode;
        while (current != null)
        {
            if (IsNodeDraggable(current))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static SuiNode FindDropTargetNode(SuiNode startNode)
    {
        var current = startNode;
        while (current != null)
        {
            if (IsNodeDroppable(current))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsNodeDraggable(SuiNode node)
    {
        if (node == null || node.IsWindow || node.IsDisabled)
        {
            return false;
        }

        var raw = node.GetAttribute("draggable");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return ParseBoolAttribute(raw, false);
    }

    private static bool IsNodeDroppable(SuiNode node)
    {
        if (node == null || node.IsDisabled)
        {
            return false;
        }

        var raw = node.GetAttribute("droppable");
        if (!string.IsNullOrWhiteSpace(raw) && ParseBoolAttribute(raw, false))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(node.GetAttribute("ondrop"));
    }

    private void SetDragStateTokens(SuiNode dragNode, SuiNode dropNode)
    {
        var sourceId = GetNodeTokenIdentity(dragNode);
        var sourceName = dragNode != null ? dragNode.GetAttribute("name") : string.Empty;
        var dragData = dragNode != null ? dragNode.GetAttribute("drag-data") : string.Empty;
        var targetId = GetNodeTokenIdentity(dropNode);
        var targetName = dropNode != null ? dropNode.GetAttribute("name") : string.Empty;

        textStateMap["suiDragSourceId"] = sourceId;
        textStateMap["suiDragSource"] = sourceName;
        textStateMap["suiDragData"] = dragData;
        textStateMap["suiDropTargetId"] = targetId;
        textStateMap["suiDropTarget"] = targetName;
        boolStateMap["suiDragging"] = dragNode != null;
        MarkDocumentDirty();
    }

    private void ClearDragStateTokens()
    {
        textStateMap["suiDragSourceId"] = string.Empty;
        textStateMap["suiDragSource"] = string.Empty;
        textStateMap["suiDragData"] = string.Empty;
        textStateMap["suiDropTargetId"] = string.Empty;
        textStateMap["suiDropTarget"] = string.Empty;
        boolStateMap["suiDragging"] = false;
        MarkDocumentDirty();
    }

    private static string GetNodeTokenIdentity(SuiNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var id = node.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        var name = node.GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return "node:" + node.NodeId.ToString();
    }

    private void ApplyWindowPositionOverrides()
    {
        for (var i = 0; i < documentNodes.Count; i++)
        {
            ApplyWindowPositionOverrides(documentNodes[i]);
        }
    }

    private void ApplyWindowPositionOverrides(SuiNode node)
    {
        if (node == null)
        {
            return;
        }

        if (!node.RuntimeVisible)
        {
            return;
        }

        if (node.IsWindow)
        {
            var key = GetWindowKey(node);
            if (!string.IsNullOrWhiteSpace(key) && windowPositionOverrides.TryGetValue(key, out var pos))
            {
                var delta = pos - node.LayoutRect.position;
                TranslateSubtree(node, delta);
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            ApplyWindowPositionOverrides(node.Children[i]);
        }
    }

    private void ApplyScrollOffsets()
    {
        for (var i = 0; i < documentNodes.Count; i++)
        {
            ApplyScrollOffsets(documentNodes[i]);
        }
    }

    private void ApplyScrollOffsets(SuiNode node)
    {
        if (node == null || !node.RuntimeVisible)
        {
            return;
        }

        if (node.IsScrollContainer)
        {
            var contentRect = GetContentRect(node);
            var maxScroll = Mathf.Max(0f, node.RuntimeContentHeight - contentRect.height);
            var scrollY = GetScrollY(node);
            scrollY = Mathf.Clamp(scrollY, 0f, maxScroll);
            SetScrollY(node, scrollY);
            node.RuntimeScrollY = scrollY;

            if (scrollY > 0.001f)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    TranslateSubtree(node.Children[i], new Vector2(0f, -scrollY));
                }
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            ApplyScrollOffsets(node.Children[i]);
        }
    }

    private SuiNode FindNearestScrollContainer(SuiNode startNode)
    {
        var current = startNode;
        while (current != null)
        {
            if (current.IsScrollContainer)
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private void ScrollNodeBy(SuiNode node, float deltaY)
    {
        if (node == null || !node.IsScrollContainer)
        {
            return;
        }

        var contentRect = GetContentRect(node);
        var maxScroll = Mathf.Max(0f, node.RuntimeContentHeight - contentRect.height);
        if (maxScroll <= 0.001f)
        {
            return;
        }

        var current = GetScrollY(node);
        var next = Mathf.Clamp(current + deltaY, 0f, maxScroll);
        if (Mathf.Abs(next - current) <= 0.001f)
        {
            return;
        }

        SetScrollY(node, next);
        layoutDirty = true;
    }

    private void BeginWindowDrag(SuiNode window, Vector2 pointerPosition)
    {
        if (window == null)
        {
            return;
        }

        draggingWindowNodeId = window.NodeId;
        draggingWindowKey = GetWindowKey(window);
        draggingWindowPointerOffset = pointerPosition - window.LayoutRect.position;
    }

    private void SetWindowPositionOverride(SuiNode window, Vector2 targetPosition)
    {
        if (window == null)
        {
            return;
        }

        var key = GetWindowKey(window);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var delta = targetPosition - window.LayoutRect.position;
        if (delta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        windowPositionOverrides[key] = targetPosition;
        TranslateSubtree(window, delta);
    }

    private static void TranslateSubtree(SuiNode node, Vector2 delta)
    {
        if (node == null)
        {
            return;
        }

        node.LayoutRect = new Rect(
            node.LayoutRect.x + delta.x,
            node.LayoutRect.y + delta.y,
            node.LayoutRect.width,
            node.LayoutRect.height);

        for (var i = 0; i < node.Children.Count; i++)
        {
            TranslateSubtree(node.Children[i], delta);
        }
    }

    private static SuiNode FindDraggableWindow(SuiNode startNode)
    {
        if (startNode == null)
        {
            return null;
        }

        var current = startNode;
        while (current != null)
        {
            if (current.IsWindow)
            {
                if (!IsWindowDraggable(current))
                {
                    return null;
                }

                if (IsInWindowHeader(startNode, current) || IsInHeaderBand(startNode, current))
                {
                    return current;
                }

                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsWindowDraggable(SuiNode window)
    {
        var raw = window.GetAttribute("draggable");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return !(raw == "false" || raw == "0" || raw == "no");
    }

    private static bool IsInWindowHeader(SuiNode node, SuiNode window)
    {
        var current = node;
        while (current != null && current != window)
        {
            if (current.IsWindowHeader)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsInHeaderBand(SuiNode node, SuiNode window)
    {
        if (node == null || window == null)
        {
            return false;
        }

        var y = node.LayoutRect.center.y;
        var headerHeight = Mathf.Min(32f, window.LayoutRect.height);
        return y <= (window.LayoutRect.y + headerHeight);
    }

    private static string GetWindowKey(SuiNode window)
    {
        if (window == null)
        {
            return string.Empty;
        }

        var id = window.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return "id:" + id;
        }

        var name = window.GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return "name:" + name;
        }

        return "node:" + window.NodeId.ToString();
    }

    private static string GetScrollKey(SuiNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var id = node.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return "id:" + id;
        }

        var name = node.GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return "name:" + name;
        }

        return "node:" + node.NodeId.ToString();
    }

    private float GetScrollY(SuiNode node)
    {
        if (node == null)
        {
            return 0f;
        }

        var key = GetScrollKey(node);
        if (!string.IsNullOrWhiteSpace(key) && scrollYOverrides.TryGetValue(key, out var value))
        {
            return value;
        }

        return ParseFloatOrDefault(node.GetAttribute("scroll-y"), 0f);
    }

    private void SetScrollY(SuiNode node, float value)
    {
        if (node == null)
        {
            return;
        }

        var key = GetScrollKey(node);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        scrollYOverrides[key] = Mathf.Max(0f, value);
    }

    private bool IsNodeFocused(SuiNode node)
    {
        if (node == null || !node.IsFocusable)
        {
            return false;
        }

        var controlName = GetControlName(node);
        if (string.IsNullOrWhiteSpace(controlName))
        {
            return false;
        }

        var current = GUI.GetNameOfFocusedControl();
        if (!string.IsNullOrWhiteSpace(current))
        {
            return string.Equals(current, controlName, StringComparison.Ordinal);
        }

        return string.Equals(focusedControlName, controlName, StringComparison.Ordinal);
    }

    private List<string> GetScopedFocusOrder(SuiNode activeModal)
    {
        if (activeModal == null)
        {
            return focusOrder;
        }

        var scoped = new List<string>();
        for (var i = 0; i < focusOrder.Count; i++)
        {
            var controlName = focusOrder[i];
            if (focusNodeMap.TryGetValue(controlName, out var node) && node != null && IsDescendantOrSelf(node, activeModal))
            {
                scoped.Add(controlName);
            }
        }

        return scoped;
    }

    private SuiNode GetActiveModalWindow()
    {
        if (!enableModalBehavior || documentNodes.Count == 0)
        {
            return null;
        }

        SuiNode activeModal = null;
        for (var i = 0; i < documentNodes.Count; i++)
        {
            CollectLastModal(documentNodes[i], ref activeModal);
        }

        return activeModal;
    }

    private static void CollectLastModal(SuiNode node, ref SuiNode activeModal)
    {
        if (node == null)
        {
            return;
        }

        if (node.RuntimeVisible && IsModalNode(node))
        {
            activeModal = node;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectLastModal(node.Children[i], ref activeModal);
        }
    }

    private static bool IsModalNode(SuiNode node)
    {
        if (node == null || !node.IsWindow)
        {
            return false;
        }

        var raw = node.GetAttribute("modal");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return ParseBoolAttribute(raw, false);
        }

        return node.Tag == "modal";
    }

    private bool IsWindowOpen(SuiNode window)
    {
        if (window == null || !window.IsWindow)
        {
            return true;
        }

        var key = GetWindowKey(window);
        if (!string.IsNullOrWhiteSpace(key) && windowOpenOverrides.TryGetValue(key, out var overrideOpen))
        {
            return overrideOpen;
        }

        var raw = window.GetAttribute("open");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return ParseBoolAttribute(raw, true);
    }

    private bool UpdateRuntimeVisibility()
    {
        var changed = false;
        for (var i = 0; i < documentNodes.Count; i++)
        {
            changed |= UpdateRuntimeVisibility(documentNodes[i], true);
        }

        return changed;
    }

    private bool UpdateRuntimeVisibility(SuiNode node, bool parentVisible)
    {
        if (node == null)
        {
            return false;
        }

        var visible = parentVisible && (!node.IsWindow || IsWindowOpen(node));
        var changed = node.RuntimeVisible != visible;
        node.RuntimeVisible = visible;

        for (var i = 0; i < node.Children.Count; i++)
        {
            changed |= UpdateRuntimeVisibility(node.Children[i], visible);
        }

        return changed;
    }

    private bool TryResolveWindowKey(string idOrName, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return false;
        }

        var lookup = idOrName.Trim();
        if (lookup.StartsWith("id:", StringComparison.OrdinalIgnoreCase) ||
            lookup.StartsWith("name:", StringComparison.OrdinalIgnoreCase) ||
            lookup.StartsWith("node:", StringComparison.OrdinalIgnoreCase))
        {
            key = lookup;
            return true;
        }

        if (TryFindWindowNodeByLookup(lookup, out var window))
        {
            key = GetWindowKey(window);
            return !string.IsNullOrWhiteSpace(key);
        }

        key = "id:" + lookup;
        return true;
    }

    private bool TryFindWindowNodeByLookup(string lookup, out SuiNode window)
    {
        window = null;
        if (string.IsNullOrWhiteSpace(lookup))
        {
            return false;
        }

        foreach (var pair in nodeByIdMap)
        {
            var node = pair.Value;
            if (node == null || !node.IsWindow)
            {
                continue;
            }

            var key = GetWindowKey(node);
            var id = node.GetAttribute("id");
            var name = node.GetAttribute("name");
            if (string.Equals(key, lookup, StringComparison.Ordinal) ||
                string.Equals(id, lookup, StringComparison.Ordinal) ||
                string.Equals(name, lookup, StringComparison.Ordinal))
            {
                window = node;
                return true;
            }
        }

        return false;
    }

    private bool TryFindWindowNodeByKey(string key, out SuiNode window)
    {
        window = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (var pair in nodeByIdMap)
        {
            var node = pair.Value;
            if (node == null || !node.IsWindow)
            {
                continue;
            }

            if (string.Equals(GetWindowKey(node), key, StringComparison.Ordinal))
            {
                window = node;
                return true;
            }
        }

        return false;
    }

    private void DrawModalBackdrop(SuiNode activeModal)
    {
        if (activeModal == null || !enableModalBehavior)
        {
            return;
        }

        var showBackdropRaw = activeModal.GetAttribute("show-backdrop");
        if (!string.IsNullOrWhiteSpace(showBackdropRaw) && !ParseBoolAttribute(showBackdropRaw, true))
        {
            return;
        }

        var color = defaultModalBackdropColor;
        var backdropColorRaw = activeModal.GetAttribute("backdrop-color");
        if (!string.IsNullOrWhiteSpace(backdropColorRaw) && ColorUtility.TryParseHtmlString(backdropColorRaw, out var parsedColor))
        {
            color = parsedColor;
        }

        var alphaRaw = activeModal.GetAttribute("backdrop-alpha");
        if (!string.IsNullOrWhiteSpace(alphaRaw) && float.TryParse(alphaRaw, out var alpha))
        {
            color.a = Mathf.Clamp01(alpha);
        }

        if (color.a <= 0.001f)
        {
            return;
        }

        var oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = oldColor;
    }

    private static bool ParseBoolAttribute(string raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        var value = raw.Trim().ToLowerInvariant();
        return value == "true" || value == "1" || value == "yes" || value == "on";
    }

    private bool IsNodeInvalid(SuiNode node)
    {
        if (node == null || node.IsDisabled)
        {
            return false;
        }

        if (!(node.IsInput || node.IsTextArea || node.IsSelect))
        {
            return false;
        }

        var required = ParseBool(node.GetAttribute("required"), false);
        var value = GetNodeCurrentValue(node);

        if (node.IsCheckbox)
        {
            var checkedValue = GetNodeCurrentBool(node);
            return required && !checkedValue;
        }

        if (required && string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(node.GetAttribute("minlength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minLength))
        {
            if ((value ?? string.Empty).Length < minLength)
            {
                return true;
            }
        }

        if (int.TryParse(node.GetAttribute("maxlength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxLength))
        {
            if ((value ?? string.Empty).Length > maxLength)
            {
                return true;
            }
        }

        var pattern = node.GetAttribute("pattern");
        if (!string.IsNullOrWhiteSpace(pattern) && !string.IsNullOrWhiteSpace(value))
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore invalid regex patterns in markup for runtime safety.
            }
        }

        var minRaw = node.GetAttribute("min");
        var maxRaw = node.GetAttribute("max");
        var hasRangeRules = !string.IsNullOrWhiteSpace(minRaw) || !string.IsNullOrWhiteSpace(maxRaw);
        if (hasRangeRules && !string.IsNullOrWhiteSpace(value))
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
            {
                return true;
            }

            if (float.TryParse(minRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) && numericValue < min)
            {
                return true;
            }

            if (float.TryParse(maxRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var max) && numericValue > max)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateNodeValidationAttribute(SuiNode node, bool isInvalid)
    {
        if (node == null)
        {
            return;
        }

        if (isInvalid)
        {
            node.Attributes["aria-invalid"] = "true";
            invalidNodeIdSet.Add(node.NodeId);
        }
        else
        {
            node.Attributes.Remove("aria-invalid");
            invalidNodeIdSet.Remove(node.NodeId);
        }
    }

    private string GetNodeCurrentValue(SuiNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var key = node.GetBindingKey();
        if (!string.IsNullOrWhiteSpace(key) && textStateMap.TryGetValue(key, out var bound))
        {
            return bound ?? string.Empty;
        }

        if (node.IsTextArea || node.IsInput || node.IsSelect)
        {
            var value = node.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return node.Text ?? string.Empty;
    }

    private bool GetNodeCurrentBool(SuiNode node)
    {
        if (node == null)
        {
            return false;
        }

        var key = node.GetBindingKey();
        if (!string.IsNullOrWhiteSpace(key) && boolStateMap.TryGetValue(key, out var bound))
        {
            return bound;
        }

        return ParseBool(node.GetAttribute("checked"), false);
    }

    private void ApplyBindingTypeFromText(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || !bindingTypeMap.TryGetValue(key, out var valueType))
        {
            return;
        }

        switch (valueType)
        {
            case SuiBindingValueType.Bool:
                boolStateMap[key] = ParseBool(value, false);
                textStateMap[key] = boolStateMap[key] ? "true" : "false";
                break;
            case SuiBindingValueType.Int:
                intStateMap[key] = ParseInt(value, 0);
                textStateMap[key] = intStateMap[key].ToString(CultureInfo.InvariantCulture);
                break;
            case SuiBindingValueType.Float:
                floatStateMap[key] = ParseFloat(value, 0f);
                textStateMap[key] = floatStateMap[key].ToString("0.###", CultureInfo.InvariantCulture);
                break;
        }
    }

    private void UpdateTokenForKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (bindingTypeMap.TryGetValue(key, out var valueType))
        {
            switch (valueType)
            {
                case SuiBindingValueType.Bool:
                    if (boolStateMap.TryGetValue(key, out var boolValue))
                    {
                        tokenMap[key] = boolValue ? "true" : "false";
                        return;
                    }

                    break;
                case SuiBindingValueType.Int:
                    if (intStateMap.TryGetValue(key, out var intValue))
                    {
                        tokenMap[key] = intValue.ToString(CultureInfo.InvariantCulture);
                        return;
                    }

                    break;
                case SuiBindingValueType.Float:
                    if (floatStateMap.TryGetValue(key, out var floatValue))
                    {
                        tokenMap[key] = floatValue.ToString("0.###", CultureInfo.InvariantCulture);
                        return;
                    }

                    break;
            }
        }

        if (textStateMap.TryGetValue(key, out var value))
        {
            tokenMap[key] = value ?? string.Empty;
        }
    }

    private static bool ParseBool(string raw, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var value = raw.Trim().ToLowerInvariant();
        return value == "true" || value == "1" || value == "yes" || value == "on";
    }

    private static int ParseInt(string raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static float ParseFloat(string raw, float fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private bool TryInvokeMappedOrScriptAction(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        if (actionMap.TryGetValue(actionName, out var callback) && callback != null)
        {
            callback.Invoke();
            return true;
        }

        return TryInvokeScriptAction(actionName);
    }

    private bool TryInvokeScriptAction(string actionName)
    {
        if (scriptActionMap.TryGetValue(actionName, out var binding))
        {
            return InvokeScriptBinding(binding);
        }

        var dotIndex = actionName.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex < actionName.Length - 1)
        {
            var methodOnly = actionName.Substring(dotIndex + 1);
            if (scriptActionMap.TryGetValue(methodOnly, out var methodBinding))
            {
                return InvokeScriptBinding(methodBinding);
            }
        }

        return false;
    }

    private static bool InvokeScriptBinding(ScriptActionBinding binding)
    {
        if (binding.Target == null || binding.Method == null)
        {
            return false;
        }

        try
        {
            binding.Method.Invoke(binding.Target, null);
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("SUI script action invoke failed: " + ex.Message, binding.Target);
            return false;
        }
    }

    private void BuildScriptActionMap()
    {
        var targets = CollectScriptTargets();
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null)
            {
                continue;
            }

            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (var m = 0; m < methods.Length; m++)
            {
                var method = methods[m];
                if (!IsSupportedScriptMethod(method))
                {
                    continue;
                }

                var key = method.Name;
                scriptActionMap[key] = new ScriptActionBinding
                {
                    Target = target,
                    Method = method
                };

                var qualified = target.GetType().Name + "." + method.Name;
                scriptActionMap[qualified] = new ScriptActionBinding
                {
                    Target = target,
                    Method = method
                };
            }
        }
    }

    private List<MonoBehaviour> CollectScriptTargets()
    {
        var result = new List<MonoBehaviour>(scriptTargets.Count + 8);
        if (scriptTargets != null)
        {
            for (var i = 0; i < scriptTargets.Count; i++)
            {
                var target = scriptTargets[i];
                if (target == null || result.Contains(target))
                {
                    continue;
                }

                result.Add(target);
            }
        }

        if (includeSiblingMonoBehaviours)
        {
            var siblings = GetComponents<MonoBehaviour>();
            for (var i = 0; i < siblings.Length; i++)
            {
                var sibling = siblings[i];
                if (sibling == null || sibling == this || result.Contains(sibling))
                {
                    continue;
                }

                result.Add(sibling);
            }
        }

        return result;
    }

    private static bool IsSupportedScriptMethod(MethodInfo method)
    {
        if (method == null || method.IsSpecialName)
        {
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            return false;
        }

        return method.GetParameters().Length == 0;
    }

    private static float ParseFloatOrDefault(string raw, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return float.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static Rect GetContentRect(SuiNode node)
    {
        if (node == null || node.Style == null)
        {
            return default;
        }

        var style = node.Style;
        return new Rect(
            node.LayoutRect.x + style.PaddingLeft,
            node.LayoutRect.y + style.PaddingTop,
            Mathf.Max(0f, node.LayoutRect.width - style.PaddingLeft - style.PaddingRight),
            Mathf.Max(0f, node.LayoutRect.height - style.PaddingTop - style.PaddingBottom));
    }

    private static bool IsDescendantOrSelf(SuiNode node, SuiNode root)
    {
        if (node == null || root == null)
        {
            return false;
        }

        var current = node;
        while (current != null)
        {
            if (current == root)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static string GetAssetText(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        var asTextAsset = asset as TextAsset;
        if (asTextAsset != null)
        {
            return asTextAsset.text;
        }

#if UNITY_EDITOR
        var assetPath = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrWhiteSpace(assetPath) && File.Exists(assetPath))
        {
            return File.ReadAllText(assetPath);
        }
#endif

        return null;
    }
}
}

