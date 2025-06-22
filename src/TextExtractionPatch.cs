using HarmonyLib;
using UnityEngine;

/// <summary>
/// Patch for automatic extraction of all game texts on initialization
/// </summary>
[HarmonyPatch]
public static class TextExtractionPatch
{
    private static bool textExtracted = false;

    /// <summary>
            /// Automatically extract all texts after GameData loading
    /// </summary>
    //[HarmonyPatch(typeof(GameData), "loadData", new System.Type[] { typeof(string) })]
    //[HarmonyPostfix]
    public static void ExtractAllTextOnGameDataLoad()
    {
        if (!textExtracted)
        {
            try
            {
                Debug.Log("Starting automatic text extraction after GameData.loadData()...");
                TextDataExtractor.ExtractAllTextToPluginDirectory();
                textExtracted = true;
                Debug.Log("✓ All game text automatically extracted to plugin/text/ directory!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to extract text automatically: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
            /// Duplicate extraction attempt during DataControl initialization as backup
    /// </summary>
    //[HarmonyPatch(typeof(DataControl), "initialize")]
    //[HarmonyPostfix]
    public static void ExtractAllTextOnDataControlInit()
    {
        if (!textExtracted)
        {
            try
            {
                Debug.Log("Starting automatic text extraction (DataControl fallback)...");
                TextDataExtractor.ExtractAllTextToPluginDirectory();
                textExtracted = true;
                Debug.Log("✓ All game text automatically extracted to plugin/text/ directory!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to extract text automatically (fallback): {ex.Message}");
            }
        }
    }
} 