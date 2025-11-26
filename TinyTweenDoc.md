Markdown# 📘 TinyTween - Technical Manual

**Version:** 1.0.0 (Production Ready)  
**Platform:** Unity (C#)  
**License:** MIT  
**Date:** November 2025

---

## 📋 Table of Contents

- [📋 Table of Contents](#-table-of-contents)
- [1. Overview \& Architecture](#1-overview--architecture)
  - [Key Features](#key-features)
- [2. Installation](#2-installation)
- [3. Basic API Usage](#3-basic-api-usage)
  - [3.1 Movement (Move)](#31-movement-move)
- [🤝 Contributing](#-contributing)
- [❤️ License](#️-license)

---

## 1. Overview & Architecture

**TinyTween** is a lightweight, zero-allocation, performance-focused tweening library for Unity. It is designed as a "lightweight" alternative to heavier libraries like DOTween, specifically tailored for mobile and performance-critical projects.

### Key Features

- **Zero-Alloc:** Utilizes a custom `Struct` based handle system and internal object pooling to ensure **zero garbage collection** during runtime.
- **Safety:** Automatically handles `NullReferenceExceptions` if the target object is destroyed during animation.
- **Performance:** Uses a flat array and stack-based pooling architecture for CPU efficiency.

---

## 2. Installation

1.  Create a folder named `TinyTween` in your Unity project's `Assets` folder.
2.  Create a C# script named `TinyTween.cs`.
3.  Paste the provided library code into this script.
4.  No initialization is required; the `TinyTweenRunner` creates itself automatically upon the first call.

---

## 3. Basic API Usage

TinyTween uses C# Extension Methods, allowing you to call tweens directly on any `Transform` component.

### 3.1 Movement (Move)

Moves an object from its current position to a target position.

```csharp
// World Space Movement
transform.TMove(new Vector3(10, 5, 0), 1.5f);

// Local Space Movement
transform.TLocalMove(new Vector3(0, 5, 0), 1f);
3.2 JumpSimulates a parabolic jump movement towards a target.C#// Jump to (10,0,0) with a height of 2 units, bouncing 3 times, in 1 second.
transform.TJump(new Vector3(10, 0, 0), duration: 1f, height: 2f, count: 3);
3.3 Punch (Vibration Effects)Used for impacts, damage feedback, or UI clicks. The object returns to its original state after the effect.C#// Position Shake (Vibration)
// Strength: (1,1,0), Duration: 0.5s, Vibrato: 5
transform.TPunch(new Vector3(1, 1, 0), 0.5f, 5);

// Scale Punch (Jelly Effect)
transform.TPunchScale(Vector3.one * 0.5f, 0.4f, 4);
3.4 Generic Float TweenUsed to animate values other than Transform (e.g., UI Alpha, Audio Volume).C#TinyTweener.Float(from: 0f, to: 1f, duration: 2f, onUpdate: (val) =>
{
    myCanvasGroup.alpha = val; // Update UI Opacity
})
.SetEase(TinyEaseType.InOutQuad);
4. Advanced Features (Fluent API)Every tween call returns a TinyTweenHandle. You can chain methods to configure the tween.C#transform.TMove(targetPos, 2f)
    .SetEase(TinyEaseType.OutBounce)       // Set Easing Function
    .SetDelay(0.5f)                        // Wait 0.5s before starting
    .SetLoops(3, TinyLoopType.Yoyo)        // Loop 3 times (Ping-Pong)
    .SetIgnoreTimeScale(true)              // Run even if Time.timeScale is 0
    .OnUpdate((t) => Debug.Log("Progress: " + t)) // Call every frame
    .OnComplete(() => Debug.Log("Done!"));    // Call when finished
Loop TypesRestart: Resets to start value and plays again.Yoyo: Plays forward, then backwards.Incremental: Continues movement from the last position (useful for continuous rotation).5. SequencingUse Sequences to organize multiple tweens into a timeline.Append: Adds a tween to the end of the sequence.Join: Runs the tween simultaneously with the previous one.AppendInterval: Adds a delay.C#var seq = TinyTweener.Sequence();

// 1. Move Right
seq.Append(transform.TMove(new Vector3(5,0,0), 1f));

// 2. Scale Up WHILE Moving (Join)
seq.Join(transform.TPunchScale(Vector3.one, 1f));

// 3. Wait 0.5 seconds
seq.AppendInterval(0.5f);

// 4. Return to Zero
seq.Append(transform.TMove(Vector3.zero, 1f));
6. Performance & Limitations6.1 LimitationsNo Rigidbody Physics: Movement is done via transform.position. Not recommended for pushing physics objects against colliders.No Rotation Tween: Quaternion math is excluded to keep the library lightweight. Use TinyTweener.Float for rotations.No Animation Curves: Only TinyEaseType enums are supported to avoid GC allocation.6.2 Best PracticesHandle Check: TinyTweenHandle is a struct. Never check if (handle != null). Always use if (handle.IsValid).Safety: If a target object is destroyed, the tween is silently killed. OnComplete will NOT fire in this case.7. TroubleshootingSymptomPossible CauseSolutionAnimation doesn't startTime.timeScale might be 0.Use .SetIgnoreTimeScale(true).Instant CompletionDuration is 0 or negative.Check your duration value.Sequence Timing IssuesTrying to append an invalid tween.Ensure the tween target exists before appending to a sequence.Values not updatingLogic error in CustomFloat.Ensure your onUpdate callback is assigning the value to a property.
```

## 🤝 Contributing
Contributions are welcome! Feel free to update, improve, or customize the code to fit your needs. Pull requests are welcome.

## ❤️ License
This project is licensed under the MIT License.