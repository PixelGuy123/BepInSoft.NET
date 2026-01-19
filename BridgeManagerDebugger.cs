#if DEBUG
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInSerializer;

/// <summary>
/// Debug utility class for BridgeManager. Only compiled in DEBUG builds.
/// </summary>
internal static class BridgeManagerDebugger
{
    internal static IEnumerator WaitForGameplayRoutine(string targetScene)
    {
        yield return null;
        while (SceneManager.GetActiveScene().name != targetScene) yield return null;

        // Run benchmarks (uncomment as needed)
        RunBenchmark(1);
        // RunBenchmark(2);
        // RunBenchmark(5);
        // RunBenchmark(25);
        // RunBenchmark(50);
        // RunBenchmark(100);
    }

    private static void RunBenchmark(int instantiations)
    {
        var debug = BridgeManager.enableDebugLogs.Value;
        if (debug)
            BridgeManager.logger.LogInfo($"==== Starting benchmark with {instantiations} instantiations =====");

        System.Diagnostics.Stopwatch stopwatch = new();
        stopwatch.Start();

        // ----- BENCHMARK START -----
        var newObject = new GameObject($"OurTestSubject_{instantiations}").AddComponent<SerializationBridgeTester>();
        newObject.gameObject.AddComponent<SerializationBridgeTester>().payload.emptyList = ["NotSoEmptyAfterAll"];

        for (int i = 0; i < instantiations; i++)
        {
            if (debug)
                BridgeManager.logger.LogInfo($"==== Instantiating OurTestObject_{instantiations}_{i} =====");

            newObject = Object.Instantiate(newObject);
            newObject.VerifyIntegrity();

            if (debug)
                newObject.name = $"OurTestObject_{instantiations}_{i}";
        }
        // ----- BENCHMARK END -----

        stopwatch.Stop();
        BridgeManager.logger.LogInfo($"==== Instantiated {instantiations} Object(s) | Elapsed milliseconds: {stopwatch.ElapsedMilliseconds}ms ====");
    }
}

#endif
