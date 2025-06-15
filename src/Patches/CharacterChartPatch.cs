using HarmonyLib;
using System.Collections.Generic;
using TranslationMod;
using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    [HarmonyPatch(typeof(StringPrinter), "getCharacterChart")]
    public static class CharacterChartPatch
    {
        private static string _lastAppliedLanguageCode = "";
        private static readonly Dictionary<char, int> _addedCharacters = new();
        private static readonly Dictionary<char, int> _originalCharacters = new();
        private static bool _originalCharactersSaved = false;

        public static void Postfix(ref Dictionary<char, int> __result)
        {
            if (!SaveOriginalChart(__result)) return;

            var currentPack = LanguageManager.GetCurrentLanguagePack();
            string langCode = currentPack?.LanguageCode ?? ConfigKeys.EnglishLanguageCode;
            if (langCode == _lastAppliedLanguageCode) return;

            RemovePreviousCharacters(__result);
            if (currentPack == null || langCode == ConfigKeys.EnglishLanguageCode)
            {
                _lastAppliedLanguageCode = ConfigKeys.EnglishLanguageCode;
                return;
            }
            ApplyPackChart(currentPack, __result);
            _lastAppliedLanguageCode = langCode;
        }

        private static bool SaveOriginalChart(Dictionary<char, int> chart)
        {
            if (_originalCharactersSaved) return true;
            foreach (var kv in chart) _originalCharacters[kv.Key] = kv.Value;
            _originalCharactersSaved = true;
            return true;
        }

        private static void RemovePreviousCharacters(Dictionary<char, int> chart)
        {
            foreach (var ch in _addedCharacters.Keys)
                chart.Remove(ch);
            _addedCharacters.Clear();
        }

        private static void ApplyPackChart(LanguagePack pack, Dictionary<char, int> chart)
        {
            var newChart = pack.GetCharacterChart();
            if (newChart == null) return;
            foreach (var kv in newChart)
            {
                chart[kv.Key] = kv.Value;
                _addedCharacters[kv.Key] = kv.Value;
            }
        }
    }
} 