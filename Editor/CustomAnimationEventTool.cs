using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

public class CustomAnimationEventTool : EditorWindow
{
    private AnimationClip targetClip;
    private GameObject previewPrefab;
    private List<EventData> eventPoints = new List<EventData>();

    private PreviewRenderUtility previewUtility;
    private GameObject previewInstance;
    private Dictionary<int, GameObject> vfxPreviews = new Dictionary<int, GameObject>();

    private Vector2 previewDir = new Vector2(120, -20);
    private float scrubTime = 0f;
    private string[] availableFunctions = new string[0];
    private string newMethodName = "";

    [MenuItem("Window/Custom/Animation Event Tool")]
    public static void ShowWindow() => GetWindow<CustomAnimationEventTool>("Animation Event Tool");

    private void OnEnable()
    {
        if (previewUtility == null) previewUtility = new PreviewRenderUtility();
        FetchAnimEventsMethods();
    }

    private void FetchAnimEventsMethods()
    {
        System.Type type = typeof(AnimEvents);
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        availableFunctions = methods.Select(m => m.Name).ToArray();
    }

    private void OnDisable() => Cleanup();

    private void Cleanup()
    {
        if (previewUtility != null) { previewUtility.Cleanup(); previewUtility = null; }
        if (previewInstance != null) DestroyImmediate(previewInstance);
        ClearAllVFXPreviews();
    }

    private void ClearAllVFXPreviews()
    {
        foreach (var vfx in vfxPreviews.Values) if (vfx != null) DestroyImmediate(vfx);
        vfxPreviews.Clear();
    }

    private void OnGUI()
    {
        DrawGlobalTimeline();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(450));
        DrawSettings();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        DrawPreviewArea();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGlobalTimeline()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("ANIMATION TIMELINE CONTROL", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.BeginChangeCheck();
        scrubTime = EditorGUILayout.Slider(scrubTime, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) UpdateAnimationPreview();
        EditorGUILayout.EndVertical();
    }

    private void DrawSettings()
    {
        GUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");
        EditorGUI.BeginChangeCheck();
        targetClip = (AnimationClip)EditorGUILayout.ObjectField("Target Clip", targetClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck() && targetClip != null) LoadEventsFromClip();

        previewPrefab = (GameObject)EditorGUILayout.ObjectField("Preview Model", previewPrefab, typeof(GameObject), false);

        if (GUILayout.Button("RESET & CLEANUP", GUILayout.Height(20)))
        {
            if (EditorUtility.DisplayDialog("Cleanup", "ต้องการล้างข้อมูลทั้งหมดหรือไม่?", "Yes", "No"))
            {
                eventPoints.Clear(); Cleanup();
                if (previewUtility == null) previewUtility = new PreviewRenderUtility();
            }
        }
        EditorGUILayout.EndVertical();

        if (targetClip != null)
        {
            // --- ส่วนการเพิ่ม EVENT (รวมทุกแบบ) ---
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ADD / CREATE EVENTS", EditorStyles.boldLabel);

            // ปุ่มแบบเดิม (เลือกจาก List)
            if (GUILayout.Button("+ ADD NEW EVENT POINT (Manual Select List)", GUILayout.Height(25)))
            {
                eventPoints.Add(new EventData { time = scrubTime });
            }

            GUILayout.Space(5);

            // ปุ่มทางลัด VFX
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // สีเขียวอ่อน
            if (GUILayout.Button("+ PlayEffect (VFX Quick Add)", GUILayout.Height(25)))
            {
                eventPoints.Add(new EventData { func = "PlayEffect", time = scrubTime });
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Create New Code into AnimEvents.cs", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            newMethodName = EditorGUILayout.TextField(newMethodName, GUILayout.Height(20));
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f); // สีฟ้า
            if (GUILayout.Button("Create EmptyMethod", GUILayout.Width(130), GUILayout.Height(20)))
            {
                CreateAndAddEmptyMethod();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            // ------------------------------------

            GUILayout.Space(10);
            for (int i = 0; i < eventPoints.Count; i++) DrawEventBox(i);

            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("APPLY EVENTS TO CLIP", GUILayout.Height(45))) Apply();
            GUI.backgroundColor = Color.white;
        }
    }

