using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;
using TranslationMod.Configuration;
using TranslationMod.Patches;
using System;
using static GlobalSettings;

namespace TranslationMod
{
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class TranslationMod : BaseUnityPlugin
	{
		internal static new ManualLogSource Logger;

		private void Awake()
		{
			Logger = base.Logger;
			
			try
			{
				ConfigurationManager.Initialize();
				Logger.LogInfo("Configuration system initialized successfully.");

				LanguageManager.Initialize();
				Logger.LogInfo("Language manager initialized.");
				
				HarmonyManager.ApplyPatches(new Harmony(MyPluginInfo.PLUGIN_GUID));
				Logger.LogInfo("Harmony patches applied successfully.");

				// Synchronize LanguageManager with current game settings
				LanguageManager.SynchronizeWithGame();
				Logger.LogInfo("Language manager synchronized with game settings.");

			}
			catch (System.Exception e)
			{
				Logger.LogError($"Failed to initialize plugin: {e}");
			}
			
			Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
		}
	}
}

