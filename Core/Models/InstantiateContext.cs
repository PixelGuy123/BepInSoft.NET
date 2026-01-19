using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepInSerializer.Core.Models;

/// <summary>
/// Context passed between Prefix and Postfix.
/// </summary>
internal class InstantiateContext(GameObject originalRoot)
{
    public GameObject OriginalRoot = originalRoot;
    // Map: RelativePath (hash) -> List of Component States (ordered by occurrence)
    public Dictionary<uint, List<ComponentSerializationState>> SnapshotData = [];

    // Cache for cleanup
    public List<Type> SuppressedTypes = [];
    public bool IsActive = true;
}