using BepInEx.Configuration;

namespace TranslationMod.Configuration
{
    public class PluginConfig
    {
        private readonly ConfigFile _configFile;
        
        // Path to language packs folder
        public ConfigEntry<string> LanguagePacksPath { get; private set; }

        public PluginConfig(ConfigFile configFile)
        {
            _configFile = configFile;

            // Path to language packs
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