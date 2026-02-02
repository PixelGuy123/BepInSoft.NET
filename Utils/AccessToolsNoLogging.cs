using System;
using System.Reflection;
using HarmonyLib;

namespace BepInSerializer.Utils;

// A copycat of AccessTools, but that doesn't log unnecessary "field not found" stuff in the console; 
// idk why they just didn't add a parameter to suppress any of its warnings
internal static class AccessToolsNoLogging
{
    public static FieldInfo Field(Type type, string name)
    {
        if (type == null || name == null)
            return null;

        FieldInfo fieldInfo = AccessTools.FindIncludingBaseTypes(type, t => t.GetField(name, AccessTools.all));
        return fieldInfo;
    }
}