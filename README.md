🚀 TinyTween: The Lightweight & Zero-Alloc Tween Engine for Unity

TinyTween is an ultra-lightweight, high-performance tweening engine for Unity, designed to ensure ZERO MEMORY ALLOCATION at runtime.

It is specifically optimized for mobile games, Hyper-Casual projects, and "Bullet Hell" style games where thousands of objects need to move simultaneously without triggering the Garbage Collector.

🔥 Why TinyTween?

1. ⚡ Zero Allocation

Unlike traditional tween libraries, TinyTween does not allocate memory using new for every animation start. It utilizes a smart Object Pooling system to recycle tween instances.

Result: No Garbage Collector (GC) spikes, no stuttering in your gameplay.

2. 🏎️ Performance-First Architecture

The backend Runner system manages C# Lists using the Swap Removal (O(1)) technique.

Result: Even moving 10,000+ objects simultaneously won't choke your CPU.

3. 🛡️ Struct-Based Handle System

The returned controller (TinyTweenHandle) is a struct, not a class.

Advantage: It lives and dies on the Stack, avoiding Heap allocation overhead entirely.

4. 📦 Single File, Drop-in Integration

No complex folder structures or DLL files. Just drag and drop TinyTween.cs into your project and start coding.

✨ Core Features

Move: Smooth movement to target.

Jump: Parabolic jumping movement.

Punch: Elastic vibration effect for Position or Scale.

Delay: Wait before starting the animation.

Loops:

Restart: Restart from beginning.

Yoyo: Ping-pong back and forth.

Incremental: Keep moving forward indefinitely (e.g., Climbing stairs).

Easing: Linear, Elastic, Bounce, Back, Sine, Quad, and many more...

💻 Usage Examples

Basic Movement

// Move object to (10, 0, 0) in 1 second
transform.TMove(new Vector3(10, 0, 0), 1f);


Jump & Easing

// Jump 5 units forward, 2 units high, 3 bounces
transform.TJump(targetPos, 1f, 2f, 3)
         .SetEase(TinyEaseType.OutBounce);


Delayed Start

// Wait for 0.5 seconds, then move
transform.TMove(targetPos, 1f)
         .SetDelay(0.5f);


Punch (Shake Effect)

// Damage effect: Shake the object
transform.TPunchScale(Vector3.one * 0.5f, 0.5f, 10);


Infinite Loop (Yoyo)

// Move back and forth endlessly
transform.TLocalMove(new Vector3(2, 0, 0), 1f)
         .SetLoops(-1, TinyLoopType.Yoyo)
         .SetEase(TinyEaseType.InOutSine);


🛠️ Installation

Download TinyTween.cs.

Drop it into your Unity project's Scripts folder.

Done! 🎉

TinyTween: No bloat, just pure performance.
