using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TranslationMod.Configuration;

namespace TranslationMod
{
    /// <summary>
    /// Сервис для перевода текста с использованием CSV-файлов переводов
    /// </summary>
    public sealed class TranslationService
    {
        private readonly Dictionary<string, string> _dict;
        private readonly Dictionary<string, string> _translationCache = new();
        private readonly HashSet<string> _missingKeys = new();
        private readonly HashSet<string> _loggedInputs = new();
        private readonly HashSet<string> _loggedTranslations = new();
        private readonly HashSet<string> _loggedTitleCaseHits = new();

        private readonly object _lockObject = new();
        private readonly string _missingKeysFilePath;

        /// <summary>
        /// Регулярное выражение для проверки, что строка состоит только из заглавных букв, цифр, пробелов и знаков препинания
        /// и содержит хотя бы одну заглавную букву
        /// </summary>
        //private static readonly Regex AllUpperCaseRegex = new Regex(@"^(?=.*[A-Z])[A-Z0-9 \p{P}]+$", RegexOptions.Compiled);
        private static readonly Regex AllUpperCaseRegex =
            new(@"^(?=.*\p{Lu})[0-9\p{P}\p{Lu}\s]+$", RegexOptions.CultureInvariant);
        public TranslationService() 
        {
            _dict = LoadCsv(GetCsvFiles());
            
            _missingKeysFilePath = GetMissingKeysFilePath();
            
            // Initialize translation buffer dictionary
            TranslationMod.Logger?.LogInfo($"[TranslationService] Initialized with {_dict.Count} translations loaded from CSV files");
            TranslationMod.Logger?.LogInfo($"[TranslationService] Missing keys will be saved to: {_missingKeysFilePath}");
        }

        /// <summary>
        /// Основной метод для перевода текста
        /// </summary>
        /// <param name="input">Исходный текст</param>
        /// <returns>Переведенный текст</returns>
        public string Process(string input)
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
                // Логируем только первый раз для каждого input
                TranslationMod.Logger?.LogDebug($"[TranslationService] Processing new input: '{input}'");
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
                    //TranslationMod.Logger?.LogInfo($"Parser result:");
                }
                foreach (var sentence in sentences)
                {                    
                    if (!_loggedInputs.Contains(input))
                    {
                        //TranslationMod.Logger?.LogInfo($"   - '{sentence}'");
                    }
                    string translatedSentence = TranslateSentence(sentence);
                    translatedSentences.Add(translatedSentence);
                }
                if (!_loggedInputs.Contains(input))
                {
                    _loggedInputs.Add(input);
                    //TranslationMod.Logger?.LogInfo($"Template: '{template}'");
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

        /// <summary>
        /// Перевод предложения (с использованием GameTextParser)
        /// </summary>
        /// <param name="sentence">Предложение для перевода</param>
        /// <returns>Переведенное предложение</returns>
        private string TranslateSentence(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return sentence;

            // Ищем перевод для предложения
            string translated;
            bool foundDirectTranslation = _dict.TryGetValue(sentence, out translated);
            
            if (!foundDirectTranslation)
            {
                TranslationMod.Logger?.LogInfo($"[TranslationService] Key not found: '{sentence}'");
                
                // Дополнительный поиск: если input состоит только из заглавных букв,
                // преобразуем в Title Case и ищем снова
                if (IsAllUpperCase(sentence))
                {
                    string titleCaseVersion = ConvertToTitleCase(sentence);

                    TranslationMod.Logger?.LogInfo($"[TranslationService] Key is CAPS. TitleCase: '{titleCaseVersion}'");
                    if (_dict.TryGetValue(titleCaseVersion, out string titleCaseTranslated))
                    {
                        // Найден перевод для Title Case версии - преобразуем его в ЗАГЛАВНЫЕ БУКВЫ
                        translated = titleCaseTranslated.ToUpper();
                        
                        // Логируем успешное нахождение через Title Case (дедупликация)
                        LogTitleCaseHit(sentence, titleCaseVersion, translated);
                    }
                    else
                    {
                        translated = sentence;
                        SaveMissingKey(sentence);
                    }
                }
                else
                {
                    TranslationMod.Logger?.LogInfo($"[TranslationService] Key is not CAPS");
                    translated = sentence;
                    SaveMissingKey(sentence);
                }
            }
            else
            {
                // Логируем успешные прямые переводы (дедупликация)
                LogDirectTranslation(sentence, translated);
            }

            return translated;
        }

        /// <summary>Сохраняет отсутствующий ключ в файл need_translate.csv</summary>
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
                        TranslationMod.Logger?.LogInfo($"[TranslationService] Created directory for missing keys: '{directory}'");
                    }

