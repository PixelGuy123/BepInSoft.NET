#if DEBUG
using System.Collections;
using BepInSerializer.Test;
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

        // Make a basic sprite array
        var sampleTexture = Texture2D.blackTexture;
        Sprite[] testSprites = new Sprite[10];
        for (int i = 0; i < testSprites.Length; i++)
        {
            Rect spriteRect = new(0, 0, sampleTexture.width, sampleTexture.height);
            Vector2 pivot = new(0.5f, 0.5f);
            testSprites[i] = Sprite.Create(sampleTexture, spriteRect, pivot);
        }

        // Try to make a generic component for testing
        // 1. Create the GameObject
        GameObject animObj = new("TestAnimatedSprite");

        // 2. Add required components
        animObj.AddComponent<SpriteRenderer>();
        SpriteAnimator animator = animObj.AddComponent<SpriteAnimator>();

        // 3. Create the Animation data
        // We'll use the constructor: (int fps, UnderlyingType[] frames)
        // This will automatically create SpriteFrame objects internally
        SpriteAnimation walkAnim = new(12, testSprites);

        // 4. Assign the animation to the animator
        animator.currentAnimation = walkAnim;

        // 5. Clone the animator
        Object.Instantiate(animator);
        Object.Instantiate(animObj);
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
