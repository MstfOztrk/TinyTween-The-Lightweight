using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyTween
{
    public enum TinyTweenType
    {
        Move,
        Rotate,
        Jump,
        Punch,
        PunchScale,
        CustomFloat
    }

    public enum TinyLoopType
    {
        Restart,
        Yoyo,
        Incremental
    }

    public enum TinyEaseType
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce
    }

    internal enum TinyRotateMode
    {
        QuaternionTo,
        EulerRelative
    }

    internal sealed class TinyTweenInstance
    {
        public long uniqueId;

        public Transform target;
        public bool useLocal;
        public bool useUnscaledTime;

        public TinyTweenType type;
        public TinyEaseType easeType;

        public float duration;
        public float delay;
        public float elapsed;

        public int loopsTotal;
        public TinyLoopType loopType;

        public bool isCompleted;

        public bool captureStartOnPlay;
        public bool started;

        public Vector3 baseStartPos;
        public Vector3 baseEndPos;

        public Quaternion baseStartRot;
        public Quaternion baseEndRot;

        public Vector3 baseStartEuler;
        public Vector3 baseEndEuler;
        public TinyRotateMode rotateMode;

        public bool isRelative;
        public Vector3 relativePos;
        public Vector3 relativeEuler;

        public float jumpHeight;
        public int jumpCount;

        public Vector3 punch;

        public Action onComplete;
        public Action<float> onUpdate;

        public bool timingSealed;

        public bool speedBased;
        public float speed;
        public bool durationResolved;
        public float estimatedDuration;

        public float customFloatFrom;
        public float customFloatTo;
        public Action<float> customFloatSetter;

        public void Reset()
        {
            uniqueId = 0;

            target = null;
            useLocal = false;
            useUnscaledTime = false;

            type = TinyTweenType.Move;
            easeType = TinyEaseType.Linear;

            duration = 0f;
            delay = 0f;
            elapsed = 0f;

            loopsTotal = 1;
            loopType = TinyLoopType.Restart;

            isCompleted = false;

            captureStartOnPlay = false;
            started = false;

            baseStartPos = Vector3.zero;
            baseEndPos = Vector3.zero;

            baseStartRot = Quaternion.identity;
            baseEndRot = Quaternion.identity;

            baseStartEuler = Vector3.zero;
            baseEndEuler = Vector3.zero;
            rotateMode = TinyRotateMode.QuaternionTo;

            isRelative = false;
            relativePos = Vector3.zero;
            relativeEuler = Vector3.zero;

            jumpHeight = 0f;
            jumpCount = 0;

            punch = Vector3.zero;

            onComplete = null;
            onUpdate = null;

            timingSealed = false;

            speedBased = false;
            speed = 0f;
            durationResolved = true;
            estimatedDuration = 0f;

            customFloatFrom = 0f;
            customFloatTo = 0f;
            customFloatSetter = null;
        }
    }

    public readonly struct TinyTweenHandle
    {
        internal readonly TinyTweenInstance tween;
        readonly long id;

        internal TinyTweenHandle(TinyTweenInstance tween)
        {
            this.tween = tween;
            id = tween != null ? tween.uniqueId : -1;
        }

        public bool IsValid => tween != null && tween.uniqueId == id && !tween.isCompleted;

        internal float GetDelay() => IsValid ? tween.delay : 0f;

        internal void SetDelayRaw(float delay)
        {
            if (!IsValid) return;
            tween.delay = delay;
        }

        internal void SealTiming()
        {
            if (!IsValid) return;
            tween.timingSealed = true;
        }

        internal float GetTotalDurationEstimate()
        {
            if (!IsValid) return 0f;
            if (tween.loopsTotal == -1) return float.PositiveInfinity;

            int loops = tween.loopsTotal;
            if (loops < 1) loops = 1;

            float perLoop = tween.duration;

            if (tween.speedBased && !tween.durationResolved)
            {
                perLoop = tween.estimatedDuration;
            }

            if (perLoop <= 0f) return 0f;
            return perLoop * loops;
        }

        public TinyTweenHandle SetSpeedBased()
        {
            if (!IsValid) return this;
            if (tween.timingSealed) return this;

            float spd = tween.duration;
            if (spd <= 0f) return this;

            tween.speedBased = true;
            tween.speed = spd;
            tween.duration = 0f;
            tween.durationResolved = false;

            tween.estimatedDuration = TinyTweenRunner.EstimateSpeedBasedDuration(tween);

            // Eğer tween zaten başlamışsa veya sequence için zorlanmışsa hemen hesapla
            if (tween.started)
                TinyTweenRunner.ResolveDurationIfNeeded(tween);

            return this;
        }

        public TinyTweenHandle SetLoops(int loops, TinyLoopType loopType = TinyLoopType.Restart)
        {
            if (!IsValid) return this;
            if (tween.timingSealed) return this;

            if (loops < -1) loops = 1;
            if (loops == 0) loops = 1;

            tween.loopsTotal = loops;
            tween.loopType = loopType;
            return this;
        }

        public TinyTweenHandle SetEase(TinyEaseType ease)
        {
            if (!IsValid) return this;
            tween.easeType = ease;
            return this;
        }

        public TinyTweenHandle SetDelay(float delay)
        {
            if (!IsValid) return this;
            if (tween.timingSealed) return this;

            tween.delay = delay;
            return this;
        }

        public TinyTweenHandle SetIgnoreTimeScale(bool ignore)
        {
            if (!IsValid) return this;
            tween.useUnscaledTime = ignore;
            return this;
        }

        public TinyTweenHandle OnComplete(Action callback)
        {
            if (!IsValid) return this;
            tween.onComplete += callback;
            return this;
        }

        public TinyTweenHandle OnUpdate(Action<float> callback)
        {
            if (!IsValid) return this;
            tween.onUpdate += callback;
            return this;
        }

        public void Kill()
        {
            if (tween == null || tween.uniqueId != id) return;
            var runner = TinyTweenRunner.InstanceOrNull;
            if (runner == null) return;
            runner.KillTween(tween);
        }

        public void Complete()
        {
            if (tween == null || tween.uniqueId != id) return;
            var runner = TinyTweenRunner.InstanceOrNull;
            if (runner == null) return;
            runner.CompleteTween(tween);
        }
    }

    public sealed class TinySequence
    {
        float _cursor;
        float _prevCursor;
        bool _locked;

        public TinySequence Append(TinyTweenHandle handle)
        {
            if (!handle.IsValid) return this;

            if (_locked)
            {
                handle.Kill();
                return this;
            }

            // KRİTİK FİX: Sadece SpeedBased olanları önceden resolve et.
            // Normal tween'ler (Duration bazlı) çalışacağı zaman pozisyon yakalasın.
            // Böylece Sequence içinde MoveTo düzgün çalışır.
            if (handle.tween.speedBased)
            {
                TinyTweenRunner.ForceStartAndResolveForSequence(handle.tween);
            }

            float d = handle.GetDelay();
            float dur = handle.GetTotalDurationEstimate();

            handle.SetDelayRaw(_cursor + d);
            _prevCursor = _cursor;

            handle.SealTiming();

            if (float.IsPositiveInfinity(dur))
            {
                _cursor = float.PositiveInfinity;
                _locked = true;
                return this;
            }

            _cursor += dur + d;
            return this;
        }

        public TinySequence Join(TinyTweenHandle handle)
        {
            if (!handle.IsValid) return this;

            if (_locked)
            {
                handle.Kill();
                return this;
            }

            if (handle.tween.speedBased)
            {
                TinyTweenRunner.ForceStartAndResolveForSequence(handle.tween);
            }

            float d = handle.GetDelay();
            float dur = handle.GetTotalDurationEstimate();

            handle.SetDelayRaw(_prevCursor + d);

            handle.SealTiming();

            if (float.IsPositiveInfinity(dur))
            {
                _cursor = float.PositiveInfinity;
                _locked = true;
                return this;
            }

            float end = _prevCursor + d + dur;
            if (end > _cursor) _cursor = end;

            return this;
        }

        public TinySequence AppendInterval(float interval)
        {
            if (_locked)
            {
                _cursor = float.PositiveInfinity;
                return this;
            }

            _prevCursor = _cursor;
            _cursor += interval;
            return this;
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class TinyTweenRunner : MonoBehaviour
    {
        static TinyTweenRunner instance;
        static bool isQuitting;

        readonly List<TinyTweenInstance> activeTweens = new List<TinyTweenInstance>(512);
        readonly Stack<TinyTweenInstance> pool = new Stack<TinyTweenInstance>(512);

        long globalIdCounter;

        const float PI = Mathf.PI;
        const float HPI = Mathf.PI / 2f;
        const float CycleEpsilon = 0.000001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            instance = null;
            isQuitting = false;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static TinyTweenRunner InstanceOrNull
        {
            get
            {
                if (isQuitting) return null;
                if (instance == null)
                {
                    var go = new GameObject("[TinyTweenRunner]");
                    instance = go.AddComponent<TinyTweenRunner>();
                }
                return instance;
            }
        }

        static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag > 0f)
            {
                float inv = 1f / mag;
                q.x *= inv;
                q.y *= inv;
                q.z *= inv;
                q.w *= inv;
                return q;
            }
            return Quaternion.identity;
        }

        internal TinyTweenHandle StartTween(
            Transform target,
            Vector3 startValue,
            Vector3 endValue,
            Quaternion startRot,
            Quaternion endRot,
            float duration,
            TinyTweenType type,
            bool useLocal,
            bool captureStartOnPlay,
            bool isRelative,
            Vector3 relativePos,
            Vector3 relativeEuler,
            TinyRotateMode rotateMode,
            float jumpHeight = 0f,
            int jumpCount = 0,
            Vector3 punchVector = default,
            Action<float> customSetter = null)
        {
            if (isQuitting) return default;

            TinyTweenInstance tw = pool.Count > 0 ? pool.Pop() : new TinyTweenInstance();
            tw.Reset();

            tw.uniqueId = ++globalIdCounter;

            tw.target = target;
            tw.useLocal = useLocal;

            tw.type = type;
            tw.duration = duration;
            tw.durationResolved = true;
            tw.estimatedDuration = duration;

            tw.captureStartOnPlay = captureStartOnPlay;
            tw.started = !captureStartOnPlay || type == TinyTweenType.CustomFloat;

            tw.isRelative = isRelative;
            tw.relativePos = relativePos;
            tw.relativeEuler = relativeEuler;

            tw.rotateMode = rotateMode;

            tw.baseEndPos = endValue;
            tw.baseEndRot = endRot;

            if (!captureStartOnPlay)
            {
                if (type == TinyTweenType.Rotate)
                {
                    tw.baseStartRot = startRot;
                }
                else if (type == TinyTweenType.PunchScale)
                {
                    tw.baseStartPos = target != null ? target.localScale : startValue;
                }
                else
                {
                    tw.baseStartPos = startValue;
                }
            }
            else
            {
                tw.baseStartPos = startValue;
                tw.baseStartRot = startRot;
            }

            tw.jumpHeight = jumpHeight;
            tw.jumpCount = jumpCount;
            tw.punch = punchVector;

            if (type == TinyTweenType.CustomFloat)
            {
                tw.customFloatFrom = startValue.x;
                tw.customFloatTo = endValue.x;
                tw.customFloatSetter = customSetter;
            }

            activeTweens.Add(tw);
            return new TinyTweenHandle(tw);
        }

        void Despawn(TinyTweenInstance tw)
        {
            tw.Reset();
            pool.Push(tw);
        }

        internal void KillTween(TinyTweenInstance tw)
        {
            if (tw != null) tw.isCompleted = true;
        }

        internal void CompleteTween(TinyTweenInstance tw)
        {
            if (tw == null || tw.isCompleted) return;

            ApplyCompleteValue(tw);
            tw.isCompleted = true;

            var cb = tw.onComplete;
            if (cb != null)
            {
                try { cb(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        internal static void ForceStartAndResolveForSequence(TinyTweenInstance tw)
        {
            if (tw == null) return;

            if (!tw.started)
            {
                if (tw.type == TinyTweenType.CustomFloat)
                {
                    tw.started = true;
                }
                else if (tw.target == null)
                {
                    tw.started = true;
                }
                else
                {
                    if (tw.type == TinyTweenType.Rotate)
                        tw.baseStartRot = tw.useLocal ? tw.target.localRotation : tw.target.rotation;
                    else if (tw.type == TinyTweenType.PunchScale)
                        tw.baseStartPos = tw.target.localScale;
                    else
                        tw.baseStartPos = tw.useLocal ? tw.target.localPosition : tw.target.position;

                    tw.started = true;
                }
            }

            ResolveDurationIfNeeded(tw);
        }

        internal static float EstimateSpeedBasedDuration(TinyTweenInstance tw)
        {
            if (tw == null) return 0f;
            float spd = tw.speed;
            if (spd <= 0f) return 0f;

            float dist = 0f;

            if (tw.type == TinyTweenType.Move || tw.type == TinyTweenType.Jump)
            {
                if (tw.isRelative)
                {
                    dist = tw.relativePos.magnitude;
                }
                else
                {
                    Vector3 s = tw.baseStartPos;
                    if (tw.captureStartOnPlay && tw.target != null)
                        s = tw.useLocal ? tw.target.localPosition : tw.target.position;
                    dist = Vector3.Distance(s, tw.baseEndPos);
                }
            }
            else if (tw.type == TinyTweenType.Rotate)
            {
                if (tw.rotateMode == TinyRotateMode.EulerRelative)
                {
                    dist = tw.relativeEuler.magnitude;
                }
                else
                {
                    Quaternion s = tw.baseStartRot;
                    if (tw.captureStartOnPlay && tw.target != null)
                        s = tw.useLocal ? tw.target.localRotation : tw.target.rotation;
                    dist = Quaternion.Angle(s, tw.baseEndRot);
                }
            }
            else if (tw.type == TinyTweenType.CustomFloat)
            {
                dist = Mathf.Abs(tw.customFloatTo - tw.customFloatFrom);
            }

            return dist > 0f ? (dist / spd) : 0f;
        }

        internal static void ResolveDurationIfNeeded(TinyTweenInstance tw)
        {
            if (tw == null) return;
            if (!tw.speedBased || tw.durationResolved) return;

            float spd = tw.speed;
            if (spd <= 0f)
            {
                tw.duration = 0f;
                tw.durationResolved = true;
                return;
            }

            float dist = 0f;

            if (tw.type == TinyTweenType.Move || tw.type == TinyTweenType.Jump)
            {
                // Speed based'de gerçek zamanlı hesaplama için:
                Vector3 currentStart = tw.baseStartPos;
                if (tw.started && tw.captureStartOnPlay && tw.target != null) 
                   currentStart = tw.useLocal ? tw.target.localPosition : tw.target.position;

                dist = tw.isRelative ? tw.relativePos.magnitude : Vector3.Distance(currentStart, tw.baseEndPos);
            }
            else if (tw.type == TinyTweenType.Rotate)
            {
                if (tw.rotateMode == TinyRotateMode.EulerRelative)
                    dist = tw.relativeEuler.magnitude;
                else
                    dist = Quaternion.Angle(tw.baseStartRot, tw.baseEndRot);
            }
            else if (tw.type == TinyTweenType.CustomFloat)
            {
                dist = Mathf.Abs(tw.customFloatTo - tw.customFloatFrom);
            }

            tw.duration = dist > 0f ? (dist / spd) : 0f;
            tw.durationResolved = true;
        }

        void EnsureStarted(TinyTweenInstance tw)
        {
            if (tw.started) return;

            if (tw.type == TinyTweenType.CustomFloat)
            {
                tw.started = true;
                return;
            }

            if (tw.target == null)
            {
                tw.started = true;
                return;
            }

            if (tw.type == TinyTweenType.Rotate)
            {
                if (tw.rotateMode == TinyRotateMode.EulerRelative)
                {
                    tw.baseStartEuler = tw.useLocal ? tw.target.localEulerAngles : tw.target.eulerAngles;
                    tw.baseEndEuler = tw.baseStartEuler + tw.relativeEuler;
                }
                else
                {
                    tw.baseStartRot = tw.useLocal ? tw.target.localRotation : tw.target.rotation;
                }
            }
            else if (tw.type == TinyTweenType.PunchScale)
            {
                tw.baseStartPos = tw.target.localScale;
            }
            else
            {
                tw.baseStartPos = tw.useLocal ? tw.target.localPosition : tw.target.position;
            }

            if (tw.isRelative)
            {
                if (tw.type == TinyTweenType.Move || tw.type == TinyTweenType.Jump)
                {
                    tw.baseEndPos = tw.baseStartPos + tw.relativePos;
                }
            }

            tw.started = true;
        }

        void ApplyCompleteValue(TinyTweenInstance tw)
        {
            EnsureStarted(tw);
            ResolveDurationIfNeeded(tw);

            int loops = tw.loopsTotal;
            if (loops == 0) loops = 1;

            if (tw.type == TinyTweenType.CustomFloat)
            {
                float from = tw.customFloatFrom;
                float to = tw.customFloatTo;

                float finalVal;

                if (loops == -1)
                {
                    finalVal = to;
                }
                else if (tw.loopType == TinyLoopType.Yoyo)
                {
                    finalVal = (loops % 2 == 0) ? from : to;
                }
                else if (tw.loopType == TinyLoopType.Incremental && loops > 1)
                {
                    float diff = to - from;
                    finalVal = from + diff * loops;
                }
                else
                {
                    finalVal = to;
                }

                var setter = tw.customFloatSetter;
                if (setter != null)
                {
                    try { setter(finalVal); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                var upd = tw.onUpdate;
                if (upd != null)
                {
                    try { upd(finalVal); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                return;
            }

            if (tw.type != TinyTweenType.CustomFloat && tw.target == null) return;

            if (tw.type == TinyTweenType.PunchScale)
            {
                tw.target.localScale = tw.baseStartPos;
                return;
            }

            if (tw.type == TinyTweenType.Punch)
            {
                Vector3 p = tw.baseStartPos;
                if (tw.useLocal) tw.target.localPosition = p;
                else tw.target.position = p;
                return;
            }

            if (tw.type == TinyTweenType.Rotate)
            {
                if (tw.rotateMode == TinyRotateMode.EulerRelative)
                {
                    Vector3 finalEuler;

                    if (loops == -1)
                    {
                        finalEuler = tw.baseEndEuler;
                    }
                    else if (tw.loopType == TinyLoopType.Yoyo)
                    {
                        finalEuler = (loops % 2 == 0) ? tw.baseStartEuler : tw.baseEndEuler;
                    }
                    else if (tw.loopType == TinyLoopType.Incremental && loops > 1)
                    {
                        Vector3 diff = tw.baseEndEuler - tw.baseStartEuler;
                        finalEuler = tw.baseStartEuler + diff * loops;
                    }
                    else
                    {
                        finalEuler = tw.baseEndEuler;
                    }

                    Quaternion r = Quaternion.Euler(finalEuler);
                    if (tw.useLocal) tw.target.localRotation = r;
                    else tw.target.rotation = r;
                }
                else
                {
                    Quaternion finalRot;

                    if (loops == -1)
                    {
                        finalRot = tw.baseEndRot;
                    }
                    else if (tw.loopType == TinyLoopType.Yoyo)
                    {
                        finalRot = (loops % 2 == 0) ? tw.baseStartRot : tw.baseEndRot;
                    }
                    else if (tw.loopType == TinyLoopType.Incremental && loops > 1)
                    {
                        Quaternion diff = tw.baseEndRot * Quaternion.Inverse(tw.baseStartRot);
                        diff.ToAngleAxis(out float angle, out Vector3 axis);
                        if (angle > 180f) angle -= 360f;

                        Quaternion step = Quaternion.AngleAxis(angle * loops, axis);
                        finalRot = tw.baseStartRot * step;
                    }
                    else
                    {
                        finalRot = tw.baseEndRot;
                    }

                    if (tw.useLocal) tw.target.localRotation = finalRot;
                    else tw.target.rotation = finalRot;
                }

                return;
            }

            Vector3 finalPos;

            if (loops == -1)
            {
                finalPos = tw.baseEndPos;
            }
            else if (tw.loopType == TinyLoopType.Yoyo)
            {
                finalPos = (loops % 2 == 0) ? tw.baseStartPos : tw.baseEndPos;
            }
            else if (tw.loopType == TinyLoopType.Incremental && loops > 1)
            {
                Vector3 diff = tw.baseEndPos - tw.baseStartPos;
                finalPos = tw.baseStartPos + diff * loops;
            }
            else
            {
                finalPos = tw.baseEndPos;
            }

            if (tw.useLocal) tw.target.localPosition = finalPos;
            else tw.target.position = finalPos;
        }

        float EvaluateEase(TinyEaseType ease, float t)
        {
            switch (ease)
            {
                case TinyEaseType.Linear: return t;
                case TinyEaseType.InSine: return 1f - Mathf.Cos(t * HPI);
                case TinyEaseType.OutSine: return Mathf.Sin(t * HPI);
                case TinyEaseType.InOutSine: return -(Mathf.Cos(PI * t) - 1f) / 2f;
                case TinyEaseType.InQuad: return t * t;
                case TinyEaseType.OutQuad: return 1f - (1f - t) * (1f - t);
                case TinyEaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case TinyEaseType.InCubic: return t * t * t;
                case TinyEaseType.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case TinyEaseType.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case TinyEaseType.InQuart: return t * t * t * t;
                case TinyEaseType.OutQuart: return 1f - Mathf.Pow(1f - t, 4f);
                case TinyEaseType.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;
                case TinyEaseType.InQuint: return t * t * t * t * t;
                case TinyEaseType.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case TinyEaseType.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;
                case TinyEaseType.InExpo: return t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case TinyEaseType.OutExpo: return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case TinyEaseType.InOutExpo: return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
                case TinyEaseType.InCirc: return 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
                case TinyEaseType.OutCirc: return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case TinyEaseType.InOutCirc: return t < 0.5f ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;
                case TinyEaseType.InBack: return 2.70158f * t * t * t - 1.70158f * t * t;
                case TinyEaseType.OutBack: return 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
                case TinyEaseType.InOutBack: return t < 0.5f ? (Mathf.Pow(2f * t, 2f) * ((2.5949095f + 1f) * 2f * t - 2.5949095f)) / 2f : (Mathf.Pow(2f * t - 2f, 2f) * ((2.5949095f + 1f) * (t * 2f - 2f) + 2.5949095f) + 2f) / 2f;
                case TinyEaseType.InElastic: return t == 0f ? 0f : t == 1f ? 1f : -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * ((2f * PI) / 3f));
                case TinyEaseType.OutElastic: return t == 0f ? 0f : t == 1f ? 1f : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * ((2f * PI) / 3f)) + 1f;
                case TinyEaseType.InOutElastic: return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * ((2f * PI) / 4.5f))) / 2f : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * ((2f * PI) / 4.5f))) / 2f + 1f;
                case TinyEaseType.InBounce: return 1f - EvaluateEase(TinyEaseType.OutBounce, 1f - t);
                case TinyEaseType.OutBounce: return t < 1f / 2.75f ? 7.5625f * t * t : t < 2f / 2.75f ? 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f : t < 2.5f / 2.75f ? 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f : 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;
                case TinyEaseType.InOutBounce: return t < 0.5f ? (1f - EvaluateEase(TinyEaseType.OutBounce, 1f - 2f * t)) / 2f : (1f + EvaluateEase(TinyEaseType.OutBounce, 2f * t - 1f)) / 2f;
                default: return t;
            }
        }

        void Update()
        {
            int count = activeTweens.Count;
            if (count == 0) return;

            float dt = Time.deltaTime;
            float unscaledDt = Time.unscaledDeltaTime;

            for (int i = count - 1; i >= 0; i--)
            {
                TinyTweenInstance tw = activeTweens[i];

                if (tw.isCompleted || (tw.type != TinyTweenType.CustomFloat && tw.target == null))
                {
                    RemoveAtSwapBack(i, tw);
                    continue;
                }

                float currentDt = tw.useUnscaledTime ? unscaledDt : dt;
                tw.elapsed += currentDt;

                float activeTime = tw.elapsed - tw.delay;
                if (activeTime < 0f)
                    continue;

                // Tween zamanı geldiğinde başlamasını sağla (Sequence için kritik)
                EnsureStarted(tw);
                ResolveDurationIfNeeded(tw);

                if (tw.duration <= 0f)
                {
                    ApplyCompleteValue(tw);
                    tw.isCompleted = true;

                    var cb0 = tw.onComplete;
                    RemoveAtSwapBack(i, tw);
                    if (cb0 != null)
                    {
                        try { cb0(); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                    continue;
                }

                int loops = tw.loopsTotal;
                if (loops == 0) loops = 1;

                double total = (double)tw.duration * (double)Mathf.Max(1, loops);
                bool finished = loops != -1 && ((double)activeTime >= total);

                float tForCycle = activeTime;
                if (tForCycle > 0f)
                {
                    tForCycle -= CycleEpsilon;
                    if (tForCycle < 0f) tForCycle = 0f;
                }

                long cycleIndexL = (long)(tForCycle / tw.duration);
                if (loops != -1 && cycleIndexL >= loops)
                    cycleIndexL = loops - 1;
                if (cycleIndexL < 0) cycleIndexL = 0;

                float cycleTime = activeTime - ((float)cycleIndexL * tw.duration);
                if (finished) cycleTime = tw.duration;

                float progress = cycleTime / tw.duration;
                if (progress < 0f) progress = 0f;
                if (progress > 1f) progress = 1f;

                float k = EvaluateEase(tw.easeType, progress);

                int parity = (int)(cycleIndexL & 1L);
                int cycleIndex = cycleIndexL > int.MaxValue ? int.MaxValue : (int)cycleIndexL;

                if (tw.type == TinyTweenType.CustomFloat)
                {
                    float v = Mathf.LerpUnclamped(tw.customFloatFrom, tw.customFloatTo, k);

                    var setter = tw.customFloatSetter;
                    if (setter != null)
                    {
                        try { setter(v); }
                        catch (Exception ex) { Debug.LogException(ex); tw.isCompleted = true; }
                    }

                    var upd = tw.onUpdate;
                    if (!tw.isCompleted && upd != null)
                    {
                        try { upd(v); }
                        catch (Exception ex) { Debug.LogException(ex); tw.isCompleted = true; }
                    }

                    if (tw.isCompleted)
                    {
                        RemoveAtSwapBack(i, tw);
                        continue;
                    }

                    if (finished)
                    {
                        ApplyCompleteValue(tw);
                        tw.isCompleted = true;

                        var cb1 = tw.onComplete;
                        RemoveAtSwapBack(i, tw);
                        if (cb1 != null)
                        {
                            try { cb1(); }
                            catch (Exception ex) { Debug.LogException(ex); }
                        }
                    }

                    continue;
                }

                if (tw.type == TinyTweenType.Rotate)
                {
                    if (tw.rotateMode == TinyRotateMode.EulerRelative)
                    {
                        Vector3 s = tw.baseStartEuler;
                        Vector3 e = tw.baseEndEuler;

                        if (tw.loopType == TinyLoopType.Incremental && cycleIndexL > 0)
                        {
                            Vector3 diff = tw.baseEndEuler - tw.baseStartEuler;
                            s = tw.baseStartEuler + diff * cycleIndex;
                            e = tw.baseStartEuler + diff * (cycleIndex + 1);
                        }
                        else if (tw.loopType == TinyLoopType.Yoyo && (parity == 1))
                        {
                            Vector3 tmp = s;
                            s = e;
                            e = tmp;
                        }

                        Vector3 ev = Vector3.LerpUnclamped(s, e, k);
                        Quaternion r = Quaternion.Euler(ev);
                        if (tw.useLocal) tw.target.localRotation = r;
                        else tw.target.rotation = r;
                    }
                    else
                    {
                        Quaternion s = tw.baseStartRot;
                        Quaternion e = tw.baseEndRot;

                        if (tw.loopType == TinyLoopType.Incremental && cycleIndexL > 0)
                        {
                            Quaternion diff = tw.baseEndRot * Quaternion.Inverse(tw.baseStartRot);
                            diff.ToAngleAxis(out float angle, out Vector3 axis);
                            if (angle > 180f) angle -= 360f;

                            Quaternion stepA = Quaternion.AngleAxis(angle * cycleIndexL, axis);
                            Quaternion stepB = Quaternion.AngleAxis(angle * (cycleIndexL + 1), axis);

                            s = tw.baseStartRot * stepA;
                            e = tw.baseStartRot * stepB;
                        }
                        else if (tw.loopType == TinyLoopType.Yoyo && (parity == 1))
                        {
                            Quaternion tmp = s;
                            s = e;
                            e = tmp;
                        }

                        Quaternion r = Quaternion.SlerpUnclamped(s, e, k);
                        if (k < 0f || k > 1f) r = NormalizeQuaternion(r);

                        if (tw.useLocal) tw.target.localRotation = r;
                        else tw.target.rotation = r;
                    }
                }
                else if (tw.type == TinyTweenType.PunchScale)
                {
                    float punchTime = k * tw.jumpCount * PI * 2f;
                    float decay = 1f - k;
                    float finalDecay = decay * decay * decay;

                    Vector3 punchVal = tw.punch * (Mathf.Sin(punchTime) * finalDecay);
                    tw.target.localScale = tw.baseStartPos + punchVal;
                }
                else
                {
                    Vector3 s = tw.baseStartPos;
                    Vector3 e = tw.baseEndPos;

                    if ((tw.type == TinyTweenType.Move || tw.type == TinyTweenType.Jump) && tw.loopType == TinyLoopType.Incremental && cycleIndexL > 0)
                    {
                        Vector3 diff = tw.baseEndPos - tw.baseStartPos;
                        s = tw.baseStartPos + diff * cycleIndex;
                        e = tw.baseEndPos + diff * cycleIndex;
                    }
                    else if ((tw.type == TinyTweenType.Move || tw.type == TinyTweenType.Jump) && tw.loopType == TinyLoopType.Yoyo && (parity == 1))
                    {
                        Vector3 tmp = s;
                        s = e;
                        e = tmp;
                    }

                    Vector3 result = s;

                    if (tw.type == TinyTweenType.Move)
                    {
                        result = Vector3.LerpUnclamped(s, e, k);
                    }
                    else if (tw.type == TinyTweenType.Jump)
                    {
                        result = Vector3.LerpUnclamped(s, e, k);
                        float jumpProgress = k * tw.jumpCount * PI;
                        if (k < 1f) result.y += Mathf.Sin(jumpProgress) * tw.jumpHeight;
                    }
                    else if (tw.type == TinyTweenType.Punch)
                    {
                        float punchTime = k * tw.jumpCount * PI * 2f;
                        float decay = 1f - k;
                        float finalDecay = decay * decay * decay;
                        result = tw.baseStartPos + (tw.punch * (Mathf.Sin(punchTime) * finalDecay));
                    }

                    if (tw.useLocal) tw.target.localPosition = result;
                    else tw.target.position = result;
                }

                var upd2 = tw.onUpdate;
                if (upd2 != null)
                {
                    try { upd2(k); }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        tw.isCompleted = true;
                    }
                }

                if (tw.isCompleted)
                {
                    RemoveAtSwapBack(i, tw);
                    continue;
                }

                if (finished)
                {
                    ApplyCompleteValue(tw);
                    tw.isCompleted = true;

                    var cb2 = tw.onComplete;
                    RemoveAtSwapBack(i, tw);
                    if (cb2 != null)
                    {
                        try { cb2(); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
            }
        }

        void RemoveAtSwapBack(int index, TinyTweenInstance tw)
        {
            int lastIndex = activeTweens.Count - 1;
            if (index < lastIndex) activeTweens[index] = activeTweens[lastIndex];
            activeTweens.RemoveAt(lastIndex);
            Despawn(tw);
        }
    }

    public static class TinyTweener
    {
        public static TinySequence Sequence() => new TinySequence();

        static TinyTweenHandle StartSafe(
            Transform target,
            Vector3 startValue,
            Vector3 endValue,
            Quaternion startRot,
            Quaternion endRot,
            float duration,
            TinyTweenType type,
            bool useLocal,
            bool captureStartOnPlay,
            bool isRelative,
            Vector3 relativePos,
            Vector3 relativeEuler,
            TinyRotateMode rotateMode,
            float jumpHeight = 0f,
            int jumpCount = 0,
            Vector3 punchVector = default,
            Action<float> customSetter = null)
        {
            var runner = TinyTweenRunner.InstanceOrNull;
            if (runner == null) return default;

            return runner.StartTween(
                target,
                startValue,
                endValue,
                startRot,
                endRot,
                duration,
                type,
                useLocal,
                captureStartOnPlay,
                isRelative,
                relativePos,
                relativeEuler,
                rotateMode,
                jumpHeight,
                jumpCount,
                punchVector,
                customSetter);
        }

        public static TinyTweenHandle Move(Transform target, Vector3 startValue, Vector3 endValue, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, startValue, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Move, useLocal, false, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo);
        }

        public static TinyTweenHandle MoveTo(Transform target, Vector3 endValue, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Move, useLocal, true, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo);
        }

        public static TinyTweenHandle MoveBy(Transform target, Vector3 amount, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Move, useLocal, true, true, amount, Vector3.zero, TinyRotateMode.QuaternionTo);
        }

        public static TinyTweenHandle Rotate(Transform target, Quaternion startValue, Quaternion endValue, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, startValue, endValue, duration, TinyTweenType.Rotate, useLocal, false, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo);
        }

        public static TinyTweenHandle RotateTo(Transform target, Quaternion endValue, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, Quaternion.identity, endValue, duration, TinyTweenType.Rotate, useLocal, true, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo);
        }

        public static TinyTweenHandle RotateTo(Transform target, Vector3 endEuler, float duration, bool useLocal = false)
            => RotateTo(target, Quaternion.Euler(endEuler), duration, useLocal);

        public static TinyTweenHandle RotateBy(Transform target, Vector3 eulerAmount, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Rotate, useLocal, true, true, Vector3.zero, eulerAmount, TinyRotateMode.EulerRelative);
        }

        public static TinyTweenHandle Jump(Transform target, Vector3 startValue, Vector3 endValue, float height, int count, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, startValue, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Jump, useLocal, false, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo, height, count);
        }

        public static TinyTweenHandle JumpTo(Transform target, Vector3 endValue, float height, int count, float duration, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Jump, useLocal, true, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo, height, count);
        }

        public static TinyTweenHandle Punch(Transform target, Vector3 punchVector, float duration, int vibrato = 3, bool useLocal = false)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Punch, useLocal, true, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo, 0f, vibrato, punchVector);
        }

        public static TinyTweenHandle PunchScale(Transform target, Vector3 punchVector, float duration, int vibrato = 3)
        {
            if (target == null) return default;
            return StartSafe(target, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.PunchScale, true, true, false, Vector3.zero, Vector3.zero, TinyRotateMode.QuaternionTo, 0f, vibrato, punchVector);
        }

        public static TinyTweenHandle Float(float from, float to, float duration, Action<float> onValue)
        {
            return StartSafe(
                null,
                new Vector3(from, 0f, 0f),
                new Vector3(to, 0f, 0f),
                Quaternion.identity,
                Quaternion.identity,
                duration,
                TinyTweenType.CustomFloat,
                false,
                false,
                false,
                Vector3.zero,
                Vector3.zero,
                TinyRotateMode.QuaternionTo,
                0f,
                0,
                default,
                onValue);
        }
    }

    public static class TinyTweenExtensions
    {
        public static TinyTweenHandle TMove(this Transform target, Vector3 endValue, float duration)
            => TinyTweener.MoveTo(target, endValue, duration, false);

        public static TinyTweenHandle TLocalMove(this Transform target, Vector3 endValue, float duration)
            => TinyTweener.MoveTo(target, endValue, duration, true);

        public static TinyTweenHandle TMoveBy(this Transform target, Vector3 amount, float duration)
            => TinyTweener.MoveBy(target, amount, duration, false);

        public static TinyTweenHandle TLocalMoveBy(this Transform target, Vector3 amount, float duration)
            => TinyTweener.MoveBy(target, amount, duration, true);

        public static TinyTweenHandle TRotate(this Transform target, Vector3 endEuler, float duration)
            => TinyTweener.RotateTo(target, endEuler, duration, false);

        public static TinyTweenHandle TRotate(this Transform target, Quaternion endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, false);

        public static TinyTweenHandle TLocalRotate(this Transform target, Vector3 endEuler, float duration)
            => TinyTweener.RotateTo(target, endEuler, duration, true);

        public static TinyTweenHandle TLocalRotate(this Transform target, Quaternion endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, true);

        public static TinyTweenHandle TRotateBy(this Transform target, Vector3 eulerAmount, float duration)
            => TinyTweener.RotateBy(target, eulerAmount, duration, false);

        public static TinyTweenHandle TLocalRotateBy(this Transform target, Vector3 eulerAmount, float duration)
            => TinyTweener.RotateBy(target, eulerAmount, duration, true);

        public static TinyTweenHandle TJump(this Transform target, Vector3 endValue, float duration, float height, int count)
            => TinyTweener.JumpTo(target, endValue, height, count, duration, false);

        public static TinyTweenHandle TLocalJump(this Transform target, Vector3 endValue, float duration, float height, int count)
            => TinyTweener.JumpTo(target, endValue, height, count, duration, true);

        public static TinyTweenHandle TPunch(this Transform target, Vector3 punchVector, float duration, int vibrato = 3)
            => TinyTweener.Punch(target, punchVector, duration, vibrato, false);

        public static TinyTweenHandle TLocalPunch(this Transform target, Vector3 punchVector, float duration, int vibrato = 3)
            => TinyTweener.Punch(target, punchVector, duration, vibrato, true);

        public static TinyTweenHandle TPunchScale(this Transform target, Vector3 punchVector, float duration, int vibrato = 3)
            => TinyTweener.PunchScale(target, punchVector, duration, vibrato);

        public static void Kill(this TinyTweenHandle handle) => handle.Kill();
        public static void Complete(this TinyTweenHandle handle) => handle.Complete();
        public static TinyTweenHandle SetLoops(this TinyTweenHandle handle, int loops, TinyLoopType type) => handle.SetLoops(loops, type);
        public static TinyTweenHandle SetEase(this TinyTweenHandle handle, TinyEaseType ease) => handle.SetEase(ease);
        public static TinyTweenHandle SetDelay(this TinyTweenHandle handle, float delay) => handle.SetDelay(delay);
        public static TinyTweenHandle SetSpeedBased(this TinyTweenHandle handle) => handle.SetSpeedBased();
        public static TinyTweenHandle SetIgnoreTimeScale(this TinyTweenHandle handle, bool ignore) => handle.SetIgnoreTimeScale(ignore);
        public static TinyTweenHandle OnComplete(this TinyTweenHandle handle, Action callback) => handle.OnComplete(callback);
        public static TinyTweenHandle OnUpdate(this TinyTweenHandle handle, Action<float> callback) => handle.OnUpdate(callback);
    }
}
