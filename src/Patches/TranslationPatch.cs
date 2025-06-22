// Patches/UITextBlockSetContentPatch.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Harmony patch intercepting UITextBlock.setContent(string)
    /// and inserting translated text.
    /// </summary>
    [HarmonyPatch(typeof(UITextBlock), nameof(UITextBlock.setContent), new[] { typeof(string) })]
    public static class UITextBlockSetContentPatch
    {
        /* ----------------------------------------------------------------- */
        /* 1.  Lazy-инициализация переводчика                               */
        /* ----------------------------------------------------------------- */

        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());

        /* ----------------------------------------------------------------- */
        /* 1.0  Буфер для tooltip ключей: переведенный ключ -> оригинальный ключ */
        /* ----------------------------------------------------------------- */
        
        /// <summary>
        /// Буфер tooltip ключей: переведенный ключ -> оригинальный ключ
        /// </summary>
        public static readonly Dictionary<string, string> TooltipKeyBuffer = new();
        
        /// <summary>
        /// Regex для извлечения tooltip ключей из тегов <tag>tooltipKey</tag>
        /// </summary>
        private static readonly Regex TooltipTagRegex = new(@"<tag>([^<]+)</tag>", RegexOptions.Compiled);
        
        /// <summary>
        /// Объект для синхронизации доступа к буферу
        /// </summary>
        private static readonly object _tooltipBufferLock = new();

        /* ----------------------------------------------------------------- */
        /* 1.1  Рефлексия для доступа к приватным полям и методам            */
        /* ----------------------------------------------------------------- */
        
        private static FieldInfo _illuminatedImageField;
        private static FieldInfo _fontField;
        private static FieldInfo _contentField;
        private static FieldInfo _tooltipField;
        
        private static MethodInfo _preProcessStringMethod;
        private static MethodInfo _identifyTooltipKeywordsMethod;
        private static MethodInfo _splitIntoParagraphMethod;
        private static MethodInfo _parseParagraphMethod;
        private static MethodInfo _pruneLengthMethod;
        
        /// <summary>Ленивая инициализация FieldInfo для illuminatedImage</summary>
        private static FieldInfo IlluminatedImageField
        {
            get
            {
                if (_illuminatedImageField == null)
                    _illuminatedImageField = typeof(UITextBlock).GetField("illuminatedImage", BindingFlags.NonPublic | BindingFlags.Instance);
                return _illuminatedImageField;
            }
        }
        
        /// <summary>Ленивая инициализация FieldInfo для font</summary>
        private static FieldInfo FontField
        {
            get
            {
                if (_fontField == null)
                    _fontField = typeof(UITextBlock).GetField("font", BindingFlags.NonPublic | BindingFlags.Instance);
                return _fontField;
            }
        }
        
        /// <summary>Ленивая инициализация FieldInfo для content</summary>
        private static FieldInfo ContentField
        {
            get
            {
                if (_contentField == null)
                    _contentField = typeof(UITextBlock).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance);
                return _contentField;
            }
        }

        /// <summary>Ленивая инициализация FieldInfo для content</summary>
        private static FieldInfo ToolTipField
        {
            get
            {
                if (_tooltipField == null)
                    _tooltipField = typeof(UITextBlock).GetField("toolTips", BindingFlags.NonPublic | BindingFlags.Instance);
                return _tooltipField;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для preProcessString</summary>
        private static MethodInfo PreProcessStringMethod
        {
            get
            {
                if (_preProcessStringMethod == null)
                    _preProcessStringMethod = typeof(UITextBlock).GetMethod("preProcessString", BindingFlags.NonPublic | BindingFlags.Instance);
                return _preProcessStringMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для identifyTooltipKeywords</summary>
        private static MethodInfo IdentifyTooltipKeywordsMethod
        {
            get
            {
                if (_identifyTooltipKeywordsMethod == null)
                    _identifyTooltipKeywordsMethod = typeof(UITextBlock).GetMethod("identifyTooltipKeywords", BindingFlags.NonPublic | BindingFlags.Instance);
                return _identifyTooltipKeywordsMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для splitIntoParagraph</summary>
        private static MethodInfo SplitIntoParagraphMethod
        {
            get
            {
                if (_splitIntoParagraphMethod == null)
                    _splitIntoParagraphMethod = typeof(UITextBlock).GetMethod("splitIntoParagraph", BindingFlags.NonPublic | BindingFlags.Instance);
                return _splitIntoParagraphMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для parseParagraph</summary>
        private static MethodInfo ParseParagraphMethod
        {
            get
            {
                if (_parseParagraphMethod == null)
                    _parseParagraphMethod = typeof(UITextBlock).GetMethod("parseParagraph", BindingFlags.NonPublic | BindingFlags.Instance);
                return _parseParagraphMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для pruneLength</summary>
        private static MethodInfo PruneLengthMethod
        {
            get
            {
                if (_pruneLengthMethod == null)
                    _pruneLengthMethod = typeof(UITextBlock).GetMethod("pruneLength", BindingFlags.NonPublic | BindingFlags.Instance);
                return _pruneLengthMethod;
            }
        }

        /* ----------------------------------------------------------------- */
        /* 2.  Prefix: меняем аргумент метода и полная реимплементация       */
        /* ----------------------------------------------------------------- */

        private static bool Prefix(UITextBlock __instance, string __0)
        {
            try
            {
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null || currentLanguagePack.Name.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Выполняем оригинальный метод
                }

                // Сначала переводим текст
                string translatedText = _translator.Value.Process(__0);
                
                // Затем полная реимплементация setContent с переведенным текстом
                SetContentComplete(__instance, __0, translatedText);
                
                // Возвращаем false, чтобы пропустить оригинальный метод
                return false;
            }
            catch (Exception ex)
            {
#if DEBUG
                TranslationMod.Logger?.LogError($"Error in custom setContent implementation: {ex.Message}");
#endif
                // В случае ошибки позволяем выполниться оригинальному методу
                return true;
            }
        }
        
        /// <summary>Полная реимплементация UITextBlock.setContent</summary>
        private static void SetContentComplete(UITextBlock instance, string input, string translated)
        {
            try
            {                
                if (instance.isContentEqual(input) || input == null || input.Length == 0)
                {
                    return;
                }
                
                if (PreProcessStringMethod == null)
                {
#if DEBUG
                    TranslationMod.Logger?.LogError("PreProcessStringMethod is null");
#endif
                    return;
                }
                
                instance.clearElements();
                
                if (ContentField == null)
                {
#if DEBUG
                    TranslationMod.Logger?.LogError("ContentField is null");
#endif
                    return;
                }
                
                ContentField.SetValue(instance, string.Copy(input));
                
                if (FontField == null)
                {
#if DEBUG
                    TranslationMod.Logger?.LogError("FontField is null");
#endif
                    return;
                }
                
                var font = FontField.GetValue(instance) as Font;
                if (font == null)
                {
                    TranslationMod.Logger?.LogError("No font set for text box:" + input);
                    MainControl.logError("No font set for text box:" + input);
                    return;
                }
                
                if (instance.illuminatedFont != null)
                {
                    int subimageForChar = StringPrinter.getSubimageForChar(translated[0]);
                    
                    // Расширенная проверка для кириллицы (ваш комментарий: переделать на проверку взятых кодов из кодировки)
                    if (subimageForChar <= 25 || (subimageForChar >= 90 && subimageForChar <= 122))
                    {                        
                        if (IlluminatedImageField == null)
                        {
                            TranslationMod.Logger?.LogError("IlluminatedImageField is null");
                            return;
                        }
                        
                        var illuminatedImage = new UICanvasVertical();
                        translated = translated.Substring(1);
                        illuminatedImage.backgroundTexture = TextureTools.getLetterSubImageTextureData(
                            subimageForChar, instance.illuminatedFont.getModelPath());
                        illuminatedImage.padding.right = font.wordSpacing;
                        IlluminatedImageField.SetValue(instance, illuminatedImage);
                        instance.add(illuminatedImage);
                    }
                    else
                    {
#if DEBUG
                        TranslationMod.Logger?.LogInfo($"Character '{translated[0]}' with subimage {subimageForChar} doesn't match illuminated criteria");
#endif
                    }
                }
                translated = (string)PreProcessStringMethod.Invoke(instance, new object[] { translated });
                
                string taggedInput = identifyTooltipKeywords(instance, input);
                if(taggedInput != input)
                {
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"Tagged input: {taggedInput}");
#endif
                    
                    // Извлекаем tooltip ключи и добавляем их в буфер
                    var keys = ExtractAndBufferTooltipKeys(taggedInput);
                    
                    // Оборачиваем переведенные ключи в теги <tag></tag>
                    translated = TagKeys(translated, keys);

#if DEBUG
                    TranslationMod.Logger?.LogInfo($"Translated tagged input: {translated}");
#endif
                }

                if (SplitIntoParagraphMethod == null)
                {
                    TranslationMod.Logger?.LogError("SplitIntoParagraphMethod is null");
                    return;
                }
                var paragraphs = (List<string>)SplitIntoParagraphMethod.Invoke(instance, new object[] { translated });
                if (paragraphs != null)
                {
                    if (ParseParagraphMethod == null)
                    {
                        TranslationMod.Logger?.LogError("ParseParagraphMethod is null");
                        return;
                    }
                    
                    foreach (string paragraph in paragraphs)
                    {
                        ParseParagraphMethod.Invoke(instance, new object[] { paragraph });
                    }
                }
                
                if (PruneLengthMethod == null)
                {
                    TranslationMod.Logger?.LogError("PruneLengthMethod is null");
                    return;
                }
                
                PruneLengthMethod.Invoke(instance, null);
                
                instance.alignElements();                
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"Exception in SetContentComplete: {ex.Message}");
                TranslationMod.Logger?.LogError($"Stack trace: {ex.StackTrace}");
                throw; // Перебрасываем исключение для обработки в Prefix
            }
        }

        /// <summary>
        /// Извлекает tooltip ключи из тегов <tag>tooltipKey</tag>, переводит их и добавляет в буфер
        /// </summary>
        /// <param name="input">Исходный текст с тегами</param>
        private static Dictionary<string, string> ExtractAndBufferTooltipKeys(string input)
        {
            Dictionary<string, string> keys = new Dictionary<string, string>();
            try
            {
                var matches = TooltipTagRegex.Matches(input);
                if (matches.Count == 0)
                {
#if DEBUG
                    TranslationMod.Logger?.LogDebug($"[TooltipBuffer] No tooltip tags found in: {input}");
#endif
                    return keys;
                }
#if DEBUG
                TranslationMod.Logger?.LogDebug($"[TooltipBuffer] Match Count: {matches.Count}");
#endif 
                lock (_tooltipBufferLock)
                {
                    foreach (Match match in matches)
                    {
#if DEBUG
                        TranslationMod.Logger?.LogDebug($"[TooltipBuffer] Match Count: {match.Groups.Count}");
#endif      
                        if (match.Groups.Count > 1)
                        {          
                            string originalKey = match.Groups[1].Value;
                            if (string.IsNullOrWhiteSpace(originalKey))
                                continue;

                            // Переводим ключ
                            string translatedKey = _translator.Value.Process(originalKey);
                            if(!keys.ContainsKey(translatedKey))
                            {
                                keys.Add(translatedKey, originalKey);
                            }
                        }
                    }
                }
                return keys;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[TooltipBuffer] Error extracting tooltip keys: {ex.Message}");
                return keys;
            }
        }

        private static string identifyTooltipKeywords(UITextBlock instance, string input)
        {
            if (IdentifyTooltipKeywordsMethod == null)
            {
                TranslationMod.Logger?.LogError("IdentifyTooltipKeywordsMethod is null");
                return input;
            }
            return (string)IdentifyTooltipKeywordsMethod.Invoke(instance, new object[] { input });
        }


        public static string HighlightKeysInText(string input, List<string> keys)
        {
            if (string.IsNullOrWhiteSpace(input) || keys == null || keys.Count == 0)
                return input;

            // Сортируем ключи по убыванию длины для приоритета длинных совпадений
            var orderedKeys = keys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderByDescending(k => k.Length)
                .ToList();

            foreach (var key in orderedKeys)
            {
                // Удаляем лишние пробелы и приводим к нижнему регистру для сопоставления
                string normalizedKey = key.Trim();

                // Пытаемся найти слова, начинающиеся на ключ и допускающие падежные окончания
                string pattern = $@"\b({Regex.Escape(normalizedKey)}\p{{L}}*)\b";

                input = Regex.Replace(
                    input,
                    pattern,
                    match =>
                    {
                        // Уже содержит тег — пропускаем
                        if (match.Value.Contains("<tag>") || match.Value.Contains("</tag>"))
                            return match.Value;

                        // Заворачиваем найденное совпадение
                        return $"<tag>{match.Value}</tag>";
                    },
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return input;
        }
        
        private struct PatternInfo
        {
            public string OriginalKey { get; set; }
            public string Key { get; set; }
            public Regex Rx { get; set; }
        }
        /// <summary>
        /// Оборачивает найденные ключи в <tag></tag> с предотвращением дублирования.
        /// </summary>
        public static string TagKeys(string text, Dictionary<string, string> dict)
        {
            if (string.IsNullOrWhiteSpace(text) || dict == null)
                return text;

            try
            {
                // Набор для отслеживания уже обработанных позиций
                var processedRanges = new List<(int start, int end)>();
                
                // 1) строим Regex-паттерны для всех ключей (длинные → первыми)
                var patternInfos = dict
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .OrderByDescending(kvp => kvp.Key.Length)
                    .Select(kvp => 
                    {
                        try
                        {
                            var pattern = BuildPattern(kvp.Key);
#if DEBUG
                            TranslationMod.Logger?.LogInfo($"[TagKeys] Pattern: {pattern}");
#endif
                            if (string.IsNullOrEmpty(pattern))
                                return (PatternInfo?)null;
                            

                            return (PatternInfo?)new PatternInfo
                            {
                                OriginalKey = kvp.Value,
                                Key = kvp.Key,
                                Rx  = new Regex(
                                        pattern,
                                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                            };
                        }
                        catch (Exception ex)
                        {
                            TranslationMod.Logger?.LogError($"[TagKeys] Error building pattern for key '{kvp.Key}': {ex.Message}");
                            return (PatternInfo?)null;
                        }
                    })
                    .Where(rx => rx.HasValue)
                    .Select(rx => rx.Value)
                    .ToArray();

                // 2) Собираем все совпадения с их позициями
                var allMatches = new List<(Match match, PatternInfo pattern, int priority)>();
                
                for (int i = 0; i < patternInfos.Length; i++)
                {
                    var p = patternInfos[i];
                    try
                    {
                        var matches = p.Rx.Matches(text);
                        foreach (Match match in matches)
                        {
                            // Проверяем, что совпадение не содержит уже тег
                            if (!match.Value.Contains("<tag>") && !match.Value.Contains("</tag>"))
                            {
                                allMatches.Add((match, p, i)); // i как приоритет (меньше = выше приоритет)
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TranslationMod.Logger?.LogError($"[TagKeys] Error finding matches for pattern: {ex.Message}");
                    }
                }

                // 3) Сортируем по приоритету (длина ключа) и позиции
                allMatches = allMatches
                    .OrderBy(x => x.priority) // Сначала по приоритету (длинные ключи первыми)
                    .ThenBy(x => x.match.Index) // Затем по позиции в тексте
                    .ToList();

                // 4) Отфильтровываем пересекающиеся совпадения
                var finalMatches = new List<(Match match, PatternInfo pattern)>();
                
                foreach (var (match, pattern, _) in allMatches)
                {
                    int start = match.Index;
                    int end = match.Index + match.Length - 1;
                    
                    // Проверяем, не пересекается ли с уже выбранными совпадениями
                    bool overlaps = processedRanges.Any(range => 
                        !(end < range.start || start > range.end));
                    
                    if (!overlaps)
                    {
                        finalMatches.Add((match, pattern));
                        processedRanges.Add((start, end));
#if DEBUG
                        TranslationMod.Logger?.LogDebug($"[TagKeys] Added match: '{match.Value}' at {start}-{end}");
#endif
                    }
                    else
                    {
#if DEBUG
                        TranslationMod.Logger?.LogDebug($"[TagKeys] Skipped overlapping match: '{match.Value}' at {start}-{end}");
#endif
                    }
                }

                // 5) Применяем замены в обратном порядке (от конца к началу), чтобы не сбить позиции
                finalMatches = finalMatches.OrderByDescending(x => x.match.Index).ToList();
                
                foreach (var (match, pattern) in finalMatches)
                {
                    try
                    {
                        string matchValue = match.Value;
                        string replacement = $"<tag>{matchValue}</tag>";
                        
                        // Добавляем в буфер tooltip ключей
                        if (!TooltipKeyBuffer.ContainsKey(matchValue) && matchValue != pattern.OriginalKey)
                        {
                            TooltipKeyBuffer.Add(matchValue, pattern.OriginalKey);
                        }
                        
                        // Заменяем в тексте
                        text = text.Substring(0, match.Index) + replacement + text.Substring(match.Index + match.Length);
                        
#if DEBUG
                        TranslationMod.Logger?.LogDebug($"[TagKeys] Replaced '{matchValue}' with '{replacement}'");
#endif
                    }
                    catch (Exception ex)
                    {
                        TranslationMod.Logger?.LogError($"[TagKeys] Error applying replacement: {ex.Message}");
                    }
                }

                return text;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[TagKeys] General error in TagKeys: {ex.Message}");
                return text; // Возвращаем исходный текст в случае ошибки
            }
        }

        /* ── PRIVATE ──────────────────────────────────────────── */

        /// строит приближённый паттерн по ключу
        private static string BuildPattern(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            // «Очки развития» → «Очк\w* \s+ разви\w*»
            var tokens = key.Split(new[] { ' ', '\t' },
                                StringSplitOptions.RemoveEmptyEntries);

            var parts = tokens.Select(t =>
            {
                if (string.IsNullOrEmpty(t))
                    return string.Empty;

                // берём 85 % слова (но ≥ 3 символов), остальное – любое окончание
                int keep = Math.Max(3, (int)Math.Ceiling(t.Length * 0.65));
                // Убеждаемся, что keep не превышает длину строки
                keep = Math.Min(keep, t.Length);
                
                // Для очень коротких слов (1-2 символа) используем полное слово
                if (t.Length <= 2)
                    keep = t.Length;
                
                string stem = Regex.Escape(t.Substring(0, keep));
                return stem + @"[\w\.-]*";
            }).Where(p => !string.IsNullOrEmpty(p));

            return $@"\b{string.Join(@"\s+", parts)}\b";
        }
    }
}