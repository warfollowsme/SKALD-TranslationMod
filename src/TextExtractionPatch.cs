using HarmonyLib;
using UnityEngine;

/// <summary>
/// Патч для автоматического извлечения всех текстов игры при инициализации
/// </summary>
[HarmonyPatch]
public static class TextExtractionPatch
{
    private static bool textExtracted = false;

    /// <summary>
    /// Автоматически извлекаем все тексты после загрузки данных GameData
    /// </summary>
    [HarmonyPatch(typeof(GameData), "loadData", new System.Type[] { typeof(string) })]
    [HarmonyPostfix]
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
    /// Дублируем попытку извлечения при инициализации DataControl как запасной вариант
    /// </summary>
    [HarmonyPatch(typeof(DataControl), "initialize")]
    [HarmonyPostfix]
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