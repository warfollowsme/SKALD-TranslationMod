using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TranslationMod.Configuration
{
    /// <summary>
    /// Language pack using JSON configuration
    /// </summary>
    public class LanguagePack
    {
        private readonly string _configFilePath;
        private readonly string _packPath;
        private LanguagePackData _data;

        // Основная информация о языке
        public string LanguageCode => _data?.LanguageCode ?? "";
        public string Name => _data?.Name ?? "";
        public string Description => _data?.Description ?? "";
        public string Version => _data?.Version ?? "1.0.0";

        // Пути к файлам
        public string FontFilesPath => _data?.FontFilesPath ?? ConfigKeys.DefaultFontsDir;
        public string TranslationFilesPath => _data?.TranslationFilesPath ?? ConfigKeys.DefaultTranslationsDir;
        
        // Путь к папке языкового пакета
        public string DirectoryPath => _packPath;

        public LanguagePack(string configFilePath, string packPath)
        {
            _configFilePath = configFilePath;
            _packPath = packPath;
            LoadFromFile();
        }

        /// <summary>
        /// Loads data from JSON file
        /// </summary>
        private void LoadFromFile()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string jsonContent = File.ReadAllText(_configFilePath);
                    _data = JsonConvert.DeserializeObject<LanguagePackData>(jsonContent) ?? new LanguagePackData();
                    TranslationMod.Logger?.LogInfo($"[LanguagePack] Loaded language pack from JSON: {Name}");
                }
                else
                {
                    _data = new LanguagePackData();
                    TranslationMod.Logger?.LogWarning($"[LanguagePack] Config file not found, using defaults: {_configFilePath}");
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguagePack] Error loading JSON config: {e.Message}");
                _data = new LanguagePackData();
            }
        }

        /// <summary>
        /// Data structure for language pack JSON configuration
        /// </summary>
        [JsonObject]
        public class LanguagePackData
        {
            [JsonProperty("languageCode")]
            public string LanguageCode { get; set; } = "";

            [JsonProperty("name")]
            public string Name { get; set; } = "";

            [JsonProperty("description")]
            public string Description { get; set; } = "";

            [JsonProperty("version")]
            public string Version { get; set; } = "1.0.0";

            [JsonProperty("fontFilesPath")]
            public string FontFilesPath { get; set; } = ConfigKeys.DefaultFontsDir;

            [JsonProperty("translationFilesPath")]
            public string TranslationFilesPath { get; set; } = ConfigKeys.DefaultTranslationsDir;

            [JsonProperty("characterChart")]
            public Dictionary<string, int> CharacterChart { get; set; } = new Dictionary<string, int>();
        }

        /// <summary>
        /// Get character map as Dictionary from JSON data
        /// </summary>
        public Dictionary<char, int> GetCharacterChart()
        {
            var result = new Dictionary<char, int>();

            try
            {
                if (_data?.CharacterChart != null)
                {
                    foreach (var kvp in _data.CharacterChart)
                    {
                        // Ключи должны быть символами длиной 1
                        if (kvp.Key.Length == 1)
                        {
                            result[kvp.Key[0]] = kvp.Value;
                            TranslationMod.Logger?.LogDebug($"[LanguagePack] Mapped character '{kvp.Key[0]}' to position {kvp.Value}");
                        }
                        else
                        {
                            TranslationMod.Logger?.LogWarning($"[LanguagePack] Skipped invalid character key: '{kvp.Key}' (length: {kvp.Key.Length}, expected: 1 character)");
                        }
                    }
                }

                if (result.Count == 0)
                {
                    TranslationMod.Logger?.LogWarning($"[LanguagePack] CharacterChart is empty for language '{Name}'");
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguagePack] Error parsing character chart for '{Name}': {e.Message}");
            }

            TranslationMod.Logger?.LogInfo($"[LanguagePack] Loaded {result.Count} characters from CharacterChart for language '{Name}'");
            return result;
        }

        /// <summary>
        /// Get full path to fonts folder
        /// </summary>
        public string GetFontsPath()
        {
            return Path.Combine(_packPath, FontFilesPath);
        }

        /// <summary>
        /// Get full path to translations folder
        /// </summary>
        public string GetTranslationsPath()
        {
            return Path.Combine(_packPath, TranslationFilesPath);
        }

        /// <summary>
        /// Validate language pack correctness
        /// </summary>
        public bool IsValid()
        {
            try
            {
                // Проверяем обязательные поля
                if (string.IsNullOrEmpty(LanguageCode) || 
                    string.IsNullOrEmpty(Name))
                {
                    return false;
                }

                // Проверяем, что код языка в правильном формате (2-3 символа)
                if (LanguageCode.Length < 2 || LanguageCode.Length > 3)
                {
                    return false;
                }

                // Проверяем существование папок
                if (!Directory.Exists(GetFontsPath()) || !Directory.Exists(GetTranslationsPath()))
                {
                    TranslationMod.Logger?.LogWarning($"[LanguagePack] Missing directories for language pack: {Name}");
                    // Создаем отсутствующие папки
                    Directory.CreateDirectory(GetFontsPath());
                    Directory.CreateDirectory(GetTranslationsPath());
                }

                return true;
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguagePack] Error validating language pack: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save language pack configuration to JSON file
        /// </summary>
        public void Save()
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(_configFilePath, jsonContent);
                TranslationMod.Logger?.LogInfo($"[LanguagePack] Saved language pack: {Name}");
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguagePack] Error saving language pack: {e.Message}");
            }
        }
    }
} 