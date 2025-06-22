using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Harmony patch intercepting BarkControl.Bark constructor
    /// and logging the message parameter, with translation support.
    /// </summary>
    [HarmonyPatch]
    public static class BarkPatch
    {
        /// <summary>
        /// Set of already logged messages for deduplication
        /// </summary>
        private static readonly HashSet<string> _loggedMessages = new HashSet<string>();
        
        /// <summary>
        /// Object for synchronizing access to _loggedMessages
        /// </summary>
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// Lazy initialization of translation service
        /// </summary>
        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());
        /// <summary>
        /// Define target method for patch - Bark class constructor
        /// </summary>
        /// <returns>MethodInfo of Bark constructor</returns>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase TargetMethod()
        {
            // Get BarkControl type
            var barkControlType = AccessTools.TypeByName("BarkControl");
            if (barkControlType == null)
            {
                TranslationMod.Logger?.LogError("[BarkPatch] Cannot find BarkControl type");
                return null;
            }

            // Find nested protected Bark class
            var barkType = barkControlType.GetNestedType("Bark", System.Reflection.BindingFlags.NonPublic);
            if (barkType == null)
            {
                TranslationMod.Logger?.LogError("[BarkPatch] Cannot find nested Bark type in BarkControl");
                return null;
            }

            // Get constructor with required signature
            var constructor = AccessTools.Constructor(barkType, new Type[] {
                typeof(string),  // message
                typeof(int),     // x
                typeof(int),     // y
                typeof(Color),   // textColor
                typeof(Color),   // shadowColor
                typeof(int)      // delay
            });

            if (constructor == null)
            {
                TranslationMod.Logger?.LogError("[BarkPatch] Cannot find Bark constructor with expected signature");
                return null;
            }

#if DEBUG
            TranslationMod.Logger?.LogInfo("[BarkPatch] Successfully found Bark constructor for patching");
#endif
            return constructor;
        }

        /// <summary>
        /// Prefix patch - outputs informational message to plugin logs
        /// </summary>
        /// <param name="__0">message parameter</param>
        /// <param name="__1">x parameter</param>
        /// <param name="__2">y parameter</param>
        /// <param name="__3">textColor parameter</param>
        /// <param name="__4">shadowColor parameter</param>
        /// <param name="__5">delay parameter</param>
        /// <returns>true to continue execution of original constructor</returns>
        [HarmonyPrefix]
        static bool Prefix(ref string __0, int __1, int __2, Color __3, Color __4, int __5)
        {
            try
            {
                // Save original message for logging
                string originalMessage = __0;
                
                // Check current language - if not English, translate message
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack != null && !currentLanguagePack.Name.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    // Translate message
                    string translatedMessage = _translator.Value.Process(__0);
                    __0 = translatedMessage; // Change parameter to pass translated text to constructor
                }
                
                // Check if we need to log this message (deduplication)
                if (ShouldLogMessage(originalMessage))
                {
#if DEBUG
                    if (originalMessage != __0)
                    {
                        // Log with translation information
                        TranslationMod.Logger?.LogInfo($"[BarkPatch] Bark constructor called with message: '{originalMessage}' -> '{__0}' at position ({__1}, {__2}) with delay: {__5}");
                    }
                    else
                    {
                        // Log without translation
                        TranslationMod.Logger?.LogInfo($"[BarkPatch] Bark constructor called with message: '{originalMessage}' at position ({__1}, {__2}) with delay: {__5}");
                    }
#endif
                }
                
                // Return true to allow original constructor to execute
                return true;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[BarkPatch] Error in Bark constructor prefix: {ex.Message}");
                // In case of error, still allow original constructor to execute
                return true;
            }
        }

        /// <summary>
        /// Checks if message should be logged (deduplication)
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <returns>true if message should be logged, false if it was already logged</returns>
        private static bool ShouldLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            lock (_lockObject)
            {
                // If message was already logged, don't log it again
                if (_loggedMessages.Contains(message))
                {
                    return false;
                }

                // Add message to set of logged messages
                _loggedMessages.Add(message);
                return true;
            }
        }
    }
} 