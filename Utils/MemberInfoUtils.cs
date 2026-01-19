using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BepInSerializer.Utils;

internal static class MemberInfoUtils
{
    // Check if the field is a property
    public static bool IsFieldABackingField(this FieldInfo field) => field.IsDefined(typeof(CompilerGeneratedAttribute), false) || field.Name.Contains("k__BackingField");
    public static bool IsFieldABackingField(this MemberInfo info) =>
        info is FieldInfo field && field.IsFieldABackingField();

    // Check if the field can be serialized in general
    public static bool DoesFieldPassUnityValidationRules(this FieldInfo field)
    {
        var fieldType = field.FieldType;
        // Skip any private fields that aren't marked as SerializeField
        if (field.IsDefined(typeof(NonSerializedAttribute)) || (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false)))
            return false;

        return fieldType.IsSerializable || field.IsDefined(typeof(SerializeReference), false);
    }
}