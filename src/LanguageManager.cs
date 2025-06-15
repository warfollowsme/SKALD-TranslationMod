using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TranslationMod.Configuration;
using TranslationMod.Patches;

namespace TranslationMod
{
    /// <summary>
    /// Manager for handling languages and language packs
    /// </summary>
    public static class LanguageManager
    {
        public static event Action OnLanguageChanged;
        
        private static bool _isLanguageReady = false;
        private static FieldInfo stateField;
        private static FieldInfo alternativesField;
        
        // Текущий язык, определяемый игрой (хранится в памяти, не в конфиге)
        private static string _currentLanguageCode = ConfigKeys.EnglishLanguageCode;
        private static string _currentLanguageName = ConfigKeys.EnglishLanguageName;
        
        // Кэш языковых пакетов
        private static readonly ConcurrentDictionary<string, LanguagePack> _languagePacks = new();
        private static string _pluginDirectory;

        public static void Initialize()
        {
            if (_isLanguageReady) return;

            try
            {
                // Инициализируем путь к директории плагина
                _pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                
                // С помощью рефлексии получаем "описание" приватных полей,
                // которые нам нужны для чтения значения из CarouselSetting.
                var carouselSettingType = AccessTools.Inner(typeof(GlobalSettings.SettingsCollection), GameConstants.CarouselSettingType);
                if (carouselSettingType == null)
                {
                    TranslationMod.Logger?.LogError("[LanguageManager] Could not find CarouselSetting type via reflection");
                    return;
                }

                stateField = AccessTools.Field(carouselSettingType, GameConstants.CarouselStateField);
                alternativesField = AccessTools.Field(carouselSettingType, GameConstants.CarouselAlternativesField);
                
                if (stateField == null || alternativesField == null)
                {
                    TranslationMod.Logger?.LogError("[LanguageManager] Could not find required fields via reflection");
                    return;
                }
                
                // Инициализируем патч шрифтов  
                FontAssetPatch.Initialize();
                
                _isLanguageReady = true;
                TranslationMod.Logger?.LogInfo("[LanguageManager] Initialized successfully");
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Failed to initialize: {e.Message}");
            }
        }

        /// <summary>
        /// Switches plugin language. Loads new language pack and notifies subscribers.
        /// </summary>
        /// <param name="newLanguageName">New language name (e.g., "Russian")</param>
        public static void SwitchLanguage(string newLanguageName)
        {
            TranslationMod.Logger?.LogInfo($"[LanguageManager] Attempting to switch language to: {newLanguageName}");
            
            string newLanguageCode = GetLanguageCodeByName(newLanguageName) 
                                     ?? ConfigKeys.EnglishLanguageCode;

            // Сохраняем текущий язык для внутреннего использования (не в конфиг, а в память)
            _currentLanguageCode = newLanguageCode;
            _currentLanguageName = newLanguageName;
            
            LoadLanguagePackByCode(newLanguageCode);
            
            OnLanguageChanged?.Invoke();
            TranslationMod.Logger?.LogInfo($"[LanguageManager] Language switched to '{newLanguageName}' ({newLanguageCode}). Event invoked.");
        }

        /// <summary>
        /// Gets current language code (e.g., "ru").
        /// Language is determined by the game, not plugin config.
        /// </summary>
        public static string GetCurrentLanguageCode()
        {
            return _currentLanguageCode;
        }

        /// <summary>
        /// Gets current language name (e.g., "Russian").
        /// Language is determined by the game, not plugin config.
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return _currentLanguageName;
        }

        /// <summary>
        /// Gets currently loaded language pack.
        /// </summary>
        public static LanguagePack GetCurrentLanguagePack()
        {
            if (!_isLanguageReady)
            {
                // TranslationMod.Logger.LogWarning("[LanguageManager] GetCurrentLanguagePack called before language system is ready.");
                return null;
            }
            return GetLanguagePack(GetCurrentLanguageCode());
        }

        /// <summary>
        /// Trigger language change event
        /// </summary>
        public static void TriggerLanguageChange()
        {
            OnLanguageChanged?.Invoke();
            TranslationMod.Logger?.LogInfo("[LanguageManager] Language change event triggered manually");
        }

        /// <summary>
        /// Updates current language without loading language pack (for game synchronization)
        /// </summary>
        /// <param name="languageName">Language name</param>
        /// <param name="languageCode">Language code (optional)</param>
        internal static void UpdateCurrentLanguage(string languageName, string languageCode = null)
        {
            if (string.IsNullOrEmpty(languageName))
                return;

            _currentLanguageName = languageName;
            
            if (!string.IsNullOrEmpty(languageCode))
            {
                _currentLanguageCode = languageCode;
            }
            else
            {
                // Если код не указан, определяем его по имени
                _currentLanguageCode = GetLanguageCodeByName(languageName) 
                                     ?? ConfigKeys.EnglishLanguageCode;
            }
            
            TranslationMod.Logger?.LogDebug($"[LanguageManager] Current language updated to: '{_currentLanguageName}' ({_currentLanguageCode})");
        }

