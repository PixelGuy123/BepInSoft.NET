using System.Reflection;
using System;
using System.Linq.Expressions;
using HarmonyLib;
using System.Collections.Generic;
using BepInSerializer.Core.Models;
using System.Linq;
using UnityEngine;

namespace BepInSerializer.Utils;

internal static class ReflectionUtils
{
    // Delegates for specific constructors
    public delegate Array ArrayConstructorDelegate(params int[] lengths);
    // Structs for caching keys
    internal record struct BaseTypeElementTypeItem(Type Base, Type[] Elements);
    internal record struct BaseTypeRankLengthItem(Type Base, int RankCount);
    // Caching system
    internal static LRUCache<FieldInfo, Func<object, object>> FieldInfoGetterCache;
    internal static LRUCache<PropertyInfo, Func<object, object>> PropertyInfoGetterCache;
    internal static LRUCache<FieldInfo, Func<object, object, object>> FieldInfoSetterCache;
    internal static LRUCache<PropertyInfo, Func<object, object, object>> PropertyInfoSetterCache;
    internal static LRUCache<string, Type> TypeNameCache;
    internal static LRUCache<string, Func<object, object>> ConstructorCache;
    internal static LRUCache<BaseTypeElementTypeItem, Func<object>> GenericActivatorConstructorCache;
    internal static LRUCache<Type, Func<object>> ParameterlessActivatorConstructorCache;
    internal static LRUCache<BaseTypeRankLengthItem, ArrayConstructorDelegate> ArrayActivatorConstructorCache;
    internal static LRUCache<Type, Func<object, object>> SelfActivatorConstructorCache;
    internal static LRUCache<Type, List<FieldInfo>> TypeToFieldsInfoCache;
    internal static LRUCache<Type, List<PropertyInfo>> TypeToPropertiesInfoCache;
    internal static LRUCache<Type, Dictionary<string, FieldInfo>> FieldInfoCache;

    // ----- Public API -----
    public static List<FieldInfo> GetUnserializableFieldInfos(this Type type)
    {
        bool isDebugEnabled = BridgeManager.enableDebugLogs.Value;
        bool isDeclaringTypeAComponent = type.IsUnityComponentType();
        return InternalGetUnserializableFieldInfos(type, isDeclaringTypeAComponent, isDebugEnabled);
    }

    public static List<PropertyInfo> GetUnserializablePropertyInfos(this Type type)
    {
        if (TypeToPropertiesInfoCache.NullableTryGetValue(type, out var properties))
            return properties;

        properties = [];

        foreach (var property in AccessTools.GetDeclaredProperties(type))
        {
            if (ShouldIncludeProperty(property))
                properties.Add(property);
        }

        // Get the base type for properties
        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            properties.AddRange(baseType.GetUnserializablePropertyInfos());
        }

