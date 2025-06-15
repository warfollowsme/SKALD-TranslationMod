using HarmonyLib;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    [HarmonyPatch]
    public static class FontAssetPatch
    {
        private static bool _isSubscribed = false;
        // Было ли выполнено предварительное чтение шрифтов
        private static bool _fontsPreloaded = false;

        public static void Initialize()
        {
            if (_isSubscribed) return;
            
            LanguageManager.OnLanguageChanged += OnLanguageChangedHandler;
            _isSubscribed = true;
            TranslationMod.Logger?.LogInfo("[FontAssetPatch] Initialized and subscribed to language change events");            
            PreloadFonts();
        }

        private static void OnLanguageChangedHandler()
        {
            ClearReplacedFonts();
            TranslationMod.Logger?.LogInfo("[FontAssetPatch] Language changed, cleared replaced fonts list");
        }
        
        public static void ClearReplacedFonts()
        {
            _fontsPreloaded = false;
            PreloadFonts();
            TranslationMod.Logger?.LogInfo("[FontAssetPatch] Cleared fonts cache and reloaded fonts");
        }
        
        private static TextureTools.TextureData LoadPngAsTextureData(string filePath)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                
                // Создаем текстуру точно так же, как это делает игра
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point; // Пиксельная графика - отключаем сглаживание
                texture.wrapMode = TextureWrapMode.Clamp;
                
                if (texture.LoadImage(fileData))
                {
                    // Применяем изменения
                    texture.Apply(false, false);
                    
                    // Создаем TextureData ТОЧНО ТАК ЖЕ, как это делает оригинальная игра
                    // Используем оригинальный конструктор TextureData(Texture2D texture)
                    var textureData = new TextureTools.TextureData(texture);
                    
                    // Освобождаем временную текстуру
                    UnityEngine.Object.Destroy(texture);
                    return textureData;
                }
                else
                {
                    TranslationMod.Logger?.LogError($"[FontAssetPatch] Failed to load image data from '{filePath}'. File might be corrupted or not a valid PNG.");
                    UnityEngine.Object.Destroy(texture); // Освобождаем ресурсы
                }
            }
            catch (IOException e)
            {
                TranslationMod.Logger?.LogError($"[FontAssetPatch] IO Error loading texture '{filePath}': {e.Message}");
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[FontAssetPatch] Unexpected error loading texture '{filePath}': {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// Adds custom texture to game buffer via reflection
        /// </summary>
        /// <param name="path">Path to original file</param>
        /// <param name="textureData">Custom texture data</param>
        /// <returns>true if successfully added to buffer</returns>
        private static bool AddTextureToGameBuffer(string path, TextureTools.TextureData textureData)
        {
            try
            {
                // Получаем поле fullImageBuffer через рефлексию
                var bufferField = AccessTools.Field(typeof(TextureTools), "fullImageBuffer");
                if (bufferField == null)
                {
                    TranslationMod.Logger?.LogError("[FontAssetPatch] Could not find fullImageBuffer field in TextureTools");
                    return false;
                }

                var bufferInstance = bufferField.GetValue(null);
                if (bufferInstance == null)
                {
                    TranslationMod.Logger?.LogError("[FontAssetPatch] fullImageBuffer instance is null");
                    return false;
                }

                // Получаем метод addTexture через рефлексию
                var addTextureMethod = AccessTools.Method(bufferInstance.GetType(), "addTexture");
                if (addTextureMethod == null)
                {
                    TranslationMod.Logger?.LogError($"[FontAssetPatch] Could not find addTexture method in {bufferInstance.GetType().Name}");
                    return false;
                }

                // Вызываем addTexture с нашими параметрами
                addTextureMethod.Invoke(bufferInstance, new object[] { path, textureData });
                
                TranslationMod.Logger?.LogDebug($"[FontAssetPatch] Successfully added custom texture to game buffer for path: {path}");
                return true;
            }
            catch (Exception e)
            {
                TranslationMod.Logger?.LogError($"[FontAssetPatch] Error adding texture to game buffer: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force loading of all target fonts into buffer during initialization.
        /// </summary>
        private static void PreloadFonts()
        {
            if (_fontsPreloaded) return;

            // Очищаем все внутренние буферы до инъекции кастомных шрифтов,
            // чтобы гарантировать, что игра будет формировать суб-спрайты из новых текстур
            TextureTools.clearBuffer();
            try
            {
                var languagePack = LanguageManager.GetCurrentLanguagePack();
                if (languagePack == null)
                {
                    TranslationMod.Logger?.LogWarning("[FontAssetPatch] Language pack is null, cannot preload fonts");
                    return;
                }

                string fontsPath = languagePack.GetFontsPath();
                if (string.IsNullOrEmpty(fontsPath) || !System.IO.Directory.Exists(fontsPath))
                {
                    TranslationMod.Logger?.LogWarning($"[FontAssetPatch] Fonts directory not found: {fontsPath}");
                    return;
                }

                foreach (var fontFile in FontConstants.TargetFontFiles)
                {
                    string customFontPathPng = System.IO.Path.Combine(fontsPath, fontFile + ".png");

                    if (!System.IO.File.Exists(customFontPathPng))
                    {
                        continue; // в пакете может не быть всех файлов
                    }

                    var textureData = LoadPngAsTextureData(customFontPathPng);
                    if (textureData == null)
                    {
                        continue; // ошибка загрузки
                    }

                    // Путь-ключ в буфере должен совпадать с тем, что запрашивает игра:
                    // Images/CustomFonts/<fontFile> (без .png)
                    string bufferKey = $"Images/CustomFonts/{fontFile}";
                    if (fontFile == "Logo")
                    {
                        bufferKey = $"Images/Backgrounds/Logo";
                    }

                    if (AddTextureToGameBuffer(bufferKey, textureData))
                    {
                        TranslationMod.Logger?.LogInfo($"[FontAssetPatch] Preloaded and injected font '{fontFile}'");
                    }
                }

                _fontsPreloaded = true;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"[FontAssetPatch] Error while preloading fonts: {ex.Message}");
            }
        }
    }
} 