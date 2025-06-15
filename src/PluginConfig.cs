using BepInEx.Configuration;

namespace TranslationMod.Configuration
{
    public class PluginConfig
    {
        private readonly ConfigFile _configFile;
        
        // Путь к папке с языковыми пакетами
        public ConfigEntry<string> LanguagePacksPath { get; private set; }

        public PluginConfig(ConfigFile configFile)
        {
            _configFile = configFile;

            // Путь к языковым пакетам
            LanguagePacksPath = _configFile.Bind(
                ConfigKeys.GeneralSection, 
                "LanguagePacksPath", 
                "LanguagePacks", 
                "Path to language packs directory (relative to plugin folder)"
            );
        }

        public bool IsValid() => LanguagePacksPath != null;

        public void Save()
        {
            _configFile?.Save();
        }
    }
} 