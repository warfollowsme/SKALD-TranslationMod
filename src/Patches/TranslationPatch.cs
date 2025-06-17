// Patches/UITextBlockSetContentPatch.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
                if(ToolTipField != null)
                {
                    var tooltip = ToolTipField.GetValue(__instance) as ToolTipControl.ToolTipCategory;
                    if(tooltip != null)
                    {
                        TranslationMod.Logger?.LogInfo($"Tooltip:");
                        var keywords = tooltip.getKeywords();
                        foreach(var keyword in keywords)
                        {
                            TranslationMod.Logger?.LogInfo($"  - {keyword}");
                        }
                    }
                }
                // Проверяем текущий язык - если English, используем оригинальный метод
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null || currentLanguagePack.Name.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Выполняем оригинальный метод
                }
                
                // Сначала переводим текст
                string translatedText = _translator.Value.Process(__instance, __0);
                
                // Затем полная реимплементация setContent с переведенным текстом
                SetContentComplete(__instance, translatedText);
                
                // Возвращаем false, чтобы пропустить оригинальный метод
                return false;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"Error in custom setContent implementation: {ex.Message}");
                // В случае ошибки позволяем выполниться оригинальному методу
                return true;
            }
        }
        
        /// <summary>Полная реимплементация UITextBlock.setContent</summary>
        private static void SetContentComplete(UITextBlock instance, string input)
        {
            try
            {                
                if (instance.isContentEqual(input) || input == null || input.Length == 0)
                {
                    return;
                }
                
                if (PreProcessStringMethod == null)
                {
                    TranslationMod.Logger?.LogError("PreProcessStringMethod is null");
                    return;
                }
                
                instance.clearElements();
                
                if (ContentField == null)
                {
                    TranslationMod.Logger?.LogError("ContentField is null");
                    return;
                }
                
                ContentField.SetValue(instance, string.Copy(input));
                
                if (FontField == null)
                {
                    TranslationMod.Logger?.LogError("FontField is null");
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
                    int subimageForChar = StringPrinter.getSubimageForChar(input[0]);
                    
                    // Расширенная проверка для кириллицы (ваш комментарий: переделать на проверку взятых кодов из кодировки)
                    if (subimageForChar <= 25 || (subimageForChar >= 90 && subimageForChar <= 122))
                    {                        
                        if (IlluminatedImageField == null)
                        {
                            TranslationMod.Logger?.LogError("IlluminatedImageField is null");
                            return;
                        }
                        
                        var illuminatedImage = new UICanvasVertical();
                        input = input.Substring(1);
                        illuminatedImage.backgroundTexture = TextureTools.getLetterSubImageTextureData(
                            subimageForChar, instance.illuminatedFont.getModelPath());
                        illuminatedImage.padding.right = font.wordSpacing;
                        IlluminatedImageField.SetValue(instance, illuminatedImage);
                        instance.add(illuminatedImage);
                    }
                    else
                    {
                        TranslationMod.Logger?.LogInfo($"Character '{input[0]}' with subimage {subimageForChar} doesn't match illuminated criteria");
                    }
                }
                input = (string)PreProcessStringMethod.Invoke(instance, new object[] { input });
                
                if (IdentifyTooltipKeywordsMethod == null)
                {
                    TranslationMod.Logger?.LogError("IdentifyTooltipKeywordsMethod is null");
                    return;
                }
                
                input = (string)IdentifyTooltipKeywordsMethod.Invoke(instance, new object[] { input });
                
                if (SplitIntoParagraphMethod == null)
                {
                    TranslationMod.Logger?.LogError("SplitIntoParagraphMethod is null");
                    return;
                }
                var paragraphs = (List<string>)SplitIntoParagraphMethod.Invoke(instance, new object[] { input });
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



        /* ----------------------------------------------------------------- */
        /* 3.  TranslationService                                            */
        /* ----------------------------------------------------------------- */
        private sealed class TranslationService
        {

            private readonly Dictionary<string, string> _dict;
            private readonly Dictionary<string, string> _translationCache = new();
            private readonly HashSet<string> _missingKeys = new();
            private readonly HashSet<string> _loggedInputs = new();

            private readonly object _lockObject = new();
            private readonly string _missingKeysFilePath;

            public TranslationService() 
            {
                _dict = LoadCsv(GetCsvFiles());
                
                _missingKeysFilePath = GetMissingKeysFilePath();
                
                // Initialize translation buffer dictionary
                TranslationMod.Logger?.LogInfo("Translation cache initialized and ready");
            }

            /* ------------------------------------------------------------- */
            /* 3.1  Основной публичный метод                                 */
            /* ------------------------------------------------------------- */
            public string Process(UITextBlock textBlock, string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                
                // Check translation buffer dictionary
                lock (_lockObject)
                {
                    if (_translationCache.TryGetValue(input, out string cachedResult))
                    {
                        return cachedResult;
                    }
                }
                
                if (!_loggedInputs.Contains(input))
                {
                    TranslationMod.Logger?.LogInfo($"Processing new input: '{input}'");
                }
                
                try
                {
                    // Use GameTextParser to split text into parts
                    var sentences = GameTextParser.Parse(input);
                    
                    // Create template from original input
                    var template = CreateTemplate(input, sentences);
                    
                    var translatedSentences = new List<string>();
                    if (!_loggedInputs.Contains(input))
                    {
                        TranslationMod.Logger?.LogInfo($"Parser result:");
                    }
                    foreach (var sentence in sentences)
                    {                    
                        if (!_loggedInputs.Contains(input))
                        {
                            TranslationMod.Logger?.LogInfo($"   - '{sentence}'");
                        }
                        string translatedSentence = TranslateSentence(sentence);
                        translatedSentences.Add(translatedSentence);
                    }
                    if (!_loggedInputs.Contains(input))
                    {
                        _loggedInputs.Add(input);
                        TranslationMod.Logger?.LogInfo($"Template: '{template}'");
                    }

                    // Apply translated sentences to template
                    var result = ApplyTemplate(template, translatedSentences);

                    // Сохраняем результат в буферный словарь
                    lock (_lockObject)
                    {
                        if (!_translationCache.ContainsKey(input))
                        {
                            _translationCache[input] = result;
                        }
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    // В случае ошибки GameTextParser возвращаем оригинальный текст
                    TranslationMod.Logger?.LogWarning($"GameTextParser failed for input '{input}': {ex.Message}, returning original text");
                    
                    // Сохраняем оригинальный текст в кеш, чтобы не пытаться обработать его снова
                    lock (_lockObject)
                    {
                        if (!_translationCache.ContainsKey(input))
                        {
                            _translationCache[input] = input;
                        }
                    }
                    
                    return input;
                }
            }

            /* ------------------------------------------------------------- */
            /* 3.2  Перевод предложения (с использованием GameTextParser)    */
            /* ------------------------------------------------------------- */
            private string TranslateSentence(string sentence)
            {
                if (string.IsNullOrEmpty(sentence)) return sentence;

                // Ищем перевод для предложения
                string translated;
                if (!_dict.TryGetValue(sentence, out translated))
                {
                    translated = sentence;
                    SaveMissingKey(sentence);
                }

                return translated;
            }

            /// <summary>Saves missing key to need_translate.csv file</summary>
            private void SaveMissingKey(string key)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(_missingKeysFilePath)) 
                    return;

                lock (_lockObject)
                {
                    // Проверяем, не добавляли ли мы уже этот ключ
                    if (_missingKeys.Contains(key)) 
                        return;

                    _missingKeys.Add(key);

                    try
                    {
                        // Создаем директорию если не существует
                        string directory = Path.GetDirectoryName(_missingKeysFilePath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            TranslationMod.Logger?.LogInfo($"Created directory for missing keys: '{directory}'");
                        }

                        // Добавляем ключ в файл (append mode)
                        using (var writer = new StreamWriter(_missingKeysFilePath, true, System.Text.Encoding.UTF8))
                        {
                            // Экранируем ключ для CSV формата
                            string escapedKey = EscapeCsvValue(key);
                            writer.WriteLine($"{escapedKey},");
                            //TranslationMod.Logger?.LogInfo($"Saved missing key to need_translate.csv: '{key}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку, но не прерываем выполнение
                        TranslationMod.Logger?.LogError($"Failed to save missing translation key '{key}': {ex.Message}");
                    }
                }
            }

            /// <summary>Gets path to need_translate.csv file</summary>
            private static string GetMissingKeysFilePath()
            {
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null) return null;

                string languagePackDirectory = currentLanguagePack.DirectoryPath;
                //string translationsDirectory = Path.Combine(
                //    languagePackDirectory, currentLanguagePack.TranslationFilesPath);

                return Path.Combine(languagePackDirectory, "need_translate.csv");
            }

            /// <summary>Escapes value for CSV format</summary>
            private static string EscapeCsvValue(string value)
            {
                if (string.IsNullOrEmpty(value)) return value;
                
                // Если содержит кавычки, запятые или переносы строк - оборачиваем в кавычки
                if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
                {
                    // Удваиваем кавычки и оборачиваем в кавычки
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                
                return value;
            }

            /// <summary>
                /// Creates template from original input, replacing found sentences with placeholders {0}, {1}, {2}, etc.
            /// </summary>
            /// <param name="input">Original string</param>
            /// <param name="sentences">List of sentences found in string</param>
            /// <returns>Template with placeholders</returns>
            private static string CreateTemplate(string input, List<string> sentences)
            {
                if (string.IsNullOrEmpty(input) || sentences == null || sentences.Count == 0)
                    return input;

                string template = input;
                
                // Сортируем предложения по убыванию длины, чтобы сначала заменить более длинные
                // Это предотвращает случайную замену коротких предложений внутри длинных
                var sortedSentences = sentences
                    .Select((sentence, index) => new { Sentence = sentence, Index = index })
                    .Where(x => !string.IsNullOrEmpty(x.Sentence))
                    .OrderByDescending(x => x.Sentence.Length)
                    .ToList();

                foreach (var item in sortedSentences)
                {
                    string sentence = item.Sentence;
                    int originalIndex = item.Index;
                    
                    // Ищем первое вхождение предложения в шаблоне
                    int position = template.IndexOf(sentence, StringComparison.Ordinal);
                    
                    if (position >= 0)
                    {
                        // Заменяем найденное предложение на плейсхолдер
                        string placeholder = "{" + originalIndex + "}";
                        template = template.Substring(0, position) + 
                                  placeholder + 
                                  template.Substring(position + sentence.Length);
                    }
                }
                
                return template;
            }

            /// <summary>
            /// Применяет переведенные предложения к шаблону
            /// </summary>
            /// <param name="template">Шаблон с плейсхолдерами</param>
            /// <param name="translatedSentences">Список переведенных предложений</param>
            /// <returns>Итоговая строка с переводом</returns>
            private static string ApplyTemplate(string template, List<string> translatedSentences)
            {
                if (string.IsNullOrEmpty(template) || translatedSentences == null)
                    return template;

                string result = template;
                
                for (int i = 0; i < translatedSentences.Count; i++)
                {
                    string placeholder = "{" + i + "}";
                    string translation = translatedSentences[i] ?? string.Empty;
                    result = result.Replace(placeholder, translation);
                }
                
                return result;
            }

            /* ------------------------------------------------------------- */
            /* 3.4  Инфраструктура                                           */
            /* ------------------------------------------------------------- */
            
            /// <summary>Получаем список CSV-файлов из language-пакета.</summary>
            private static IEnumerable<string> GetCsvFiles()
            {
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null) return Enumerable.Empty<string>();

                string languagePackDirectory = currentLanguagePack.DirectoryPath;
                string translationsDirectory = Path.Combine(
                    languagePackDirectory, currentLanguagePack.TranslationFilesPath);

                if (!Directory.Exists(translationsDirectory))
                    return Enumerable.Empty<string>();

                return Directory.GetFiles(translationsDirectory, "*.csv");
            }

            private static Dictionary<string, string> LoadCsv(IEnumerable<string> csvPaths)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var path in csvPaths.OrderBy(p => p)) // детерминированный порядок
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;

                        var columns = ParseCsvLine(line);
                        if (columns.Length < 2) continue;   // нужна хотя бы Original;Translate

                        string original = columns[0];
                        string translation = columns[1];

                        // Первый встретившийся перевод выигрывает
                        if (!dict.ContainsKey(original))
                            dict[original] = translation;
                    }
                }

                return dict;
            }

            private static string[] ParseCsvLine(string line)
            {
                var result = new List<string>();
                bool inQuotes = false;
                var current = new System.Text.StringBuilder();

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    
                    if (c == '"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            // Двойная кавычка внутри кавычек - это экранированная кавычка
                            current.Append('"');
                            i++; // Пропускаем следующую кавычку
                        }
                        else
                        {
                            // Переключаем состояние "внутри кавычек"
                            inQuotes = !inQuotes;
                        }
                        continue;
                    }
                    
                    if (c == ',' && !inQuotes)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        continue;
                    }
                    
                    current.Append(c);
                }
                
                result.Add(current.ToString());
                return result.ToArray();
            }
        }
    }
}