using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using BepInSerializer.Core;
using UnityEngine.SceneManagement;
using BepInSerializer.Utils;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;
using BepInSerializer.Patches.Serialization;
using System.Threading;

namespace BepInSerializer;

[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class BridgeManager : BaseUnityPlugin
{
	const string GUID = "pixelguy.pixelmodding.bepinex.serializer";

	// ===== CRITICAL CONFIGURATIONS =====
	internal static ConfigEntry<bool> allowNullableTypesWithoutSerializeReference;

	// ===== DEBUG & PERFORMANCE CONFIGURATIONS =====
	internal static ConfigEntry<bool> enableDebugLogs, enabledEstimatedTypeSize;
	internal static ConfigEntry<int> sizeForTypesReflectionCache, sizeForMemberAccessReflectionCache;

	// ===== STATIC INSTANCE & LOGGER =====
	internal static BridgeManager Instance { get; private set; }
	internal static ManualLogSource logger;

	void Awake()
	{
		// ----- INITIALIZATION -----
		InitializeInstance();
		ApplyHarmonyPatches();

		// ----- CONFIGURATION -----
		SetupDebugConfigurations();

		// ----- CACHE INITIALIZATION -----
		SetupCacheInitialization();

		// ----- THREAD SETUP -----
		InitializeMainThread();

#if DEBUG
		// ----- DEBUG SETUP -----
		SetupDebugMode();
#endif
	}

	private void InitializeInstance()
	{
		Instance = this;
		logger = Logger;
	}

	private void ApplyHarmonyPatches()
	{
		var h = new Harmony(GUID);
		h.PatchAll();
	}

	private void SetupDebugConfigurations()
	{
		enableDebugLogs = Config.Bind("Debugging", "Enable Debug Logs", false, "If True, the library will log all the registered types on initialization.");
		enabledEstimatedTypeSize = Config.Bind("Performance", "Enable Type-Size Estimation", false, "If True, the library will scan all types from the Plugins folder to estimate the max size of the cache for saving Types. This might make the loading time take longer.");
		sizeForTypesReflectionCache = Config.Bind("Performance", "Type Caching Limit", 600, "Determines the size of the cache for saving types. Any value below 100 will default to estimating cache size (Type-Size Estimation).");
		sizeForMemberAccessReflectionCache = Config.Bind("Performance", "Member Access Caching Limit", 450, "Determines the size of the cache for saving most member-access operations (FieldInfo.GetValue, FieldInfo.SetValue, MethodInfo.Invoke, Activator.Invoke, etc.). The value cannot be below 100.");
		sizeForMemberAccessReflectionCache.Value = Mathf.Max(100, sizeForMemberAccessReflectionCache.Value);
	}

	private void SetupCacheInitialization()
	{
		if (enabledEstimatedTypeSize.Value || sizeForTypesReflectionCache.Value < 100)
			SceneManager.sceneLoaded += OnSceneLoadedForCacheEstimation;
		else
			LRUCacheInitializer.InitializeCacheValues();
	}

	private void InitializeMainThread()
	{
		SerializationPatcher.mainThreadId = Thread.CurrentThread.ManagedThreadId;
		Logger.LogInfo($"Main Thread ({Thread.CurrentThread.ManagedThreadId}) identified.");
		Logger.LogInfo($"BepInSerializer.Mono has been initialized!");
	}

	private void OnSceneLoadedForCacheEstimation(Scene _, LoadSceneMode _2)
	{
		Logger.LogInfo("Calculating estimated size for LRUCache<Type, ...> collections...");
		SceneManager.sceneLoaded -= OnSceneLoadedForCacheEstimation;

		long typeSize = CalculateTypeSize();
		sizeForTypesReflectionCache.Value = (int)System.Math.Floor(MathUtils.CalculateCurve(typeSize, 238));
		Logger.LogInfo($"Based on {typeSize} types detected, the LRUCache collections' estimated size is {sizeForTypesReflectionCache.Value}.");

		LRUCacheInitializer.InitializeCacheValues();
	}

	private long CalculateTypeSize()
	{
		Assembly myAssembly = typeof(BridgeManager).Assembly;
		long typeSize = 0;

		foreach (var assembly in AccessTools.AllAssemblies())
		{
			if (assembly.IsGameAssembly() || assembly == myAssembly) continue;
			typeSize += AccessTools.GetTypesFromAssembly(assembly).Length;
		}

		return typeSize;
	}

#if DEBUG
	private void SetupDebugMode()
	{
		StartCoroutine(BridgeManagerDebugger.WaitForGameplayRoutine("MainMenu"));
	}
#endif
}