    private void CreateAndAddEmptyMethod()
    {
        if (string.IsNullOrEmpty(newMethodName)) return;
        string methodName = newMethodName.Trim().Replace(" ", "");
        string[] guids = AssetDatabase.FindAssets("AnimEvents t:Script");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        string content = File.ReadAllText(path);

        if (!content.Contains($"public void {methodName}("))
        {
            int lastIndex = content.LastIndexOf('}');
            string newCode = $"\n    public void {methodName}()\n    {{\n        // New Auto-Generated Method\n    }}\n";
            content = content.Insert(lastIndex, newCode);
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
        }

        eventPoints.Add(new EventData { func = methodName, time = scrubTime });
        newMethodName = "";
        EditorApplication.delayCall += () => FetchAnimEventsMethods();
    }

    private void LoadEventsFromClip()
    {
        eventPoints.Clear();
        ClearAllVFXPreviews();
        var existingEvents = AnimationUtility.GetAnimationEvents(targetClip);
        foreach (var ev in existingEvents)
        {
            EventData data = new EventData
            {
                func = ev.functionName,
                time = ev.time / targetClip.length,
                objectReference = ev.objectReferenceParameter,
                delayValue = ev.floatParameter
            };
            eventPoints.Add(data);
            if (data.objectReference is GameObject) UpdateVFXPreview(eventPoints.Count - 1);
        }
    }

