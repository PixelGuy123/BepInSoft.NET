namespace BepInSerializer.Core.Models;

/// <summary>
/// Represents a single captured field value.
/// </summary>
internal readonly struct SerializedFieldData(string name, object value)
{
    public readonly string FieldName = name;
    public readonly object FieldValue = value;
}
