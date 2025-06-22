using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Harmony patch для перехвата ToolTipControl.ToolTipCategory.getToolTip
    /// ЭТАП 2: Обрабатывает клики на переведенные ключевые слова и находит tooltip для оригинальных.
    /// Работает совместно с TooltipKeywordsPatch, который делает слова кликабельными.
    /// Использует буфер tooltip ключей из UITextBlockSetContentPatch.
    /// </summary>
    [HarmonyPatch]
    public static class TooltipTranslationPatch
    {
        /// <summary>
        /// Lazy-инициализация сервиса перевода
        /// </summary>
        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());

        /// <summary>
        /// Объект для синхронизации
        /// </summary>
        private static readonly object _lockObject = new();

        /// <summary>
        /// Определяем целевой метод для патча
        /// </summary>
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            // Ищем ToolTipControl.ToolTipCategory.getToolTip
            var toolTipControlType = AccessTools.TypeByName("ToolTipControl");
            if (toolTipControlType == null)
            {
                TranslationMod.Logger?.LogError("[TooltipTranslationPatch] ToolTipControl type not found");
                return null;
            }

            var toolTipCategoryType = toolTipControlType.GetNestedType("ToolTipCategory", BindingFlags.Public | BindingFlags.NonPublic);
            if (toolTipCategoryType == null)
            {
                TranslationMod.Logger?.LogError("[TooltipTranslationPatch] ToolTipCategory nested type not found");
                return null;
            }

            var getToolTipMethod = toolTipCategoryType.GetMethod("getToolTip", new[] { typeof(string) });
            if (getToolTipMethod == null)
            {
                TranslationMod.Logger?.LogError("[TooltipTranslationPatch] getToolTip method not found");
                return null;
            }

#if DEBUG
            TranslationMod.Logger?.LogInfo("[TooltipTranslationPatch] Successfully found getToolTip method");
#endif
            return getToolTipMethod;
        }

        /// <summary>
        /// Prefix patch - перехватывает вызов getToolTip и заменяет переведенные ключевые слова
        /// </summary>
        [HarmonyPrefix]
        static bool Prefix(object __instance, string keyword, ref object __result)
        {
            try
            {
                // Используем буфер tooltip ключей из UITextBlockSetContentPatch
                string originalKeyword = null;
                bool hasMapping = false;
                
                lock (_lockObject)
                {
                    hasMapping = UITextBlockSetContentPatch.TooltipKeyBuffer.TryGetValue(keyword, out originalKeyword);
                }

                if (!hasMapping)
                {
#if DEBUG
            TranslationMod.Logger?.LogDebug($"[TooltipTranslationPatch] No mapping found for keyword: '{keyword}', using original method");
#endif
                    return true; // Выполняем оригинальный метод
                }

#if DEBUG
            TranslationMod.Logger?.LogInfo($"[TooltipTranslationPatch] Translating tooltip keyword: '{keyword}' -> '{originalKeyword}'");
#endif

                // Вызываем оригинальный метод с оригинальным ключевым словом
                var originalMethod = TargetMethod();
                __result = originalMethod.Invoke(__instance, new object[] { originalKeyword });
                
                return false; // Пропускаем оригинальный метод
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[TooltipTranslationPatch] Error in Prefix: {ex.Message}");
                return true; // Выполняем оригинальный метод в случае ошибки
            }
        }
    }
} 