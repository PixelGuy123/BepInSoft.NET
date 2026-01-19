using UnityEngine;

namespace BepInSerializer.Core.Models;


internal readonly struct ComponentStatePair(Component component, ComponentSerializationState state)
{
    public readonly Component Component = component;
    public readonly ComponentSerializationState State = state;
}