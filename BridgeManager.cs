using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInSerializer.Core;
using BepInSerializer.Patches.Serialization;
using BepInSerializer.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInSerializer;

[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class BridgeManager : BaseUnityPlugin
{
	const string GUID = "pixelguy.pixelmodding.bepinex.serializer";

	// ===== DEBUG & PERFORMANCE CONFIGURATIONS =====
	internal static ConfigEntry<bool> enableDebugLogs, enabledEstimatedTypeSize;
	internal static ConfigEntry<int> sizeForTypesReflectionCache, sizeForMemberAccessReflectionCache;

	// ===== STATIC INSTANCE & LOGGER =====
	internal static BridgeManager Instance { get; private set; }
	internal static ManualLogSource logger;

	void Awake()
	{
		// ----- VERIFICATION -----
		if (CheckIfEnvironmentAlreadyHasSerializerAvailable()) // If this is True, cancel the whole plugin
		{
			Logger.LogWarning("Serialization Test has passed!");
			Logger.LogError($"Shutting down {PluginInfo.PLUGIN_NAME} since the environment may not need it.");
			return;
		}

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

	private bool CheckIfEnvironmentAlreadyHasSerializerAvailable()
	{
		const string expectedString = "TestValueThatShouldBeSerializedLikeSuch!#4";
		const int expectedNumber = 81958;

		// Make a new subject
		var testSubject = new GameObject("TestSerialization").AddComponent<BridgeManagerTestBehaviour>();
		testSubject.myTestClass = new() { str = expectedString, value = expectedNumber };

		// Test if the new subject is still serialized
		var newSubject = Instantiate(testSubject);
		var clonedTestClass = newSubject.myTestClass;
		bool isSubjectSerialized = clonedTestClass != null && clonedTestClass.str == expectedString && clonedTestClass.value == expectedNumber;

		// Destroy the subjects to release some memory
		Destroy(testSubject.gameObject);
		Destroy(newSubject.gameObject);

		return isSubjectSerialized;
	}

	private void InitializeInstance()
	{
		Instance = this;
		logger = Logger;
	}

	private void ApplyHarmonyPatches()
	{
		var harmony = new Harmony(GUID);
		harmony.PatchAll();
	}

	private void SetupDebugConfigurations()
	{
		enableDebugLogs = Config.Bind("Debugging", "Enable Debug Logs", false, "If True, the library will log all the registered types on initialization (ONLY USE UPON REQUESTED, this will REALLY flood up the logs with garbage).");
		enabledEstimatedTypeSize = Config.Bind("Performance", "Enable Type-Size Estimation", false, "If True, the library will scan all types from the Plugins folder to estimate the max size of the cache for saving Types.\nThis might slow down loading time for a tiny bit.");
		sizeForTypesReflectionCache = Config.Bind("Performance", "Type Caching Limit", 600, "Determines the size of the cache for saving types.\nAny value below 100 will default to estimating cache size (Type-Size Estimation).");
		sizeForMemberAccessReflectionCache = Config.Bind("Performance", "Member Access Caching Limit", 450, "Determines the size of the cache for saving most member-access operations (FieldInfo.GetValue, FieldInfo.SetValue, MethodInfo.Invoke, Activator.Invoke, etc.).\nThe value cannot be below 100.");
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

		uint typeSize = CalculateTypeSize();
		sizeForTypesReflectionCache.Value = (int)Math.Max(100, Math.Floor(MathUtils.CalculateCurve(typeSize, 238)));
		Logger.LogInfo($"Based on {typeSize} types detected, the LRUCache collections' estimated size is {sizeForTypesReflectionCache.Value}.");

		LRUCacheInitializer.InitializeCacheValues();
	}

	private uint CalculateTypeSize()
	{
		Assembly myAssembly = typeof(BridgeManager).Assembly;
		uint typeSize = 0;

		foreach (var assembly in AccessTools.AllAssemblies())
		{
			if (assembly.IsGameAssembly() || assembly == myAssembly) continue;
			var types = AccessTools.GetTypesFromAssembly(assembly);
			typeSize += (uint)types.Length;
		}

		return typeSize;
	}

#if DEBUG
	private void SetupDebugMode()
	{
		StartCoroutine(BridgeManagerDebugger.WaitForGameplayRoutine("MainMenu"));
		StartCoroutine(BridgeManagerDebugger.WaitForGameplayRoutine("Level"));
	}
#endif
}

