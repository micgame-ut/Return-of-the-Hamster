#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only. The pattern builder's Test This Pattern button stores a request here, and ObjectSpawner and GameManager read it when the test level starts.
public static class PatternTest
{
    private const string PatternKey = "PatternBuilder.TestPattern";
    private const string DelayKey = "PatternBuilder.TestLoopDelay";

    // Stores a test request. Called by the pattern builder's Test This Pattern button.
    public static void Request(string patternAssetPath, float loopDelay)
    {
        EditorPrefs.SetString(PatternKey, patternAssetPath);
        EditorPrefs.SetFloat(DelayKey, loopDelay);
    }

    // True from the moment a test is requested until Clear is called.
    public static bool Active => EditorPrefs.GetString(PatternKey, "") != "";

    // The pattern prefab to test, loaded from the saved path. Null if the path no longer points at a prefab.
    public static GameObject Pattern => AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString(PatternKey, ""));

    // Seconds to wait after a run has cleared before the next one starts.
    public static float LoopDelay => EditorPrefs.GetFloat(DelayKey, 3f);

    // Forgets the request. Called when Play ends, so a normal Play isn't a test.
    public static void Clear()
    {
        EditorPrefs.DeleteKey(PatternKey);
        EditorPrefs.DeleteKey(DelayKey);
    }
}
#endif
