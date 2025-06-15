using BepInEx.Configuration;
using System;
using System.IO;
using System.Reflection;

namespace TranslationMod.Configuration
{
    public static class ConfigurationManager
    {
        private static bool _isInitialized;
        private static string _pluginDirectory;
        private static ConfigFile _configFile;
        public static PluginConfig PluginConfig { get; private set; }



        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string configPath = Path.Combine(_pluginDirectory, ConfigKeys.MainConfigFileName);

                _configFile = new ConfigFile(configPath, true);
                PluginConfig = new PluginConfig(_configFile);

                EnsureLanguagePacksDirectoryExists();
                
                _isInitialized = true;
                TranslationMod.Logger?.LogInfo($"[ConfigurationManager] Initialized. Language packs will be loaded on demand.");
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[ConfigurationManager] Failed to initialize: {e.Message}");
                // НЕ устанавливаем _isInitialized = true при ошибке, чтобы можно было повторить инициализацию
                throw; // re-throw исключение для правильной обработки в вызывающем коде
            }
        }
        
        private static void EnsureLanguagePacksDirectoryExists()
        {
            string languagePacksPath = Path.Combine(_pluginDirectory, PluginConfig.LanguagePacksPath.Value);
            
            if (!Directory.Exists(languagePacksPath))
            {
                Directory.CreateDirectory(languagePacksPath);
                TranslationMod.Logger?.LogInfo($"[ConfigurationManager] Created language packs directory: {languagePacksPath}");
            }
        }
        


        public static void Save()
        {
            if (!_isInitialized) return;
            _configFile.Save();
        }




    }
}