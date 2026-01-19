using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepInSerializer.Core.Models;

/// <summary>
/// Transient state holding the serialized data for a specific component instance.
/// Replaces the runtime overhead of the SerializationHandler component.
/// </summary>
internal class ComponentSerializationState(Component component, Type type)
{
    public Component Component { get; } = component;
    public Type ComponentType { get; } = type;
    public List<SerializedFieldData> Fields { get; } = new(8);
}