        /// <summary>
        /// Synchronizes LanguageManager state with current game settings
        /// </summary>
        public static void SynchronizeWithGame()
        {
            try
            {
                TranslationMod.Logger?.LogInfo("[LanguageManager] Starting initial language synchronization with the game");
                
                var gameplaySettings = GlobalSettings.getGamePlaySettings();
                if (gameplaySettings == null)
                {
                    TranslationMod.Logger?.LogWarning("[LanguageManager] GamePlaySettings not available for synchronization");
                    return;
                }

                var languageSetting = gameplaySettings.getObject(GameConstants.LanguageSettingId);
                if (languageSetting == null)
                {
                    TranslationMod.Logger?.LogInfo("[LanguageManager] Language setting not found in game, using default English");
                    UpdateCurrentLanguage(ConfigKeys.EnglishLanguageName, ConfigKeys.EnglishLanguageCode);
                    return;
                }

                // Извлекаем текущий язык из игровой настройки
                string currentGameLanguage = ExtractLanguageFromSetting(languageSetting);
                if (!string.IsNullOrEmpty(currentGameLanguage))
                {
                    TranslationMod.Logger?.LogInfo($"[LanguageManager] Synchronized with game language: '{currentGameLanguage}'");
                    SwitchLanguage(currentGameLanguage);
                }
                else
                {
                    TranslationMod.Logger?.LogWarning("[LanguageManager] Could not extract language from game setting, using default");
                    UpdateCurrentLanguage(ConfigKeys.EnglishLanguageName, ConfigKeys.EnglishLanguageCode);
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Error during game synchronization: {e.Message}");
                // Устанавливаем английский как безопасный fallback
                UpdateCurrentLanguage(ConfigKeys.EnglishLanguageName, ConfigKeys.EnglishLanguageCode);
            }
        }

        /// <summary>
        /// Extracts language name from settings object
        /// </summary>
        private static string ExtractLanguageFromSetting(object languageSetting)
        {
            try
            {
                if (languageSetting == null) return null;
                
                var instanceType = languageSetting.GetType();
                
                // Получаем текущее состояние и список альтернатив
                var stateField = AccessTools.Field(instanceType, GameConstants.CarouselStateField);
                var alternativesField = AccessTools.Field(instanceType, GameConstants.CarouselAlternativesField);
                
                if (stateField == null || alternativesField == null)
                {
                    TranslationMod.Logger?.LogDebug("[ExtractLanguageFromSetting] Required fields not found");
                    return null;
                }
                
                var stateValue = stateField.GetValue(languageSetting);
                var alternativesValue = alternativesField.GetValue(languageSetting);
                
                if (stateValue is int currentIndex && alternativesValue is System.Collections.IList alternatives)
                {
                    if (currentIndex >= 0 && currentIndex < alternatives.Count)
                    {
                        string selectedLanguage = alternatives[currentIndex]?.ToString();
                        TranslationMod.Logger?.LogDebug($"[ExtractLanguageFromSetting] Extracted language: '{selectedLanguage}' at index {currentIndex}");
                        return selectedLanguage;
                    }
                }
                
                return null;
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogDebug($"[ExtractLanguageFromSetting] Error: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Loads language pack by language code
        /// </summary>
        /// <param name="languageCode">Language code (e.g., "ru")</param>
        public static void LoadLanguagePackByCode(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode.Equals(ConfigKeys.EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                _languagePacks.Clear();
                TranslationMod.Logger?.LogInfo($"[LanguageManager] Switched to English. No language pack needed.");
                return;
            }

            _languagePacks.Clear();
            
            try
            {
                string languagePacksPath = Path.Combine(_pluginDirectory, ConfigurationManager.PluginConfig.LanguagePacksPath.Value);
                
                if (!Directory.Exists(languagePacksPath))
                {
                    TranslationMod.Logger?.LogWarning($"[LanguageManager] Language packs directory not found: {languagePacksPath}");
                    return;
                }

                var directories = Directory.GetDirectories(languagePacksPath);
                
                foreach (var directory in directories)
                {
                    string configFile = Path.Combine(directory, ConfigKeys.LanguagePackConfigFileName);
                    
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var languagePack = new LanguagePack(configFile, directory);
                            if (languagePack.IsValid() && 
                                string.Equals(languagePack.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
                            {
                                _languagePacks[languageCode] = languagePack;
                                TranslationMod.Logger?.LogInfo($"[LanguageManager] Successfully loaded language pack: {languagePack.Name} ({languageCode})");
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            TranslationMod.Logger?.LogWarning($"[LanguageManager] Failed to load language pack from '{directory}': {e.Message}");
                        }
                    }
                }
                
                TranslationMod.Logger?.LogWarning($"[LanguageManager] No language pack found for code: {languageCode}");
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Error loading language pack {languageCode}: {e.Message}");
            }
        }

        /// <summary>
        /// Gets language pack by language code
        /// </summary>
        /// <param name="languageCode">Language code (e.g., "ru")</param>
        /// <returns>Language pack or null for English/non-existing language</returns>
        public static LanguagePack GetLanguagePack(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode.Equals(ConfigKeys.EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
                return null;
            
            _languagePacks.TryGetValue(languageCode, out LanguagePack languagePack);
            return languagePack;
        }

        /// <summary>
        /// Get all available language packs (scans LanguagePacks folder)
        /// </summary>
        public static List<string> GetAvailableLanguageNames()
        {
            var result = new List<string>();
            
            try
            {
                string languagePacksPath = Path.Combine(_pluginDirectory, ConfigurationManager.PluginConfig.LanguagePacksPath.Value);
                
                if (!Directory.Exists(languagePacksPath))
                {
                    return result;
                }

                var directories = Directory.GetDirectories(languagePacksPath);
                
                foreach (var directory in directories)
                {
                    string configFile = Path.Combine(directory, ConfigKeys.LanguagePackConfigFileName);
                    
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var languagePack = new LanguagePack(configFile, directory);
                            if (languagePack.IsValid() && !string.IsNullOrEmpty(languagePack.Name))
                            {
                                result.Add(languagePack.Name);
                            }
                        }
                        catch (Exception e)
                        {
                            TranslationMod.Logger?.LogWarning($"[LanguageManager] Failed to load language pack from '{directory}': {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Error getting available languages: {e.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Get language code by its display name (scans folders)
        /// </summary>
        public static string GetLanguageCodeByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return ConfigKeys.EnglishLanguageCode;
                
            if (name.Equals(ConfigKeys.EnglishLanguageName, StringComparison.OrdinalIgnoreCase))
                return ConfigKeys.EnglishLanguageCode;
            
            try
            {
                string languagePacksPath = Path.Combine(_pluginDirectory, ConfigurationManager.PluginConfig.LanguagePacksPath.Value);
                
                if (!Directory.Exists(languagePacksPath))
                {
                    return ConfigKeys.EnglishLanguageCode;
                }

                var directories = Directory.GetDirectories(languagePacksPath);
                
                foreach (var directory in directories)
                {
                    string configFile = Path.Combine(directory, ConfigKeys.LanguagePackConfigFileName);
                    
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var languagePack = new LanguagePack(configFile, directory);
                            if (languagePack.IsValid() && 
                                string.Equals(languagePack.Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                return languagePack.LanguageCode;
                            }
                        }
                        catch (Exception e)
                        {
                            TranslationMod.Logger?.LogWarning($"[LanguageManager] Failed to check language pack from '{directory}': {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Error getting language code by name: {e.Message}");
            }
            
            return ConfigKeys.EnglishLanguageCode;
        }

        /// <summary>
        /// Get plugin directory (for internal use)
        /// </summary>
        public static string GetPluginDirectory()
        {
            return _pluginDirectory;
        }

        /// <summary>
        /// Check if specified language code is supported (language pack folder exists)
        /// </summary>
        public static bool IsLanguageSupported(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return false;
                
            if (languageCode.Equals(ConfigKeys.EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
                return true; // Английский всегда поддерживается
                
            try
            {
                string languagePacksPath = Path.Combine(_pluginDirectory, ConfigurationManager.PluginConfig.LanguagePacksPath.Value);
                
                if (!Directory.Exists(languagePacksPath))
                {
                    return false;
                }

                var directories = Directory.GetDirectories(languagePacksPath);
                
                foreach (var directory in directories)
                {
                    string configFile = Path.Combine(directory, ConfigKeys.LanguagePackConfigFileName);
                    
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var languagePack = new LanguagePack(configFile, directory);
                            if (languagePack.IsValid() && 
                                string.Equals(languagePack.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch (Exception e)
                        {
                            TranslationMod.Logger?.LogWarning($"[LanguageManager] Failed to check language pack from '{directory}': {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageManager] Error checking language support: {e.Message}");
            }
            
            return false;
        }
    }
} 