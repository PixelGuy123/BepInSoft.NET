#if DEBUG
static class ChangeTestComponentStructure
{
    // Invasive patch test. The serializer should stop this patch from being called.
    private static void Prefix(SerializationBridgeTester __instance)
    {
        if (!__instance.initializeOnAwake)
        {
            __instance.gameObject.AddComponent<ExternalRefComponent>();
            BepInSerializer.BridgeManager.logger.LogInfo("Aggressive patch called!");
        }
    }
}

#endif