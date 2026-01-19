using System.Reflection;
using BepInSerializer.Utils;
using UnityEngine;

namespace BepInSerializer.Core.Serialization.Converters.Models;

/// <summary>
/// Context information for a field during conversion. This class is your primary way to interact with field data during conversion.
/// </summary>
public class FieldContext
{
    // ----- Public API -----
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldContext"/> class with original context.
    /// </summary>
    /// <param name="originalContext">The original context used before any conversions were applied.</param>
    /// <param name="fieldInfo">The current field to be held by.</param>
    public FieldContext(FieldContext originalContext, FieldInfo fieldInfo)
    {
        OriginalContext = originalContext ?? throw new System.ArgumentNullException(nameof(originalContext));
        Field = fieldInfo;
        OriginalValue = Field.CreateFieldGetter()(originalContext.OriginalValue);
        ContainsSerializeReference = Field.IsDefined(typeof(SerializeReference));
        CircularDependencyDetector = originalContext.CircularDependencyDetector;
    }
    /// <summary>
    /// The current original value of the field in this context.
    /// </summary>
    public readonly object OriginalValue;
    /// <summary>
    /// Indicates whether the field has the <see cref="SerializeReference"/> attribute.
    /// </summary>
    public readonly bool ContainsSerializeReference;
    /// <summary>
    /// Checks if the current context has a circular dependency detected.
    /// </summary>
    public bool HasCircularDependency => CircularDependencyDetector.HasCircularDependency(OriginalValue);

    // ----- Internal API ------
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldContext"/> class without original context.
    /// </summary>
    /// <param name="fieldInfo">The current field to be held by.</param>
    /// <param name="originalValue">The current value of this field.</param>

    internal FieldContext(FieldInfo fieldInfo, object originalValue)
    {
        Field = fieldInfo;
        OriginalValue = originalValue;
        ContainsSerializeReference = Field.IsDefined(typeof(SerializeReference));
        CircularDependencyDetector = new CircularDependencyDetector();
    }

    /// <summary>
    /// The circular dependency detector for this conversion process.
    /// </summary>
    internal readonly CircularDependencyDetector CircularDependencyDetector;
    /// <summary>
    /// The original context used before any conversions were applied.
    /// </summary>
    internal readonly FieldContext OriginalContext;
    /// <summary>
    /// The field information related to this context.
    /// </summary>
    internal readonly FieldInfo Field;
}