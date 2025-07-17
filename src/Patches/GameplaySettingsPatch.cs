using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TranslationMod;

namespace TranslationMod.Patches
{
    [HarmonyPatch]
    public static class GameplaySettingsPatch
    {
        internal static bool _isLanguageSettingCreated = false;

        [HarmonyPatch(typeof(GlobalSettings.GamePlaySettings), "initialize")]
        [HarmonyPostfix]
        public static void AddLanguageSetting(GlobalSettings.GamePlaySettings __instance)
        {
            if (__instance.getObject(GameConstants.LanguageSettingId) != null)
            {
                _isLanguageSettingCreated = true;
                return;
            }
            try
            {
                var availableLanguages = LanguageManager.GetAvailableLanguageNames();
                TranslationMod.Logger?.LogInfo($"[GameplaySettingsPatch] Available languages: {string.Join(", ", availableLanguages)}");
                var allLanguages = new List<string> { GameConstants.EnglishLanguageName };
                allLanguages.AddRange(availableLanguages);

                var carouselType = AccessTools.Inner(typeof(GlobalSettings.SettingsCollection), GameConstants.CarouselSettingType);
                var ctor = AccessTools.Constructor(carouselType, new[] { typeof(string), typeof(string), typeof(string), typeof(List<string>), typeof(int) });
                int startingIndex = 0;
                var currentCode = LanguageManager.GetCurrentLanguageCode();
                if (currentCode != ConfigKeys.EnglishLanguageCode)
                {
                    string currentName = null;
                    foreach (var name in availableLanguages)
                    {
                        if (LanguageManager.GetLanguageCodeByName(name) == currentCode) { currentName = name; break; }
                    }
                    if (currentName != null) startingIndex = allLanguages.IndexOf(currentName);
                }
                object[] args = { GameConstants.LanguageSettingId, GameConstants.LanguageSettingName, GameConstants.LanguageSettingDescription, allLanguages, startingIndex };
                var instance = ctor.Invoke(args) as GlobalSettings.SettingsCollection.Setting;
                __instance.add(instance);
                _isLanguageSettingCreated = true;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[GameplaySettingsPatch] {ex.Message}");
            }
        }
    }
} 