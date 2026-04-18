using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SUI.Runtime.Editor
{
[CustomEditor(typeof(SuiRuntimeHost))]
public sealed class SuiRuntimeHostEditor : UnityEditor.Editor
{
    private const string BulkModePrefKey = "SUI.Runtime.SuiRuntimeHostEditor.BulkMode";
    private const string BulkTextPrefKey = "SUI.Runtime.SuiRuntimeHostEditor.BulkText";

    private SerializedProperty razorFileProp;
    private SerializedProperty styleSheetFileProp;
    private SerializedProperty rootNamespaceProp;
    private SerializedProperty compileWithSandboxRazorProp;
    private SerializedProperty panelProp;
    private SerializedProperty valuesProp;
    private SerializedProperty actionsProp;
    private SerializedProperty generatedCSharpProp;
    private SerializedProperty lastErrorProp;
    private SerializedProperty propagateControlStateToTokensProp;
    private SerializedProperty enablePerformanceCountersProp;
    private SerializedProperty documentRebuildCountProp;
    private SerializedProperty layoutPassCountProp;
    private SerializedProperty renderPassCountProp;
    private SerializedProperty lastDocumentRebuildMsProp;

    private bool useBulkMode;
    private string bulkText = string.Empty;

    private void OnEnable()
    {
        razorFileProp = serializedObject.FindProperty("razorFile");
        styleSheetFileProp = serializedObject.FindProperty("styleSheetFile");
        rootNamespaceProp = serializedObject.FindProperty("rootNamespace");
        compileWithSandboxRazorProp = serializedObject.FindProperty("compileWithSandboxRazor");
        panelProp = serializedObject.FindProperty("panel");
        valuesProp = serializedObject.FindProperty("values");
        actionsProp = serializedObject.FindProperty("actions");
        generatedCSharpProp = serializedObject.FindProperty("generatedCSharp");
        lastErrorProp = serializedObject.FindProperty("lastError");
        propagateControlStateToTokensProp = serializedObject.FindProperty("propagateControlStateToTokens");
        enablePerformanceCountersProp = serializedObject.FindProperty("enablePerformanceCounters");
        documentRebuildCountProp = serializedObject.FindProperty("documentRebuildCount");
        layoutPassCountProp = serializedObject.FindProperty("layoutPassCount");
        renderPassCountProp = serializedObject.FindProperty("renderPassCount");
        lastDocumentRebuildMsProp = serializedObject.FindProperty("lastDocumentRebuildMs");

        useBulkMode = EditorPrefs.GetBool(BulkModePrefKey, false);
        bulkText = EditorPrefs.GetString(BulkTextPrefKey, string.Empty);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSourceSection();
        EditorGUILayout.Space(8f);

        DrawBindingsSection();
        EditorGUILayout.Space(8f);

        EditorGUILayout.PropertyField(actionsProp, true);
        EditorGUILayout.Space(8f);

        EditorGUILayout.PropertyField(panelProp, true);
        EditorGUILayout.Space(8f);

        DrawDebugSection();
        EditorGUILayout.Space(8f);

        DrawDiagnosticsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSourceSection()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(razorFileProp);
        EditorGUILayout.PropertyField(styleSheetFileProp);
        EditorGUILayout.PropertyField(rootNamespaceProp);
        EditorGUILayout.PropertyField(compileWithSandboxRazorProp);
    }

    private void DrawBindingsSection()
    {
        EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Values", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(useBulkMode ? "Use Table Mode" : "Use Bulk Mode", GUILayout.Width(120f)))
        {
            useBulkMode = !useBulkMode;
            EditorPrefs.SetBool(BulkModePrefKey, useBulkMode);
        }
        EditorGUILayout.EndHorizontal();

        if (useBulkMode)
        {
            DrawBulkValuesEditor();
        }
        else
        {
            DrawTableValuesEditor();
        }

        DrawValueWarnings();
    }

    private void DrawTableValuesEditor()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Key", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Type", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Value", EditorStyles.miniBoldLabel);
        GUILayout.Space(24f);
        EditorGUILayout.EndHorizontal();

        for (var i = 0; i < valuesProp.arraySize; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            var keyProp = element.FindPropertyRelative("key");
            var typeProp = element.FindPropertyRelative("valueType");
            var valueProp = element.FindPropertyRelative("value");

            EditorGUILayout.BeginHorizontal();
            keyProp.stringValue = EditorGUILayout.TextField(keyProp.stringValue);
            EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(70f));
            valueProp.stringValue = EditorGUILayout.TextField(valueProp.stringValue);

            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("-", GUILayout.Width(24f)))
            {
                valuesProp.DeleteArrayElementAtIndex(i);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Row"))
        {
            var index = valuesProp.arraySize;
            valuesProp.InsertArrayElementAtIndex(index);
            var newElement = valuesProp.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("key").stringValue = string.Empty;
            newElement.FindPropertyRelative("valueType").enumValueIndex = (int)SuiBindingValueType.String;
            newElement.FindPropertyRelative("value").stringValue = string.Empty;
        }

        if (GUILayout.Button("Sort A-Z"))
        {
            SortValuesByKey();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawBulkValuesEditor()
    {
        EditorGUILayout.HelpBox("Use one binding per line: key=value or key:type=value (type: string, bool, int, float)", MessageType.Info);

        bulkText = EditorGUILayout.TextArea(bulkText, GUILayout.MinHeight(110f));
        EditorPrefs.SetString(BulkTextPrefKey, bulkText ?? string.Empty);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load From Table"))
        {
            bulkText = BuildBulkTextFromValues();
            EditorPrefs.SetString(BulkTextPrefKey, bulkText ?? string.Empty);
        }

        if (GUILayout.Button("Apply To Table"))
        {
            ApplyBulkTextToValues(bulkText);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDebugSection()
    {
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(generatedCSharpProp, true);
        EditorGUILayout.PropertyField(lastErrorProp, true);
        EditorGUILayout.PropertyField(propagateControlStateToTokensProp);
    }

    private void DrawDiagnosticsSection()
    {
        EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enablePerformanceCountersProp);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(documentRebuildCountProp);
            EditorGUILayout.PropertyField(layoutPassCountProp);
            EditorGUILayout.PropertyField(renderPassCountProp);
            EditorGUILayout.PropertyField(lastDocumentRebuildMsProp);
        }
    }

    private void DrawValueWarnings()
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        var emptyCount = 0;
        var parseIssues = new List<string>();

        for (var i = 0; i < valuesProp.arraySize; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            var key = element.FindPropertyRelative("key").stringValue ?? string.Empty;
            var type = (SuiBindingValueType)element.FindPropertyRelative("valueType").enumValueIndex;
            var rawValue = element.FindPropertyRelative("value").stringValue ?? string.Empty;
            key = key.Trim();

            if (key.Length == 0)
            {
                emptyCount++;
                continue;
            }

            if (!seenKeys.Add(key))
            {
                duplicates.Add(key);
            }

            if (type == SuiBindingValueType.Bool)
            {
                var normalized = rawValue.Trim().ToLowerInvariant();
                var isBool = normalized == "true" || normalized == "false" || normalized == "1" || normalized == "0" || normalized == "yes" || normalized == "no" || normalized == "on" || normalized == "off";
                if (!isBool)
                {
                    parseIssues.Add($"{key} expects bool");
                }
            }
            else if (type == SuiBindingValueType.Int)
            {
                if (!int.TryParse(rawValue, out _))
                {
                    parseIssues.Add($"{key} expects int");
                }
            }
            else if (type == SuiBindingValueType.Float)
            {
                if (!float.TryParse(rawValue, out _))
                {
                    parseIssues.Add($"{key} expects float");
                }
            }
        }

        if (emptyCount > 0)
        {
            EditorGUILayout.HelpBox($"Values has {emptyCount} empty key(s). Empty keys are ignored.", MessageType.Warning);
        }

        if (duplicates.Count > 0)
        {
            EditorGUILayout.HelpBox("Duplicate keys found: " + string.Join(", ", duplicates), MessageType.Warning);
        }

        if (parseIssues.Count > 0)
        {
            EditorGUILayout.HelpBox("Typed value parse issues: " + string.Join(", ", parseIssues), MessageType.Warning);
        }
    }

    private void SortValuesByKey()
    {
        var entries = new List<Tuple<string, SuiBindingValueType, string>>(valuesProp.arraySize);
        for (var i = 0; i < valuesProp.arraySize; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            entries.Add(new Tuple<string, SuiBindingValueType, string>(
                element.FindPropertyRelative("key").stringValue ?? string.Empty,
                (SuiBindingValueType)element.FindPropertyRelative("valueType").enumValueIndex,
                element.FindPropertyRelative("value").stringValue ?? string.Empty));
        }

        entries.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));

        valuesProp.arraySize = entries.Count;
        for (var i = 0; i < entries.Count; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("key").stringValue = entries[i].Item1;
            element.FindPropertyRelative("valueType").enumValueIndex = (int)entries[i].Item2;
            element.FindPropertyRelative("value").stringValue = entries[i].Item3;
        }
    }

    private string BuildBulkTextFromValues()
    {
        var sb = new StringBuilder(valuesProp.arraySize * 24);
        for (var i = 0; i < valuesProp.arraySize; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            var key = element.FindPropertyRelative("key").stringValue ?? string.Empty;
            var type = (SuiBindingValueType)element.FindPropertyRelative("valueType").enumValueIndex;
            var value = element.FindPropertyRelative("value").stringValue ?? string.Empty;
            if (key.Trim().Length == 0)
            {
                continue;
            }

            sb.Append(key);
            if (type != SuiBindingValueType.String)
            {
                sb.Append(':').Append(type.ToString().ToLowerInvariant());
            }

            sb.Append('=').Append(value).Append('\n');
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private void ApplyBulkTextToValues(string text)
    {
        var parsed = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var left = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                if (left.Length == 0)
                {
                    continue;
                }

                var key = left;
                var type = SuiBindingValueType.String;
                var typeSeparator = left.LastIndexOf(':');
                if (typeSeparator > 0 && typeSeparator < left.Length - 1)
                {
                    key = left.Substring(0, typeSeparator).Trim();
                    var rawType = left.Substring(typeSeparator + 1).Trim();
                    if (rawType.Equals("bool", StringComparison.OrdinalIgnoreCase))
                    {
                        type = SuiBindingValueType.Bool;
                    }
                    else if (rawType.Equals("int", StringComparison.OrdinalIgnoreCase))
                    {
                        type = SuiBindingValueType.Int;
                    }
                    else if (rawType.Equals("float", StringComparison.OrdinalIgnoreCase))
                    {
                        type = SuiBindingValueType.Float;
                    }
                    else if (rawType.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        type = SuiBindingValueType.String;
                    }
                }

                if (key.Length == 0)
                {
                    continue;
                }

                parsed.Add(new KeyValuePair<string, string>(key + "\u001f" + (int)type, value));
            }
        }

        valuesProp.arraySize = parsed.Count;
        for (var i = 0; i < parsed.Count; i++)
        {
            var element = valuesProp.GetArrayElementAtIndex(i);
            var payload = parsed[i].Key;
            var marker = payload.LastIndexOf('\u001f');
            var key = marker > 0 ? payload.Substring(0, marker) : payload;
            var typeIndex = (int)SuiBindingValueType.String;
            if (marker > 0 && marker < payload.Length - 1)
            {
                var raw = payload.Substring(marker + 1);
                if (!int.TryParse(raw, out typeIndex))
                {
                    typeIndex = (int)SuiBindingValueType.String;
                }
            }

            element.FindPropertyRelative("key").stringValue = key;
            element.FindPropertyRelative("valueType").enumValueIndex = typeIndex;
            element.FindPropertyRelative("value").stringValue = parsed[i].Value;
        }
    }
}
}