        TypeToPropertiesInfoCache.NullableAdd(type, properties);
        return properties;
    }


    public static Func<object, object> CreateFieldGetter(this FieldInfo fieldInfo) =>
        CreateMemberGetter(
            fieldInfo,
            FieldInfoGetterCache,
            fi => fi.DeclaringType,
            fi => fi.FieldType,
            Expression.Field
        );


    public static Func<object, object> CreatePropertyGetter(this PropertyInfo propertyInfo) =>
        CreateMemberGetter(
            propertyInfo,
            PropertyInfoGetterCache,
            pi => pi.DeclaringType,
            pi => pi.PropertyType,
            Expression.Property
        );

    public static Func<object, object, object> CreateFieldSetter(this FieldInfo fieldInfo) =>
        CreateMemberSetter(
            fieldInfo,
            FieldInfoSetterCache,
            fi => fi.DeclaringType,
            fi => fi.FieldType,
            Expression.Field
        );


    public static Func<object, object, object> CreatePropertySetter(this PropertyInfo propertyInfo) =>
        CreateMemberSetter(
            propertyInfo,
            PropertyInfoSetterCache,
            pi => pi.DeclaringType,
            pi => pi.PropertyType,
            Expression.Property
        );



    // There are some Unity components that have their own constructor for duplication (eg.: new Material(Material))
    public static bool TryGetSelfActivator(this Type type, out Func<object, object> func)
    {
        if (SelfActivatorConstructorCache.NullableTryGetValue(type, out func)) return true;

        var selfConstructor = type.GetConstructor([type]); // Get a constructor that is itself
        if (selfConstructor == null)
        {
            func = null;
            return false;
        }

        // Get the parameter as object
        var parameter = Expression.Parameter(typeof(object), "self"); // (object self) => { }
        // Cast the parameter as desired type
        var typedParameter = Expression.Convert(parameter, type); // (object self) => { (Material)self }
        // Put this parameter to be used inside the constructor
        var newExpression = Expression.New(selfConstructor, typedParameter); // (object self) => new Material((Material)self);
        // Compile expression
        func = Expression.Lambda<Func<object, object>>(newExpression, parameter).Compile();
        SelfActivatorConstructorCache.NullableAdd(type, func);
        return true;
    }

    public static Func<object> GetParameterlessConstructor(this Type type)
    {
        // Check if the type has a generic constructor to use the other function instead
        if (type.IsGenericType || type.IsGenericTypeDefinition)
        {
            // BridgeManager.logger.LogInfo($"This is a parameterless generic type: {type}. Using generic parameterless constructor.");
            return type.GetGenericParameterlessConstructor(type.GetGenericArguments());
        }

        // Check for cache
        if (ParameterlessActivatorConstructorCache.NullableTryGetValue(type, out var func)) return func;
        var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
        if (parameterlessConstructor == null)
        {
            ParameterlessActivatorConstructorCache.NullableAdd(type, null);
            return null;
        }

        // Create the Expression: () => new Type()
        NewExpression newExp = Expression.New(parameterlessConstructor);

        // Cast to object so the delegate is compatible with Func<object>
        UnaryExpression castExp = Expression.Convert(newExp, typeof(object));

        // Compile it into a reusable delegate
        func = Expression.Lambda<Func<object>>(castExp).Compile(); // () => (object)new Type();
        ParameterlessActivatorConstructorCache.NullableAdd(type, func);
        return func;
    }
    public static Func<object> GetGenericParameterlessConstructor(this Type genericDefinition, params Type[] elementTypes)
    {
        // If it is not already a type definition, make it one
        if (!genericDefinition.IsGenericTypeDefinition)
            genericDefinition = genericDefinition.GetGenericTypeDefinition();

        var typeElement = new BaseTypeElementTypeItem(genericDefinition, elementTypes);

        if (GenericActivatorConstructorCache.NullableTryGetValue(typeElement, out var func))
            return func;

        // Create the constructed generic type: List<T> becomes List<int>
        if (elementTypes.Length != genericDefinition.GetGenericArguments().Length)
            throw new ArgumentException($"Number of type arguments ({elementTypes.Length}) doesn't match the generic type definition's arity ({genericDefinition.GetGenericArguments().Length})");

        Type constructedType = genericDefinition.MakeGenericType(elementTypes);

        // Get the appropriate constructor
        var constructor = constructedType.GetConstructor(Type.EmptyTypes);

        Expression newExp;

        if (constructor != null)
        {
            // Class with explicit parameterless constructor or struct with explicit parameterless constructor
            newExp = Expression.New(constructor);
        }
        else if (constructedType.IsValueType)
        {
            // using Expression.Default, which creates the default value for value types
            newExp = Expression.Default(constructedType);
        }
        else
        {
            // If it doesn't contain a parameterless constructor and it's a class, just return a null func
            func = null;
            GenericActivatorConstructorCache.NullableAdd(typeElement, func);
            return func;
        }

        // Cast to object so the delegate is compatible with Func<object>
        UnaryExpression castExp = Expression.Convert(newExp, typeof(object));

        // Compile it into a reusable delegate
        func = Expression.Lambda<Func<object>>(castExp).Compile();
        GenericActivatorConstructorCache.NullableAdd(typeElement, func);

        return func;
    }

    public static ArrayConstructorDelegate GetArrayConstructor(this Type elementType, int rankCount)
    {
        if (rankCount < 1) throw new ArgumentException("Rank count must be at least 1.");
        var rankLengthItem = new BaseTypeRankLengthItem(elementType, rankCount);
        if (ArrayActivatorConstructorCache.NullableTryGetValue(rankLengthItem, out var func))
            return func;

        // Create parameter expression for the array lengths
        ParameterExpression lengthsParam = Expression.Parameter(typeof(int[]), "lengths");

        // Create new array expression: new T[lengths[0], lengths[1], ..., lengths[rankCount-1]]
        NewArrayExpression newArrayExp = Expression.NewArrayBounds(elementType,
            Enumerable.Range(0, rankCount).Select(i =>
                Expression.ArrayIndex(lengthsParam, Expression.Constant(i))
            )
        );

        // Cast the T[,] as System.Array
        UnaryExpression castExp = Expression.TypeAs(newArrayExp, typeof(Array));

        // Compile the lambda: (int[] lengths) => (Array)new T[lengths[0], lengths[1], ..., lengths[rankCount-1]]
        func = Expression.Lambda<ArrayConstructorDelegate>(castExp, lengthsParam).Compile();
        ArrayActivatorConstructorCache.NullableAdd(rankLengthItem, func);

        return func;
    }


    public static Type GetFastType(string compName)
    {
        // Expensive lookup if no cache available
        if (TypeNameCache == null)
            return Type.GetType(compName);

        // Fast Type Lookup
        if (!TypeNameCache.TryGetValue(compName, out Type compType))
        {
            compType = Type.GetType(compName);
            if (compType != null) TypeNameCache.Add(compName, compType);
        }
        return compType;
    }

    public static FieldInfo GetFastField(this Type compType, string fieldName)
    {
        if (FieldInfoCache == null) return AccessToolsNoLogging.Field(compType, fieldName);

        var fields = FieldInfoCache.GetValue(compType, t => []);
        if (!fields.TryGetValue(fieldName, out var field))
        {
            field = AccessToolsNoLogging.Field(compType, fieldName);
            fields[fieldName] = field;
        }
        return field;
    }

    public static bool IsTypeHierarchyGeneric(this Type type)
    {
        Type t = type;
        while (t != null && t != typeof(object) && t != typeof(UnityEngine.Object))
        {
            if (t.IsGenericType) return true;
            t = t.BaseType;
        }
        return false;
    }

    // ----- Private API -----
    private static bool ShouldIncludeField(FieldInfo field, bool isDeclaringTypeAComponent, bool isDebugEnabled)
    {
        // Static or non-writeable is irrelevant
        if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
            return false;

        // Apply Serialization implementation (private fields can't be serialized, like in Unity)
        if (!field.DoesFieldPassUnityValidationRules())
        {
            if (isDebugEnabled)
                BridgeManager.logger.LogInfo($"{field.Name} SKIPPED FOR NOT PASSING VALIDATION RULES.");
            return false;
        }
        if (isDebugEnabled)
            BridgeManager.logger.LogInfo($"IsDeclaringTypeComponent: {isDeclaringTypeAComponent}");
        if (isDeclaringTypeAComponent && field.FieldType.CanUnitySerialize())
        {
            if (isDebugEnabled)
                BridgeManager.logger.LogInfo($"{field.Name} SKIPPED FOR NATIVE SERIALIZATION.");
            return false;
        }

        return true;
    }

    private static bool ShouldIncludeProperty(PropertyInfo property)
    {
        // Static is irrelevant
        if (property.IsStatic())
            return false;

        // The property must have getter and setter, and one getter that's without parameters
        if (!property.CanWrite ||
            property.GetGetMethod(true) == null ||
            property.GetGetMethod(true).GetParameters().Length != 0)
            return false;

        return true;
    }
    // Recursively search, but already knowing the declaring type is a component from the beginning
    private static List<FieldInfo> InternalGetUnserializableFieldInfos(Type type, bool isDeclaringTypeAComponent, bool isDebugEnabled)
    {
        if (TypeToFieldsInfoCache.NullableTryGetValue(type, out var fields))
            return fields;

        fields = [];
        foreach (var field in AccessTools.GetDeclaredFields(type))
        {
            if (ShouldIncludeField(field, isDeclaringTypeAComponent, isDebugEnabled))
                fields.Add(field);
        }

        // Try to get fields from base types
        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object) && baseType != typeof(Component))
            fields.AddRange(InternalGetUnserializableFieldInfos(baseType, isDeclaringTypeAComponent, isDebugEnabled));

        TypeToFieldsInfoCache.NullableAdd(type, fields);
        return fields;
    }

    private static Func<object, object> CreateMemberGetter<TMember>(
    TMember memberInfo,
    LRUCache<TMember, Func<object, object>> cache,
    Func<TMember, Type> getDeclaringType,
    Func<TMember, Type> getMemberType,
    Func<Expression, TMember, Expression> createMemberAccess)
    where TMember : MemberInfo
    {
        if (cache.NullableTryGetValue(memberInfo, out var getter))
            return getter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instanceParam, getDeclaringType(memberInfo));
        var memberExp = createMemberAccess(typedInstance, memberInfo);

        var resultExp = getMemberType(memberInfo).IsValueType
            ? Expression.Convert(memberExp, typeof(object))
            : memberExp;

        getter = Expression.Lambda<Func<object, object>>(resultExp, instanceParam).Compile();
        cache.NullableAdd(memberInfo, getter);
        return getter;
    }

    private static Func<object, object, object> CreateMemberSetter<TMember>(
    TMember memberInfo,
    LRUCache<TMember, Func<object, object, object>> cache,
    Func<TMember, Type> getDeclaringType,
    Func<TMember, Type> getMemberType,
    Func<Expression, TMember, Expression> createMemberAccess)
    where TMember : MemberInfo
    {
        if (cache.NullableTryGetValue(memberInfo, out var setter))
            return setter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var typedInstance = Expression.Variable(getDeclaringType(memberInfo), "typedInstance");
        var typedValue = Expression.Convert(valueParam, getMemberType(memberInfo));

        // Unbox, modify, then box back
        var block = Expression.Block(
            [typedInstance],
            Expression.Assign(typedInstance, Expression.Convert(instanceParam, getDeclaringType(memberInfo))),
            Expression.Assign(createMemberAccess(typedInstance, memberInfo), typedValue),
            Expression.Convert(typedInstance, typeof(object))  // Box modified struct
        );

        setter = Expression.Lambda<Func<object, object, object>>(block, instanceParam, valueParam).Compile();
        cache.NullableAdd(memberInfo, setter);
        return setter;
    }
}