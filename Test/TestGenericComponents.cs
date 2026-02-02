#if DEBUG
using UnityEngine;
using System;

namespace BepInSerializer.Test;

// --- 1. Concrete Frame Implementation ---
[Serializable]
public class SpriteFrame : CustomAnimationFrame<Sprite>
{
    // Necessary for the 'new()' constraint
    public SpriteFrame() : base() { }
    public SpriteFrame(Sprite value, float time) : base(value, time) { }
}

// --- 2. Concrete Animation Implementation ---
[Serializable]
public class SpriteAnimation : CustomAnimation<SpriteFrame, Sprite>
{
    public SpriteAnimation() : base() { }
    public SpriteAnimation(int fps, Sprite[] frames) : base(fps, frames) { }
    public SpriteAnimation(Sprite[] frames, float totalTime) : base(frames, totalTime) { }
    public SpriteAnimation(SpriteFrame[] frames) : base(frames) { }
}

// --- 3. Concrete Animator Implementation ---
public class SpriteAnimator : CustomAnimator<SpriteAnimation, SpriteFrame, Sprite>
{
}

// Stubs for the interfaces in your snippet
public interface ISimpleAnimator { }


public abstract class CustomAnimator<AnimationType, Frame, UnderlyingType> : MonoBehaviour, ISimpleAnimator
    where AnimationType : CustomAnimation<Frame, UnderlyingType>, new()
    where Frame : CustomAnimationFrame<UnderlyingType>, new()
{
    [SerializeField]
    public AnimationType currentAnimation;

    private void Awake() { }
}

[Serializable]
public abstract class CustomAnimationFrame<T>
{
    [SerializeField]
    public T value;

    [SerializeField]
    public float time;

    public CustomAnimationFrame()
    {
        value = default;
        time = 0f;
    }

    public CustomAnimationFrame(T value, float time)
    {
        this.value = value;
        this.time = time;
    }
}

[Serializable]
public abstract class CustomAnimation<Frame, UnderlyingType> where Frame : CustomAnimationFrame<UnderlyingType>, new()
{
    /// <summary>
    /// The amount of frames in the animation
    /// </summary>
    public Frame[] frames;

    /// <summary>
    /// The length of the animation in seconds.
    /// </summary>
    public float animationLength;

    /// <summary>
    /// Create an animation with the specified FPS
    /// </summary>
    /// <param name="fps"></param>
    /// <param name="frames"></param>
    public CustomAnimation(int fps, UnderlyingType[] frames)
    {
        this.frames = new Frame[frames.Length];
        float timePerFrame = 1000f / fps / 1000f;
        for (int i = 0; i < this.frames.Length; i++)
        {
            this.frames[i] = new Frame();
            this.frames[i].value = frames[i];
            this.frames[i].time = timePerFrame;
        }
        animationLength = frames.Length / (float)fps;
    }

    /// <summary>
    /// Create an animation that is totalTime long.
    /// </summary>
    /// <param name="frames"></param>
    /// <param name="totalTime"></param>
    public CustomAnimation(UnderlyingType[] frames, float totalTime)
    {
        this.frames = new Frame[frames.Length];
        float timePerFrame = totalTime / frames.Length;
        for (int i = 0; i < this.frames.Length; i++)
        {
            this.frames[i] = new Frame();
            this.frames[i].value = frames[i];
            this.frames[i].time = timePerFrame;
        }
        animationLength = totalTime;
    }

    public CustomAnimation(Frame[] frames)
    {
        this.frames = frames;
        for (int i = 0; i < this.frames.Length; i++)
        {
            animationLength += this.frames[i].time;
        }
    }

    public CustomAnimation()
    {
        this.frames = new Frame[0];
        this.animationLength = 0f;
    }
}
#endif