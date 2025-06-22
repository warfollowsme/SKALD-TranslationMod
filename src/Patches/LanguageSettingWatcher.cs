using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TranslationMod;

namespace TranslationMod.Patches
{
    [HarmonyPatch]
    public static class LanguageSettingWatcher
    {
        private static string _lastLanguage = "";
        private static bool _processing = false;

        /// <summary>
        /// Set target methods for patches
        /// </summary>
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            
            try
            {
                var carouselSettingType = AccessTools.Inner(typeof(GlobalSettings.SettingsCollection), GameConstants.CarouselSettingType);
                if (carouselSettingType == null)
                {
                    TranslationMod.Logger?.LogWarning("[LanguageSettingWatcher] Could not find CarouselSetting type - this is normal if game structure changed");
                    return methods;
                }

                // Find setStateTo method with different signatures
                var setStateToMethod = AccessTools.Method(carouselSettingType, GameConstants.SetStateToMethod, new[] { typeof(int) })
                                    ?? AccessTools.Method(carouselSettingType, "setState", new[] { typeof(int) })
                                    ?? AccessTools.Method(carouselSettingType, "SetState", new[] { typeof(int) });
                if (setStateToMethod != null)
                {
                    methods.Add(setStateToMethod);
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"[LanguageSettingWatcher] Found method for patching: {setStateToMethod.Name}");
#endif
                }

                // Find incrementState method
                var incrementStateMethod = AccessTools.Method(carouselSettingType, GameConstants.IncrementStateMethod, new[] { typeof(int) })
                                        ?? AccessTools.Method(carouselSettingType, "increment", new[] { typeof(int) })
                                        ?? AccessTools.Method(carouselSettingType, "Increment", new[] { typeof(int) });
                if (incrementStateMethod != null)
                {
                    methods.Add(incrementStateMethod);
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"[LanguageSettingWatcher] Found method for patching: {incrementStateMethod.Name}");
#endif
                }

                // Find applySettingSaveData method
                var applySettingSaveDataMethod = AccessTools.Method(carouselSettingType, GameConstants.ApplySettingSaveDataMethod)
                                            ?? AccessTools.Method(carouselSettingType, "applySaveData")
                                            ?? AccessTools.Method(carouselSettingType, "ApplySaveData");
                if (applySettingSaveDataMethod != null)
                {
                    methods.Add(applySettingSaveDataMethod);
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"[LanguageSettingWatcher] Found method for patching: {applySettingSaveDataMethod.Name}");
#endif
                }

                if (methods.Count == 0)
                {
#if DEBUG
                    TranslationMod.Logger?.LogInfo("[LanguageSettingWatcher] No suitable methods found to patch - language detection may not work automatically");
#endif
                }
                else
                {
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"[LanguageSettingWatcher] Successfully found {methods.Count} methods to patch");
#endif
                }
            }
            catch (System.Exception e)
            {
                TranslationMod.Logger?.LogError($"[LanguageSettingWatcher] Error finding target methods: {e.Message}");
            }
            
            return methods;
        }

        [HarmonyPostfix]
        public static void AfterCarouselCall(object __instance)
        {
            if (_processing) return;
            try
            {
                if (!IsLanguageSetting(__instance)) return;
                _processing = true;
                string name = GetSelectedLanguage(__instance);
                if (!string.IsNullOrEmpty(name) && name != _lastLanguage)
                {
                    LanguageManager.SwitchLanguage(name);
                    _lastLanguage = name;
#if DEBUG
                    TranslationMod.Logger?.LogInfo($"[LanguageSettingWatcher] switched to {name}");
#endif
                }
            }
            finally { _processing = false; }
        }

        private static bool IsLanguageSetting(object inst)
        {
            var type = inst.GetType();
            if (type.Name != "CarouselSetting") return false;
            var altField = AccessTools.Field(type, GameConstants.CarouselAlternativesField);
            if (altField?.GetValue(inst) is System.Collections.IList list && list.Count >= 2)
            {
                foreach (var o in list) if (o?.ToString() == GameConstants.EnglishLanguageName) return true;
            }
            return false;
        }

        private static string GetSelectedLanguage(object inst)
        {
            var type = inst.GetType();
            var stateField = AccessTools.Field(type, GameConstants.CarouselStateField);
            var altField = AccessTools.Field(type, GameConstants.CarouselAlternativesField);
            if (stateField?.GetValue(inst) is int index && altField?.GetValue(inst) is System.Collections.IList list)
            {
                if (index >= 0 && index < list.Count) return list[index]?.ToString();
            }
            return null;
        }
    }
} 