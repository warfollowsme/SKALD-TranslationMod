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
        /// Набор уже залогированных сообщений для дедупликации
        /// </summary>
        private static readonly HashSet<string> _loggedMessages = new HashSet<string>();
        
        /// <summary>
        /// Объект для синхронизации доступа к _loggedMessages
        /// </summary>
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// Lazy-инициализация сервиса перевода
        /// </summary>
        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());
        /// <summary>
        /// Определяем целевой метод для патча - конструктор класса Bark
        /// </summary>
        /// <returns>MethodInfo конструктора Bark</returns>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase TargetMethod()
        {
            // Получаем тип BarkControl
            var barkControlType = AccessTools.TypeByName("BarkControl");
            if (barkControlType == null)
            {
                TranslationMod.Logger?.LogError("[BarkPatch] Cannot find BarkControl type");
                return null;
            }

            // Ищем вложенный protected класс Bark
            var barkType = barkControlType.GetNestedType("Bark", System.Reflection.BindingFlags.NonPublic);
            if (barkType == null)
            {
                TranslationMod.Logger?.LogError("[BarkPatch] Cannot find nested Bark type in BarkControl");
                return null;
            }

            // Получаем конструктор с нужной сигнатурой
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

            TranslationMod.Logger?.LogInfo("[BarkPatch] Successfully found Bark constructor for patching");
            return constructor;
        }

        /// <summary>
        /// Prefix патч - выводит информационное сообщение в логи плагина
        /// </summary>
        /// <param name="__0">message parameter</param>
        /// <param name="__1">x parameter</param>
        /// <param name="__2">y parameter</param>
        /// <param name="__3">textColor parameter</param>
        /// <param name="__4">shadowColor parameter</param>
        /// <param name="__5">delay parameter</param>
        /// <returns>true для продолжения выполнения оригинального конструктора</returns>
        [HarmonyPrefix]
        static bool Prefix(ref string __0, int __1, int __2, Color __3, Color __4, int __5)
        {
            try
            {
                // Сохраняем оригинальное сообщение для логирования
                string originalMessage = __0;
                
                // Проверяем текущий язык - если не English, переводим сообщение
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack != null && !currentLanguagePack.Name.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    // Переводим сообщение
                    string translatedMessage = _translator.Value.Process(__0);
                    __0 = translatedMessage; // Изменяем параметр для передачи переведенного текста в конструктор
                }
                
                // Проверяем, нужно ли логировать это сообщение (дедупликация)
                if (ShouldLogMessage(originalMessage))
                {
                    if (originalMessage != __0)
                    {
                        // Логируем с информацией о переводе
                        TranslationMod.Logger?.LogInfo($"[BarkPatch] Bark constructor called with message: '{originalMessage}' -> '{__0}' at position ({__1}, {__2}) with delay: {__5}");
                    }
                    else
                    {
                        // Логируем без перевода
                        TranslationMod.Logger?.LogInfo($"[BarkPatch] Bark constructor called with message: '{originalMessage}' at position ({__1}, {__2}) with delay: {__5}");
                    }
                }
                
                // Возвращаем true, чтобы позволить выполниться оригинальному конструктору
                return true;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[BarkPatch] Error in Bark constructor prefix: {ex.Message}");
                // В случае ошибки всё равно позволяем выполниться оригинальному конструктору
                return true;
            }
        }

        /// <summary>
        /// Проверяет, нужно ли логировать сообщение (дедупликация)
        /// </summary>
        /// <param name="message">Сообщение для проверки</param>
        /// <returns>true если сообщение нужно залогировать, false если оно уже было залогировано</returns>
        private static bool ShouldLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            lock (_lockObject)
            {
                // Если сообщение уже было залогировано, не логируем его снова
                if (_loggedMessages.Contains(message))
                {
                    return false;
                }

                // Добавляем сообщение в набор залогированных
                _loggedMessages.Add(message);
                return true;
            }
        }
    }
} 