                    // Добавляем ключ в файл (append mode)
                    using (var writer = new StreamWriter(_missingKeysFilePath, true, System.Text.Encoding.UTF8))
                    {
                        // Экранируем ключ для CSV формата
                        string escapedKey = EscapeCsvValue(key);
                        writer.WriteLine($"{escapedKey},");
                    }
                    
                    // Логируем добавление нового отсутствующего ключа
                    TranslationMod.Logger?.LogWarning($"[TranslationService] Missing translation key added to need_translate.csv: '{key}'");
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но не прерываем выполнение
                    TranslationMod.Logger?.LogError($"[TranslationService] Failed to save missing translation key '{key}': {ex.Message}");
                }
            }
        }

        /// <summary>Логирует успешный прямой перевод (с дедупликацией)</summary>
        private void LogDirectTranslation(string original, string translated)
        {
            lock (_lockObject)
            {
                if (!_loggedTranslations.Contains(original))
                {
                    _loggedTranslations.Add(original);
                    TranslationMod.Logger?.LogInfo($"[TranslationService] Direct translation: '{original}' -> '{translated}'");
                }
            }
        }

        /// <summary>Логирует успешное нахождение перевода через Title Case (с дедупликацией)</summary>
        private void LogTitleCaseHit(string original, string titleCaseVersion, string finalTranslation)
        {
            lock (_lockObject)
            {
                if (!_loggedTitleCaseHits.Contains(original))
                {
                    _loggedTitleCaseHits.Add(original);
                    TranslationMod.Logger?.LogInfo($"[TranslationService] Title Case hit: '{original}' -> '{titleCaseVersion}' -> '{finalTranslation}'");
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
        /// Проверяет, состоит ли строка только из заглавных букв, цифр, пробелов и знаков препинания
        /// и содержит хотя бы одну заглавную букву
        /// </summary>
        /// <param name="input">Строка для проверки</param>
        /// <returns>true если соответствует паттерну заглавных букв</returns>
        private static bool IsAllUpperCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            input = Regex.Replace(input, @"\s+", " ").Trim();
            return AllUpperCaseRegex.IsMatch(input);
        }

        /// <summary>
        /// Преобразует строку в Title Case (первая буква каждого слова заглавная, остальные маленькие)
        /// Оптимизировано для английского языка
        /// </summary>
        /// <param name="input">Исходная строка</param>
        /// <returns>Строка в Title Case</returns>
        private static string ConvertToTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // приводим к lower-case один раз
            var parts = Regex.Split(input.ToLowerInvariant(), @"(\s+)"); // сохраняем пробелы

            for (int i = 0; i < parts.Length; i++)
            {
                // если это разделитель (пробелы / табы) — пропускаем
                if (Regex.IsMatch(parts[i], @"^\s+$")) continue;

                // слово «of» — оставляем строчным, кроме первого слова всей фразы
                if (i != 0 && parts[i] == "of") continue;

                // капитализируем первую буквенную позицию слова
                parts[i] = Regex.Replace(parts[i], @"^\p{L}",
                            m => m.Value.ToUpperInvariant());
            }

            return string.Concat(parts);
        }

        /// <summary>
        /// Создает шаблон из исходного текста, заменяя найденные предложения на плейсхолдеры {0}, {1}, {2}, etc.
        /// </summary>
        /// <param name="input">Исходная строка</param>
        /// <param name="sentences">Список предложений найденных в строке</param>
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
            var current = new StringBuilder();

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