    private void DrawEventBox(int i)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        int currentIndex = System.Array.IndexOf(availableFunctions, eventPoints[i].func);
        if (currentIndex == -1) currentIndex = 0;
        eventPoints[i].func = availableFunctions[EditorGUILayout.Popup(currentIndex, availableFunctions)];

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Delete", GUILayout.Width(60)))
        {
            if (vfxPreviews.ContainsKey(i)) { DestroyImmediate(vfxPreviews[i]); vfxPreviews.Remove(i); }
            eventPoints.RemoveAt(i); return;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (eventPoints[i].func == "OnFinishWithCleanup")
        {
            eventPoints[i].delayValue = EditorGUILayout.Slider("Cleanup Delay (s)", eventPoints[i].delayValue, 0f, 5f);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            eventPoints[i].objectReference = EditorGUILayout.ObjectField("Prefab/Clip", eventPoints[i].objectReference, typeof(Object), false);
            if (EditorGUI.EndChangeCheck()) UpdateVFXPreview(i);
        }

        if (eventPoints[i].objectReference is GameObject vfxGo)
        {
            EditorGUILayout.BeginVertical("box");
            eventPoints[i].pos = EditorGUILayout.Vector3Field("Pos Offset", eventPoints[i].pos);
            eventPoints[i].rot = EditorGUILayout.Vector3Field("Rot Offset", eventPoints[i].rot);
            eventPoints[i].scale = EditorGUILayout.Vector3Field("Scale", eventPoints[i].scale);
            if (GUILayout.Button("Save to Prefab", EditorStyles.miniButton)) SaveTransformToPrefab(vfxGo, eventPoints[i]);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(25))) { eventPoints[i].time -= 0.001f; scrubTime = eventPoints[i].time; UpdateAnimationPreview(); }
        EditorGUI.BeginChangeCheck();
        eventPoints[i].time = EditorGUILayout.Slider(eventPoints[i].time, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) { scrubTime = eventPoints[i].time; UpdateAnimationPreview(); }
        if (GUILayout.Button(">", GUILayout.Width(25))) { eventPoints[i].time += 0.001f; scrubTime = eventPoints[i].time; UpdateAnimationPreview(); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void UpdateVFXPreview(int index)
    {
        if (vfxPreviews.ContainsKey(index) && vfxPreviews[index] != null) DestroyImmediate(vfxPreviews[index]);
        if (eventPoints[index].objectReference is GameObject prefab)
        {
            GameObject inst = Instantiate(prefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            vfxPreviews[index] = inst;
            eventPoints[index].pos = prefab.transform.localPosition;
            eventPoints[index].rot = prefab.transform.localEulerAngles;
            eventPoints[index].scale = prefab.transform.localScale;
        }
    }

    private void SaveTransformToPrefab(GameObject prefab, EventData data)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.transform.localPosition = data.pos;
        root.transform.localEulerAngles = data.rot;
        root.transform.localScale = data.scale;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("VFX Transform Saved!");
    }

    private void UpdateAnimationPreview()
    {
        scrubTime = Mathf.Clamp01(scrubTime);
        if (previewInstance != null && targetClip != null)
        {
            targetClip.SampleAnimation(previewInstance, scrubTime * targetClip.length);
            Repaint();
        }
    }

    private void DrawPreviewArea()
    {
        Rect rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (previewPrefab == null || targetClip == null) return;

        if (previewInstance == null)
        {
            previewInstance = Instantiate(previewPrefab, Vector3.zero, Quaternion.identity);
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
        }

        HandleMouseInput(rect);
        previewUtility.BeginPreview(rect, GUIStyle.none);
        float distance = 5f;
        Quaternion camRot = Quaternion.Euler(-previewDir.y, -previewDir.x, 0);
        previewUtility.camera.transform.position = camRot * (Vector3.back * distance) + Vector3.up * 1f;
        previewUtility.camera.transform.rotation = camRot;

        foreach (var item in vfxPreviews)
        {
            int i = item.Key; GameObject vfx = item.Value;
            if (vfx != null && i < eventPoints.Count)
            {
                vfx.transform.position = previewInstance.transform.TransformPoint(eventPoints[i].pos);
                vfx.transform.rotation = previewInstance.transform.rotation * Quaternion.Euler(eventPoints[i].rot);
                vfx.transform.localScale = eventPoints[i].scale;
                vfx.SetActive(Mathf.Abs(scrubTime - eventPoints[i].time) < 0.05f);
            }
        }
        previewUtility.Render(true);
        GUI.DrawTexture(rect, previewUtility.EndPreview(), ScaleMode.StretchToFill, false);
    }

    private void HandleMouseInput(Rect rect)
    {
        Event e = Event.current;
        if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && e.button == 0)
        {
            previewDir.x -= e.delta.x * 0.5f; previewDir.y -= e.delta.y * 0.5f; Repaint();
        }
    }

    private void Apply()
    {
        if (targetClip == null) return;
        Undo.RecordObject(targetClip, "Apply Animation Events");
        AnimationUtility.SetAnimationEvents(targetClip, new AnimationEvent[0]);

        List<AnimationEvent> newEvents = new List<AnimationEvent>();
        foreach (var p in eventPoints)
        {
            if (string.IsNullOrEmpty(p.func)) continue;
            AnimationEvent ev = new AnimationEvent
            {
                functionName = p.func.Trim(),
                time = Mathf.Clamp(p.time * targetClip.length, 0f, targetClip.length),
                objectReferenceParameter = p.objectReference as Object,
                floatParameter = p.delayValue
            };
            newEvents.Add(ev);
        }
        AnimationUtility.SetAnimationEvents(targetClip, newEvents.ToArray());
        EditorUtility.SetDirty(targetClip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan><b>[Success]</b></color> เขียนทับ Event เรียบร้อยแล้ว!");
    }

    [System.Serializable]
    public class EventData
    {
        public string func = ""; public float time; public Object objectReference;
        public float delayValue; public Vector3 pos = Vector3.zero;
        public Vector3 rot = Vector3.zero; public Vector3 scale = Vector3.one;
    }
}