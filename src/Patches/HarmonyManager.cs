using HarmonyLib;
using System.Reflection;
using BepInEx.Logging;

namespace TranslationMod.Patches
{
    public static class HarmonyManager
    {
        public static void ApplyPatches(Harmony harmony)
        {
            var logger = TranslationMod.Logger;
            
            if (logger == null)
            {
                throw new System.InvalidOperationException("TranslationMod.Logger is not initialized");
            }
            
            logger.LogInfo("Applying harmony patches.");

            try
            {
                // Применяем все патчи из текущей сборки
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                // Конвертируем в список без использования LINQ для совместимости
                var patchedMethodsEnumerable = harmony.GetPatchedMethods();
                var patchedMethods = new System.Collections.Generic.List<System.Reflection.MethodBase>();
                foreach (var method in patchedMethodsEnumerable)
                {
                    patchedMethods.Add(method);
                }
                
                logger.LogInfo($"Successfully patched {patchedMethods.Count} methods:");
                foreach (var method in patchedMethods)
                {
                    logger.LogInfo($"- {method.FullDescription()}");
                }
            }
            catch (System.Exception e)
            {
                logger.LogError($"Failed to apply Harmony patches: {e.Message}");
                throw;
            }
        }
    }
} 