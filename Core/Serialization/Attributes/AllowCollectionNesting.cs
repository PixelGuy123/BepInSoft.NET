using System;
using System.Collections.Generic;

namespace BepInSerializer.Core.Serialization.Attributes;

/// <summary>
/// Explicitly tells the serializer that it can serialize jagged arrays (eg. <see cref="T:int[][]"/>), nested lists (eg. <see cref="List{int[]}"/>) and other types of nestings between collections.
/// </summary>
/// <remarks>This attribute doesn't magically make arrays or lists support nesting; this is only a reference for converters to pick up on. The default converters from this library support <see cref="AllowCollectionNesting"/>.</remarks>
[AttributeUsage(
    AttributeTargets.Field,
    AllowMultiple = false, // For allowing multiple Converters
    Inherited = false)]
public class AllowCollectionNesting : Attribute;

