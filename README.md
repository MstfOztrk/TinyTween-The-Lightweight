# TinyTween

TinyTween is a lightweight tween utility for Unity, designed for projects that care about low overhead and predictable runtime behavior.

It lives in a single file at `Assets/GameAssets/Scripts/TinyTween.cs`, uses pooled tween instances internally, and was built mainly for playable ad workflows where memory churn and package weight matter.

## Why TinyTween

- Single-file setup, no extra DLLs or packages
- Struct-based handles for tween control
- Internal pooling for low runtime overhead
- Fluent API for ease, loops, delay, callbacks, and time-scale control
- Sequence support with `Append`, `Join`, and `AppendInterval`
- Built-in Penner-style easing set
- Speed-based tweening for supported types

## Performance Notes

TinyTween is optimized so the steady-state update path stays GC-friendly.

Important nuance:

- Transform and float tweens are designed to avoid per-frame allocations in normal playback
- Tween creation can still allocate on first use or when the pool grows
- User code can still allocate if it uses captured lambdas or multicast delegate patterns

If you want to reduce first-use spikes, call:

```csharp
TinyTweener.Warmup(64);
```

This creates the runner and pre-fills the internal tween pool.

## Installation

No package install is required.

1. Copy `Assets/GameAssets/Scripts/TinyTween.cs` into your project.
2. Optional: copy the scenario runner files if you also want the runtime test suite.

## Quick Start

```csharp
using TinyTween;
using UnityEngine;

public sealed class Example : MonoBehaviour
{
    void Start()
    {
        TinyTweener.Warmup(64);

        transform.TMove(new Vector3(10f, 0f, 0f), 1f)
            .SetEase(TinyEaseType.OutCubic);
    }
}
```

## Common Usage

### Move

```csharp
transform.TMove(new Vector3(10f, 0f, 0f), 1f);
transform.TLocalMove(new Vector3(0f, 2f, 0f), 0.5f);
transform.TMoveBy(new Vector3(1f, 0f, 0f), 0.25f);
```

### Rotate

```csharp
transform.TRotate(new Vector3(0f, 180f, 0f), 0.5f);
transform.TLocalRotateBy(new Vector3(0f, 90f, 0f), 0.3f);
```

### Jump

```csharp
transform.TJump(targetPosition, 1f, 3f, 2)
    .SetEase(TinyEaseType.Linear);
```

### Punch

```csharp
transform.TPunch(new Vector3(1f, 0f, 0f), 0.35f, 4);
transform.TPunchScale(Vector3.one * 0.25f, 0.25f, 4);
```

### Float

```csharp
TinyTweener.Float(0f, 1f, 0.4f, value =>
{
    canvasGroup.alpha = value;
})
.SetEase(TinyEaseType.InOutSine);
```

## Fluent API

```csharp
transform.TMove(new Vector3(10f, 0f, 0f), 2f)
    .SetEase(TinyEaseType.OutBounce)
    .SetLoops(2, TinyLoopType.Yoyo)
    .SetDelay(0.2f)
    .SetIgnoreTimeScale(true)
    .OnComplete(OnMoveFinished);
```

## Sequences

```csharp
var sequence = TinyTweener.Sequence();

sequence.Append(transform.TMove(new Vector3(4f, 0f, 0f), 0.4f));
sequence.Join(transform.TPunchScale(Vector3.one * 0.15f, 0.4f));
sequence.AppendInterval(0.15f);
sequence.Append(transform.TMove(Vector3.zero, 0.4f));
```

## Speed-Based Tweens

For supported tween types, `SetSpeedBased()` interprets the duration parameter as speed.

Supported:

- `Move`
- `Jump`
- `Rotate`
- `Float`

Example:

```csharp
transform.TMove(new Vector3(5f, 0f, 0f), 4f)
    .SetEase(TinyEaseType.Linear)
    .SetSpeedBased();
```

In the example above, `4f` means `4 units per second`, not `4 seconds`.

## Loop Types

- `Restart`: plays from the beginning again
- `Yoyo`: plays forward, then backward
- `Incremental`: keeps advancing from the previous loop result

```csharp
transform.TMoveBy(new Vector3(1f, 0f, 0f), 0.5f)
    .SetLoops(-1, TinyLoopType.Incremental);
```

## Handle Management

```csharp
TinyTweenHandle tween = transform.TMove(targetPosition, 1f);

if (tween.IsValid)
{
    tween.Kill();
    // or
    tween.Complete();
}
```

## Testing

The project includes a runtime scenario suite for contract testing and behavior probes.

Related files:

- `Assets/GameAssets/Scripts/TinyTweenScenarioRunner.cs`
- `Assets/GameAssets/Scripts/Editor/TinyTweenScenarioRunnerMenu.cs`

Current suite coverage includes:

- core motion and rotation
- delays, completion, killing, and zero-duration cases
- loops, yoyo, and incremental behavior
- jump, punch, and punch-scale behavior
- time-scale handling
- speed-based tweening
- sequence timing and future-state handling
- allocation observation probes

## Notes and Limitations

- `TinyTweenHandle` is a struct, so use `handle.IsValid` instead of null checks
- If a target `Transform` is destroyed during playback, TinyTween safely drops the tween
- `SetSpeedBased()` is intended for move, jump, rotate, and float tweens
- Ultra-short durations with elastic or bounce eases may still look aggressive between frames, even if final values are correct
- If you use captured lambdas in callbacks, that allocation behavior comes from the callback usage, not the tween update loop itself

## Intended Use

TinyTween was built for lean Unity projects and playable ads where:

- package size matters
- startup spikes matter
- runtime GC pressure matters
- a small DOTween-like API is enough

If you need a minimal tween layer without bringing in a larger dependency, TinyTween is a good fit.
