using System.Collections.Generic;

namespace TranslationMod
{
    /// <summary>
    /// Constants related to internal game mechanics and objects
    /// </summary>
    public static class GameConstants
    {
        // Settings IDs
        public const string LanguageSettingId = "language_setting_id";

        // Setting UI Text
        public const string LanguageSettingName = "Language";
        public const string LanguageSettingDescription = "Select the display language for the game.";

        // Reflection - Types
        public const string CarouselSettingType = "CarouselSetting";

        // Reflection - Fields
        public const string CarouselStateField = "state";
        public const string CarouselAlternativesField = "alternatives";
        public const string SettingIdField = "id";

        // Reflection - Methods
        public const string CarouselSetStateMethod = "setStateTo";
        public const string CarouselIncrementStateMethod = "incrementState";
        public const string CarouselApplySettingSaveDataMethod = "applySettingSaveData";
        public const string SetStateToMethod = "setStateTo";
        public const string IncrementStateMethod = "incrementState";
        public const string ApplySettingSaveDataMethod = "applySettingSaveData";
        
        // Misc
        public const string EnglishLanguageName = "English";
        public const string RussianLanguageName = "Russian";

        public const string MainConfigFileName = "translation.cfg";
        public const string LanguagePackConfigFileName = "language_pack.json";
    }

    /// <summary>
    /// Font-related constants
    /// </summary>
    public static class FontConstants
    {
        public static readonly HashSet<string> TargetFontFiles = new HashSet<string>
        {
            "Logo",
            "TinyFont",
            "TinyFontYellow",
            "TinyFontTall",
            "TinyFontTall - Copy",
            "TinyFontTallCapitalized",
            "TinyFontTallCapitalized - Copy",
            "TinyFontFat",
            "TinyFontCapitalizedYellow",
            "TinyFontCapitalized",
            "MediumFont",
            "MediumFontBlue",
            "InsularHuge",
            "InsularMedium",
            "InsularTiny",
            "MedievalHuge",
            "MedievalHugeThin",
            "MedievalMedium",
            "IlluminatedFont",
            "IlluminatedFontLarge",
            "BigFont"
        };
    }

    /// <summary>
    /// Constants for configuration file keys and sections
    /// </summary>
    public static class ConfigKeys
    {
        // Sections
        public const string GeneralSection = "General";
        public const string PathsSection = "Paths";
        public const string CharacterChartSection = "CharacterChart";

        // Language Pack Config Keys
        public const string LanguageCodeKey = "LanguageCode";
        public const string LanguageNameKey = "Name";
        public const string DescriptionKey = "Description";
        public const string VersionKey = "Version";
        public const string FontFilesPathKey = "FontFilesPath";
        public const string TranslationFilesPathKey = "TranslationFilesPath";

        // Language Codes
        public const string EnglishLanguageCode = "en";
        public const string RussianLanguageCode = "ru";

        // Language Names
        public const string EnglishLanguageName = "English";
        public const string RussianLanguageName = "Russian";
        
        // Default Directories
        public const string DefaultFontsDir = "fonts";
        public const string DefaultTranslationsDir = "translations";
        
        // Paths
        public const string RussianLanguagePackPath = "ru";

        // File Names
        public const string MainConfigFileName = "TranslationMod.cfg";
        public const string LanguagePackConfigFileName = "language_pack.json";
    }
} 