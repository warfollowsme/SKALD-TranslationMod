// Patches/UITextBlockSetContentPatch.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using TranslationMod.Configuration;

namespace TranslationMod.Patches
{
    /// <summary>
    /// Harmony patch intercepting UITextBlock.setContent(string)
    /// and inserting translated text.
    /// </summary>
    [HarmonyPatch(typeof(UITextBlock), nameof(UITextBlock.setContent), new[] { typeof(string) })]
    public static class UITextBlockSetContentPatch
    {
        /* ----------------------------------------------------------------- */
        /* 1.  Lazy-инициализация переводчика                               */
        /* ----------------------------------------------------------------- */

        private static readonly Lazy<TranslationService> _translator =
            new(() => new TranslationService());

        /* ----------------------------------------------------------------- */
        /* 1.1  Рефлексия для доступа к приватным полям и методам            */
        /* ----------------------------------------------------------------- */
        
        private static FieldInfo _illuminatedImageField;
        private static FieldInfo _fontField;
        private static FieldInfo _contentField;
        private static FieldInfo _tooltipField;
        
        private static MethodInfo _preProcessStringMethod;
        private static MethodInfo _identifyTooltipKeywordsMethod;
        private static MethodInfo _splitIntoParagraphMethod;
        private static MethodInfo _parseParagraphMethod;
        private static MethodInfo _pruneLengthMethod;
        
        /// <summary>Ленивая инициализация FieldInfo для illuminatedImage</summary>
        private static FieldInfo IlluminatedImageField
        {
            get
            {
                if (_illuminatedImageField == null)
                    _illuminatedImageField = typeof(UITextBlock).GetField("illuminatedImage", BindingFlags.NonPublic | BindingFlags.Instance);
                return _illuminatedImageField;
            }
        }
        
        /// <summary>Ленивая инициализация FieldInfo для font</summary>
        private static FieldInfo FontField
        {
            get
            {
                if (_fontField == null)
                    _fontField = typeof(UITextBlock).GetField("font", BindingFlags.NonPublic | BindingFlags.Instance);
                return _fontField;
            }
        }
        
        /// <summary>Ленивая инициализация FieldInfo для content</summary>
        private static FieldInfo ContentField
        {
            get
            {
                if (_contentField == null)
                    _contentField = typeof(UITextBlock).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance);
                return _contentField;
            }
        }

