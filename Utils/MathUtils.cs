using System;

namespace BepInSerializer.Utils;

internal static class MathUtils
{
    /// <summary>
    /// Make a slowly curved slope (similar to a logarithmic function) after certain threshold 'l' is passed. 
    /// </summary>
    /// <param name="x">The current x of the function.</param>
    /// <param name="l">The limit before turning into a logarithmic function.</param>
    /// <param name="k">The curving of the slope.</param>
    /// <returns></returns>
    public static double CalculateCurve(double x, double k = 1000.0)
    {
        if (k < 100.0)
            throw new ArgumentOutOfRangeException($"\'k\' is below 100 ({k}).");
        // Logarithmic segment
        return k * Math.Log(1.0 + x / k);
    }
}