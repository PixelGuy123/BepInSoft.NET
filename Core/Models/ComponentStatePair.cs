using UnityEngine;
using UnityEngine.XR;

namespace BepInSerializer.Core.Models;


internal struct ComponentStatePair()
{
    public Component Component;
    public ComponentSerializationState State
    {
        readonly get => _state;
        set
        {
            _state = value;
            HasStateDefined = value != null;
        }
    }
    public bool HasStateDefined { get; private set; }
    private ComponentSerializationState _state;

    public override readonly bool Equals(object obj) =>
        obj is ComponentStatePair pair && pair.Component == Component;
    public override readonly int GetHashCode() => HashCodeHelper.Combine(_state?.GetHashCode() ?? 0, Component.GetHashCode());
}