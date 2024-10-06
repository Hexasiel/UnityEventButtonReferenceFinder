using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

public class UnityEventButtonReferenceFinder : EditorWindow {
    [MenuItem("Tools/Find All UnityEvent References")]
    public static void ShowWindow() {
        GetWindow<UnityEventButtonReferenceFinder>("Find All UnityEvent References");
    }

    // Hold method information
    private class MethodInfo {
        public string MethodName;
        public int CallCount;
        public List<CallerInfo> Callers = new List<CallerInfo>();
    }

    private class CallerInfo {
        public string ObjectName;
        public string SceneName;

        public CallerInfo(string objectName, string sceneName) {
            ObjectName = objectName;
            SceneName = sceneName;
        }
    }

    // Dictionary to store method information
    private Dictionary<string, MethodInfo> methodDictionary = new Dictionary<string, MethodInfo>();
    // Dictionary to keep track of which methods are expanded
    private Dictionary<string, bool> methodExpanded = new Dictionary<string, bool>();

    private Vector2 scrollPosition; // To handle scrolling
    private string searchPattern = ""; // For storing the search input
    private Regex searchRegex; // For regex matching

    private void OnGUI() {
        // Search bar
        GUILayout.Label("Search:", EditorStyles.boldLabel);
        searchPattern = EditorGUILayout.TextField(searchPattern);

        // Update regex pattern
        try {
            searchRegex = new Regex(searchPattern, RegexOptions.IgnoreCase);
        } catch {
            searchRegex = null; // Reset if regex fails
        }

        if (GUILayout.Button("Find UnityEvent References in Project")) {
            FindUnityEventReferences();
        }

        // Start the scroll view
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Display methods and their call information in panels
        int index = 0; // For alternating colors
        foreach (var methodEntry in methodDictionary) {
            // Filter by search regex
            if (searchRegex != null && !searchRegex.IsMatch(methodEntry.Key)) {
                continue; // Skip this method if it doesn't match
            }

            var methodInfo = methodEntry.Value;

            // Check if this method is expanded
            if (!methodExpanded.ContainsKey(methodEntry.Key)) {
                methodExpanded[methodEntry.Key] = false; // Initialize to not expanded
            }

            // Set alternating background color
            Color backgroundColor = index % 2 == 0 ? new Color(0.9f, 0.9f, 0.9f) : Color.white;
            GUI.backgroundColor = backgroundColor;
            GUILayout.BeginVertical("box");
            GUILayout.Space(5); // Add some space at the top

            // Create a toggle for expanding/collapsing
            methodExpanded[methodEntry.Key] = EditorGUILayout.Foldout(methodExpanded[methodEntry.Key], 
                $"{methodEntry.Key} (Called {methodInfo.CallCount} times)");

            if (methodExpanded[methodEntry.Key]) {
                // Add a nested panel for each caller
                foreach (var caller in methodInfo.Callers) {
                    // Create a nested panel for each caller info
                    GUILayout.BeginVertical("box");
                    GUILayout.Label($"- {caller.ObjectName} ({caller.SceneName})");
                    GUILayout.EndVertical();
                }
            }

            GUILayout.Space(5); // Add some space at the bottom
            GUILayout.EndVertical();
            GUI.backgroundColor = Color.white; // Reset to default color
            index++;
        }

        // End the scroll view
        EditorGUILayout.EndScrollView();
    }

    private void FindUnityEventReferences() {
    methodDictionary.Clear(); // Clear previous results

    // Find all scene paths in the Build Settings
    string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
    foreach (string sceneGuid in sceneGuids) {
        string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);

        // Skip read-only package scenes
        if (scenePath.StartsWith("Packages/")) {
            continue;
        }

        // Ensure the scene file is valid and exists
        if (!scenePath.EndsWith(".unity")) {
            continue; // Skip any non-scene files
        }

        try {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            Debug.Log($"Opened scene: {scenePath}");

            // Inspect GameObjects in the scene
            foreach (GameObject rootObject in scene.GetRootGameObjects()) {
                InspectGameObjectForUnityEvents(rootObject, $"Scene:{scene.name}"); // Change here
            }

            // Close the scene after processing
            EditorSceneManager.CloseScene(scene, true);
        } catch (System.Exception e) {
            Debug.LogWarning($"Could not open scene '{scenePath}': {e.Message}");
        }
    }

    // Check all prefabs in the project
    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
    foreach (string prefabGuid in prefabGuids) {
        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);

        // Skip prefabs inside read-only packages
        if (prefabPath.StartsWith("Packages/")) {
            continue; // Skip prefabs in the Packages folder
        }

        try {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null) {
                // Inspect prefab components and mark them as prefabs
                InspectGameObjectForUnityEvents(prefab, $"Prefab:{prefab.name}"); // Change here
            } else {
                Debug.LogWarning($"Could not load prefab at '{prefabPath}' (missing or broken prefab).");
            }
        } catch (System.Exception e) {
            Debug.LogWarning($"Error loading prefab '{prefabPath}': {e.Message}");
        }
    }

    Debug.Log("Finished searching all scenes and prefabs.");
}

private void InspectGameObjectForUnityEvents(GameObject gameObject, string sourceName) {
    // Inspect all Components on the GameObject for UnityEvents
    Component[] components = gameObject.GetComponents<Component>();

    foreach (Component component in components) {
        if (component is Button button) {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) {
                Object target = button.onClick.GetPersistentTarget(i);
                string methodName = button.onClick.GetPersistentMethodName(i);

                // Aggregate method call info
                if (!methodDictionary.ContainsKey(methodName)) {
                    methodDictionary[methodName] = new MethodInfo { MethodName = methodName, CallCount = 0 };
                }

                methodDictionary[methodName].CallCount++;
                methodDictionary[methodName].Callers.Add(new CallerInfo(gameObject.name, sourceName)); // Change here
            }
        }
    }

    // Optionally check all child GameObjects recursively
    foreach (Transform child in gameObject.transform) {
        InspectGameObjectForUnityEvents(child.gameObject, sourceName);
    }
}
}