using System.IO;
using System.Reflection;

namespace BepInSerializer.Utils;

internal static class PathUtils
{
    public static bool TryGetAssemblyDirectoryName(this Assembly assembly, out string path)
    {
        try
        {
            path = Path.GetDirectoryName(assembly.Location);
            return true;
        }
        catch
        {
            path = null;
            return false;
        }
    }
}