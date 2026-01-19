namespace BepInSerializer.Core.Models;

internal enum SuppressMethod
{
    Null = 0,
    Awake = 1,
    OnEnable = 2,
    OnAfterDeserialize = 3
}