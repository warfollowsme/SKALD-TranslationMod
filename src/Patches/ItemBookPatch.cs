using HarmonyLib;
using System;
using System.Collections.Generic;
using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Патч для класса ItemBook для логирования данных метода getContent
    /// </summary>
    [HarmonyPatch(typeof(ItemBook), "getContent")]
    public static class ItemBookPatch
    {
        /// <summary>
        /// Lazy initialization of translation service
        /// </summary>
        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());

        /// <summary>
        /// Максимальная длина строки перед разбивкой
        /// </summary>
        private const int MAX_STRING_LENGTH = 295;

        /// <summary>
        /// Postfix патч для метода getContent - логируем результат getRawData()
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(ItemBook __instance, ref object __result)
        {
            try
            {
                // Получаем результат getRawData() для текущего экземпляра ItemBook
                var rawData = __instance.getRawData();
                
                // Логируем информацию о книге
                TranslationMod.Logger?.LogInfo($"[ItemBookPatch] ItemBook.getContent() called:");
                TranslationMod.Logger?.LogInfo($"  - getRawData() result: {rawData.content}");
                
                // Если результат getContent не null, логируем и его
                if (__result != null)
                {
                    var translatedResult = new List<string>();
                    TranslationMod.Logger?.LogInfo($"  - getContent() result:");
                    List<string> list = __result as List<string>;
                    foreach (string item in list)
                    {
                        // Пропускаем пустые или null строки
                        if (string.IsNullOrWhiteSpace(item)) continue;
                        
                        string translatedItem = _translator.Value.Process(item);
                        TranslationMod.Logger?.LogInfo($"  - Item: {item}");
                        TranslationMod.Logger?.LogInfo($"  - Translated: {translatedItem}");
                        
                        TranslationMod.Logger?.LogInfo($"[ItemBookPatch] {translatedItem.Length}");
                        // Проверяем длину переведенной строки и разбиваем при необходимости
                        if (translatedItem.Length > MAX_STRING_LENGTH)
                        {
                            var splitItems = TextDataExtractor.SplitText(translatedItem, MAX_STRING_LENGTH);
                            translatedResult.AddRange(splitItems);
                            
                            TranslationMod.Logger?.LogInfo($"  - Split into {splitItems.Count} parts due to length ({translatedItem.Length} > {MAX_STRING_LENGTH})");
                        }
                        else
                        {
                            translatedResult.Add(translatedItem);
                        }
                    }
                    
                    // Добавляем пустую строку если количество элементов нечетное
                    if (translatedResult.Count % 2 != 0)
                    {
                        translatedResult.Add(" ");
                    }
                    
                    // ВАЖНО: Присваиваем обновленный список обратно в __result
                    __result = translatedResult;
                    
#if DEBUG
                    TranslationMod.Logger?.LogDebug($"[ItemBookPatch] Original list count: {list.Count}, New list count: {translatedResult.Count}");
#endif
                }
                else
                {
                    TranslationMod.Logger?.LogInfo($"  - getContent() result: null");
                }
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[ItemBookPatch] Error in Postfix patch: {ex.Message}");
#if DEBUG
                TranslationMod.Logger?.LogError($"[ItemBookPatch] Stack trace: {ex.StackTrace}");
#endif
            }
        }
    }
} 