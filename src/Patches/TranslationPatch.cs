// Patches/UITextBlockSetContentPatch.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Harmony-патч, перехватывающий UITextBlock.setContent(string)
    /// и вставляющий переведённый текст.
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
        /* 2.  Prefix: меняем аргумент метода                                */
        /* ----------------------------------------------------------------- */

        private static void Prefix(ref string __0) => __0 = _translator.Value.Process(__0);

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
                
                // Инициализируем буферный словарь переводов
                TranslationMod.Logger?.LogInfo("Translation cache initialized and ready");
            }

            /* ------------------------------------------------------------- */
            /* 3.1  Основной публичный метод                                 */
            /* ------------------------------------------------------------- */
            public string Process(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;

                // Проверяем буферный словарь переведенных строк
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
                    // Используем GameTextParser для разбивки текста на части
                    var sentences = GameTextParser.Parse(input);
                    
                    // Создаем шаблон из оригинального input
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

                    // Применяем переведенные предложения к шаблону
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





            /// <summary>Сохраняет не найденный ключ в файл need_translate.csv</summary>
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

            /// <summary>Получает путь к файлу need_translate.csv</summary>
            private static string GetMissingKeysFilePath()
            {
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null) return null;

                string languagePackDirectory = currentLanguagePack.DirectoryPath;
                //string translationsDirectory = Path.Combine(
                //    languagePackDirectory, currentLanguagePack.TranslationFilesPath);

                return Path.Combine(languagePackDirectory, "need_translate.csv");
            }

            /// <summary>Экранирует значение для CSV формата</summary>
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
            /// Создает шаблон из оригинального input, заменяя найденные предложения на плейсхолдеры {0}, {1}, {2} и т.д.
            /// </summary>
            /// <param name="input">Оригинальная строка</param>
            /// <param name="sentences">Список предложений, найденных в строке</param>
            /// <returns>Шаблон с плейсхолдерами</returns>
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