🚀 TinyTween

TinyTween is a lightweight, zero-allocation, performance-focused tweening library for Unity. Built with struct-based handles and an internal object pooling system, it allows you to animate properties without triggering Garbage Collection (GC) overhead.
Personally, I use it for playable ads.
Key Goal: To provide a robust "DOTween-like" experience with a fraction of the memory footprint.

✨ Features
Zero Allocation Handles: Uses struct handles to manipulate tweens, preventing memory garbage.

Object Pooling: Internal Stack pooling keeps the runtime memory footprint extremely low.

Fluent API: Chain commands easily (SetEase, SetLoops, OnComplete).

Powerful Sequences: Create complex timelines with Append, Join, and AppendInterval.

Rich Easing Library: Includes all standard Penner easing equations (In/Out/InOut Quad, Bounce, Elastic, Back, etc.).

📦 Installation
Simply drop the TinyTween folder into your Unity project's Assets directory. No complex setup required.

⚡ Quick Start
1. Basic Movement
Use extension methods for the quickest syntax.

C#

using TinyTween;

// Move to world position (10, 0, 0) in 1 second
transform.TMove(new Vector3(10, 0, 0), 1f);

// Move to local position
transform.TLocalMove(new Vector3(5, 5, 0), 0.5f);
2. Chaining Settings (Fluent API)
You can chain multiple settings like Easing, Loops, and Callbacks.

C#

transform.TMove(new Vector3(10, 0, 0), 2f)
    .SetEase(TinyEaseType.OutBounce)
    .SetLoops(2, TinyLoopType.Yoyo) // Go there and come back
    .SetDelay(0.5f)
    .OnComplete(() => Debug.Log("Motion Finished!"));
🎮 Core Animations
Jump
Simulates a parabolic jump movement.

C#

// Jump to target, with jump power of 3, doing 2 bounces, in 1 second
transform.TJump(targetPosition, 1f, 3f, 2).SetEase(TinyEaseType.Linear);
Punch (Vibration)
Great for impacts, UI clicks, or damage effects.

Position Punch: Shakes the object.

Scale Punch: Squeezes/stretches the object (Jelly effect).

C#

// Shake position with vector (1,1,0), duration 0.5s, vibrato 5
transform.TPunch(new Vector3(1, 1, 0), 0.5f, 5);

// Scale punch (Jelly effect)
transform.TPunchScale(Vector3.one * 0.5f, 0.3f, 4);
Generic Float Tween
Tween any float value (e.g., UI Alpha, Sound Volume, Shader Properties).

C#

TinyTweener.Float(0f, 1f, 1.5f, (val) => 
{
    myCanvasGroup.alpha = val; // Update UI opacity
})
.SetEase(TinyEaseType.InOutSine);
🎞️ Sequences
Sequences allow you to organize tweens into a timeline.

Append: Adds a tween to the end of the timeline.

Join: Adds a tween that runs simultaneously with the previous tween.

C#

var seq = TinyTweener.Sequence();

// 1. Move to right
seq.Append(transform.TMove(new Vector3(5, 0, 0), 1f));

// 2. Rotate or Scale at the same time (Join)
seq.Join(transform.TPunchScale(Vector3.one, 1f));

// 3. Wait for 0.5 seconds
seq.AppendInterval(0.5f);

// 4. Return to start
seq.Append(transform.TMove(Vector3.zero, 1f));
🔄 Loops
TinyTween supports 3 loop types:

Restart: Resets to start value and plays again.

Yoyo: Plays forward, then backwards (Ping-Pong).

Incremental: Continues movement from the last position (useful for infinite scrolling or continuous rotation).

C#

// Infinite rotating or moving
transform.TMove(new Vector3(1, 0, 0), 1f)
    .SetLoops(-1, TinyLoopType.Incremental);
🛠️ Management
You can store the TinyTweenHandle to control the tween later.

C#

TinyTweenHandle myTween = transform.TMove(target, 1f);

// Later...
if (myTween.IsValid)
{
    myTween.Kill();    // Stop immediately
    // OR
    myTween.Complete(); // Fast-forward to end and trigger OnComplete
}
🛑 Limitations / Notes
Struct-Based Handles: The TinyTweenHandle is a struct. It cannot be null. To check if a tween exists, use handle.IsValid.

Unity Objects: If the target Transform is destroyed while a tween is running, TinyTween automatically handles the exception and safely cleans up the tween.

❤️ License
This project is licensed under the MIT License - see the LICENSE file for details.