        /// <summary>Ленивая инициализация FieldInfo для content</summary>
        private static FieldInfo ToolTipField
        {
            get
            {
                if (_tooltipField == null)
                    _tooltipField = typeof(UITextBlock).GetField("toolTips", BindingFlags.NonPublic | BindingFlags.Instance);
                return _tooltipField;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для preProcessString</summary>
        private static MethodInfo PreProcessStringMethod
        {
            get
            {
                if (_preProcessStringMethod == null)
                    _preProcessStringMethod = typeof(UITextBlock).GetMethod("preProcessString", BindingFlags.NonPublic | BindingFlags.Instance);
                return _preProcessStringMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для identifyTooltipKeywords</summary>
        private static MethodInfo IdentifyTooltipKeywordsMethod
        {
            get
            {
                if (_identifyTooltipKeywordsMethod == null)
                    _identifyTooltipKeywordsMethod = typeof(UITextBlock).GetMethod("identifyTooltipKeywords", BindingFlags.NonPublic | BindingFlags.Instance);
                return _identifyTooltipKeywordsMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для splitIntoParagraph</summary>
        private static MethodInfo SplitIntoParagraphMethod
        {
            get
            {
                if (_splitIntoParagraphMethod == null)
                    _splitIntoParagraphMethod = typeof(UITextBlock).GetMethod("splitIntoParagraph", BindingFlags.NonPublic | BindingFlags.Instance);
                return _splitIntoParagraphMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для parseParagraph</summary>
        private static MethodInfo ParseParagraphMethod
        {
            get
            {
                if (_parseParagraphMethod == null)
                    _parseParagraphMethod = typeof(UITextBlock).GetMethod("parseParagraph", BindingFlags.NonPublic | BindingFlags.Instance);
                return _parseParagraphMethod;
            }
        }
        
        /// <summary>Ленивая инициализация MethodInfo для pruneLength</summary>
        private static MethodInfo PruneLengthMethod
        {
            get
            {
                if (_pruneLengthMethod == null)
                    _pruneLengthMethod = typeof(UITextBlock).GetMethod("pruneLength", BindingFlags.NonPublic | BindingFlags.Instance);
                return _pruneLengthMethod;
            }
        }

        /* ----------------------------------------------------------------- */
        /* 2.  Prefix: меняем аргумент метода и полная реимплементация       */
        /* ----------------------------------------------------------------- */

        private static bool Prefix(UITextBlock __instance, string __0)
        {
            try
            {
                // if(ToolTipField != null)
                // {
                //     var tooltip = ToolTipField.GetValue(__instance) as ToolTipControl.ToolTipCategory;
                //     if(tooltip != null)
                //     {
                //         TranslationMod.Logger?.LogInfo($"Tooltip:");
                //         var keywords = tooltip.getKeywords();
                //         foreach(var keyword in keywords)
                //         {
                //             TranslationMod.Logger?.LogInfo($"  - {keyword}");
                //         }
                //     }
                // }
                // Проверяем текущий язык - если English, используем оригинальный метод
                var currentLanguagePack = LanguageManager.GetCurrentLanguagePack();
                if (currentLanguagePack == null || currentLanguagePack.Name.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Выполняем оригинальный метод
                }
                
                // Сначала переводим текст
                string translatedText = _translator.Value.Process(__0);
                
                // Затем полная реимплементация setContent с переведенным текстом
                SetContentComplete(__instance, __0, translatedText);
                
                // Возвращаем false, чтобы пропустить оригинальный метод
                return false;
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"Error in custom setContent implementation: {ex.Message}");
                // В случае ошибки позволяем выполниться оригинальному методу
                return true;
            }
        }
        
        /// <summary>Полная реимплементация UITextBlock.setContent</summary>
        private static void SetContentComplete(UITextBlock instance, string input, string translated)
        {
            try
            {                
                if (instance.isContentEqual(input) || input == null || input.Length == 0)
                {
                    return;
                }
                
                if (PreProcessStringMethod == null)
                {
                    TranslationMod.Logger?.LogError("PreProcessStringMethod is null");
                    return;
                }
                
                instance.clearElements();
                
                if (ContentField == null)
                {
                    TranslationMod.Logger?.LogError("ContentField is null");
                    return;
                }
                
                ContentField.SetValue(instance, string.Copy(input));
                
                if (FontField == null)
                {
                    TranslationMod.Logger?.LogError("FontField is null");
                    return;
                }
                
                var font = FontField.GetValue(instance) as Font;
                if (font == null)
                {
                    TranslationMod.Logger?.LogError("No font set for text box:" + input);
                    MainControl.logError("No font set for text box:" + input);
                    return;
                }
                
                if (instance.illuminatedFont != null)
                {
                    int subimageForChar = StringPrinter.getSubimageForChar(translated[0]);
                    
                    // Расширенная проверка для кириллицы (ваш комментарий: переделать на проверку взятых кодов из кодировки)
                    if (subimageForChar <= 25 || (subimageForChar >= 90 && subimageForChar <= 122))
                    {                        
                        if (IlluminatedImageField == null)
                        {
                            TranslationMod.Logger?.LogError("IlluminatedImageField is null");
                            return;
                        }
                        
                        var illuminatedImage = new UICanvasVertical();
                        translated = translated.Substring(1);
                        illuminatedImage.backgroundTexture = TextureTools.getLetterSubImageTextureData(
                            subimageForChar, instance.illuminatedFont.getModelPath());
                        illuminatedImage.padding.right = font.wordSpacing;
                        IlluminatedImageField.SetValue(instance, illuminatedImage);
                        instance.add(illuminatedImage);
                    }
                    else
                    {
                        TranslationMod.Logger?.LogInfo($"Character '{translated[0]}' with subimage {subimageForChar} doesn't match illuminated criteria");
                    }
                }
                translated = (string)PreProcessStringMethod.Invoke(instance, new object[] { translated });
                
                if (IdentifyTooltipKeywordsMethod == null)
                {
                    TranslationMod.Logger?.LogError("IdentifyTooltipKeywordsMethod is null");
                    return;
                }
                
                translated = (string)IdentifyTooltipKeywordsMethod.Invoke(instance, new object[] { translated });
                
                if (SplitIntoParagraphMethod == null)
                {
                    TranslationMod.Logger?.LogError("SplitIntoParagraphMethod is null");
                    return;
                }
                var paragraphs = (List<string>)SplitIntoParagraphMethod.Invoke(instance, new object[] { translated });
                if (paragraphs != null)
                {
                    if (ParseParagraphMethod == null)
                    {
                        TranslationMod.Logger?.LogError("ParseParagraphMethod is null");
                        return;
                    }
                    
                    foreach (string paragraph in paragraphs)
                    {
                        ParseParagraphMethod.Invoke(instance, new object[] { paragraph });
                    }
                }
                
                if (PruneLengthMethod == null)
                {
                    TranslationMod.Logger?.LogError("PruneLengthMethod is null");
                    return;
                }
                
                PruneLengthMethod.Invoke(instance, null);
                
                instance.alignElements();                
            }
            catch (Exception ex)
            {
                TranslationMod.Logger?.LogError($"Exception in SetContentComplete: {ex.Message}");
                TranslationMod.Logger?.LogError($"Stack trace: {ex.StackTrace}");
                throw; // Перебрасываем исключение для обработки в Prefix
            }
        }




    }
}