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

    internal class TinyTweenInstance
    {
        public long uniqueId;
        public Transform target;
        public Vector3 startPos;
        public Vector3 endPos;
        public Quaternion startRot;
        public Quaternion endRot;
        public float duration;
        public float delay;
        public float elapsed;
        public bool useLocal;
        public bool useUnscaledTime;
        public TinyTweenType type;
        public TinyEaseType easeType;
        public float jumpHeight;
        public int jumpCount;
        public Vector3 punch;
        public bool isCompleted;
        public int loops;
        public TinyLoopType loopType;
        public Action onComplete;
        public Action<float> onUpdate;

        public void Reset()
        {
            target = null;
            onComplete = null;
            onUpdate = null;
            isCompleted = false;
            elapsed = 0f;
            delay = 0f;
            loops = 1;
            loopType = TinyLoopType.Restart;
            easeType = TinyEaseType.Linear;
            useLocal = false;
            useUnscaledTime = false;
            jumpHeight = 0;
            jumpCount = 0;
            punch = Vector3.zero;
            startRot = Quaternion.identity;
            endRot = Quaternion.identity;
        }
    }

    public readonly struct TinyTweenHandle
    {
        internal readonly TinyTweenInstance tween;
        private readonly long id;

        internal TinyTweenHandle(TinyTweenInstance tween)
        {
            this.tween = tween;
            this.id = tween != null ? tween.uniqueId : -1;
        }

        public bool IsValid => tween != null && tween.uniqueId == id && !tween.isCompleted;

        internal float GetTotalDuration()
        {
            if (!IsValid) return 0f;
            if (tween.loops == -1) return float.MaxValue;
            return tween.duration * tween.loops;
        }

        internal float GetDelay() => IsValid ? tween.delay : 0f;

        public TinyTweenHandle SetSpeedBased()
        {
            if (IsValid)
            {
                float speed = tween.duration;
                if (speed <= 0f) return this;

                float distance = 0f;

                switch (tween.type)
                {
                    case TinyTweenType.Move:
                    case TinyTweenType.Jump:
                        distance = Vector3.Distance(tween.startPos, tween.endPos);
                        break;
                    case TinyTweenType.Rotate:
                        distance = Quaternion.Angle(tween.startRot, tween.endRot);
                        break;
                    case TinyTweenType.CustomFloat:
                        distance = Mathf.Abs(tween.endPos.x - tween.startPos.x);
                        break;
                    case TinyTweenType.Punch:
                    case TinyTweenType.PunchScale:
                        return this; 
                }

                if (distance > 0f)
                {
                    tween.duration = distance / speed;
                }
                else
                {
                    tween.duration = 0f;
                }
            }
            return this;
        }

        public TinyTweenHandle SetLoops(int loops, TinyLoopType loopType = TinyLoopType.Restart)
        {
            if (IsValid)
            {
                if (loops < -1) loops = 1;
                if (loops == 0) loops = 1;

                tween.loops = loops;
                tween.loopType = loopType;
            }
            return this;
        }

        public TinyTweenHandle SetEase(TinyEaseType ease)
        {
            if (IsValid) tween.easeType = ease;
            return this;
        }

        public TinyTweenHandle SetDelay(float delay)
        {
            if (IsValid) tween.delay = delay;
            return this;
        }

        public TinyTweenHandle SetIgnoreTimeScale(bool ignore)
        {
            if (IsValid) tween.useUnscaledTime = ignore;
            return this;
        }

        public TinyTweenHandle OnComplete(Action callback)
        {
            if (IsValid) tween.onComplete += callback;
            return this;
        }

        public TinyTweenHandle OnUpdate(Action<float> callback)
        {
            if (IsValid) tween.onUpdate += callback;
            return this;
        }

        public void Kill()
        {
            if (tween != null && tween.uniqueId == id)
            {
                TinyTweenRunner.Instance.KillTween(tween);
            }
        }

        public void Complete()
        {
            if (tween != null && tween.uniqueId == id)
            {
                TinyTweenRunner.Instance.CompleteTween(tween);
            }
        }
    }

    public struct TinySequence
    {
        private float _cursor;
        private float _prevCursor;

        public TinySequence Append(TinyTweenHandle handle)
        {
            if (!handle.IsValid) return this;

            float currentDelay = handle.GetDelay();
            float duration = handle.GetTotalDuration();

            handle.SetDelay(_cursor + currentDelay);

            _prevCursor = _cursor;
            _cursor += duration + currentDelay;

            return this;
        }

        public TinySequence Join(TinyTweenHandle handle)
        {
            if (!handle.IsValid) return this;

            float currentDelay = handle.GetDelay();
            float duration = handle.GetTotalDuration();

            handle.SetDelay(_prevCursor + currentDelay);

            float end = _prevCursor + currentDelay + duration;
            if (end > _cursor) _cursor = end;

            return this;
        }

        public TinySequence AppendInterval(float interval)
        {
            _prevCursor = _cursor;
            _cursor += interval;
            return this;
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class TinyTweenRunner : MonoBehaviour
    {
        static TinyTweenRunner instance;
        readonly List<TinyTweenInstance> activeTweens = new List<TinyTweenInstance>(512);
        readonly Stack<TinyTweenInstance> pool = new Stack<TinyTweenInstance>(512);
        long globalIdCounter = 0;
        const float PI = Mathf.PI;
        const float HPI = Mathf.PI / 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            instance = null;
        }

        public static TinyTweenRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[TinyTweenRunner]");
                    instance = go.AddComponent<TinyTweenRunner>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        internal TinyTweenHandle StartTween(Transform target, Vector3 startValue, Vector3 endValue, Quaternion startRot, Quaternion endRot, float duration, TinyTweenType type, bool useLocal, float jumpHeight = 0, int jumpCount = 0, Vector3 punchVector = default)
        {
            TinyTweenInstance tween = pool.Count > 0 ? pool.Pop() : new TinyTweenInstance();
            tween.Reset();

            tween.uniqueId = ++globalIdCounter;
            tween.target = target;
            tween.endPos = endValue;
            tween.startRot = startRot;
            tween.endRot = endRot;
            tween.duration = duration;
            tween.type = type;
            tween.useLocal = useLocal;
            tween.jumpHeight = jumpHeight;
            tween.jumpCount = jumpCount;
            tween.punch = punchVector;

            if (type == TinyTweenType.PunchScale)
            {
                if (target != null) tween.startPos = target.localScale;
            }
            else if (type == TinyTweenType.Rotate)
            {
            }
            else
            {
                tween.startPos = startValue;
            }

            activeTweens.Add(tween);
            return new TinyTweenHandle(tween);
        }

        private void Despawn(TinyTweenInstance tween)
        {
            tween.Reset();
            pool.Push(tween);
        }

        internal void KillTween(TinyTweenInstance tween)
        {
            if (tween != null) tween.isCompleted = true;
        }

        internal void CompleteTween(TinyTweenInstance tween)
        {
            if (tween == null || tween.isCompleted) return;
            
            ApplyFinalPosition(tween);
            tween.isCompleted = true;
            tween.onComplete?.Invoke();
        }

        void ApplyFinalPosition(TinyTweenInstance tween)
        {
            if (tween.target == null && tween.type != TinyTweenType.CustomFloat) return;

            if (tween.type == TinyTweenType.PunchScale)
            {
                tween.target.localScale = tween.startPos;
                return;
            }

            Vector3 finalPos = tween.startPos;
            Quaternion finalRot = tween.startRot;

            switch (tween.type)
            {
                case TinyTweenType.Move:
                case TinyTweenType.Jump:
                    finalPos = tween.endPos;
                    break;
                case TinyTweenType.Rotate:
                    finalRot = tween.endRot;
                    break;
                case TinyTweenType.Punch:
                    finalPos = tween.startPos;
                    break;
            }

            if (tween.target != null)
            {
                if (tween.type == TinyTweenType.Rotate)
                {
                    if (tween.useLocal) tween.target.localRotation = finalRot;
                    else tween.target.rotation = finalRot;
                }
                else
                {
                    if (tween.useLocal) tween.target.localPosition = finalPos;
                    else tween.target.position = finalPos;
                }
            }
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
                var tween = activeTweens[i];

                if (tween.isCompleted || (tween.target == null && tween.type != TinyTweenType.CustomFloat))
                {
                    RemoveAtSwapBack(i, tween);
                    continue;
                }

                float currentDt = tween.useUnscaledTime ? unscaledDt : dt;
                tween.elapsed += currentDt;

                float activeTime = tween.elapsed - tween.delay;
                float progress;
                
                if (tween.duration <= 0f)
                {
                    progress = activeTime >= 0f ? 1f : 0f;
                }
                else
                {
                    progress = activeTime / tween.duration;
                }
                
                progress = Mathf.Clamp01(progress);

                float k = EvaluateEase(tween.easeType, progress);

                tween.onUpdate?.Invoke(k);

                if (tween.isCompleted) 
                {
                    continue; 
                }

                if (tween.target != null)
                {
                    if (tween.type == TinyTweenType.Rotate)
                    {
                        Quaternion result = Quaternion.SlerpUnclamped(tween.startRot, tween.endRot, k);
                        if (tween.useLocal) tween.target.localRotation = result;
                        else tween.target.rotation = result;
                    }
                    else if (tween.type == TinyTweenType.PunchScale)
                    {
                        float punchTime = k * tween.jumpCount * PI * 2f;
                        float decay = 1f - k;
                        float finalDecay = decay * decay * decay;
                        
                        Vector3 punchVal = tween.punch * (Mathf.Sin(punchTime) * finalDecay);
                        tween.target.localScale = tween.startPos + punchVal;
                    }
                    else
                    {
                        Vector3 result = tween.startPos;
                        if (tween.type == TinyTweenType.Move)
                        {
                            result = Vector3.LerpUnclamped(tween.startPos, tween.endPos, k);
                        }
                        else if (tween.type == TinyTweenType.Jump)
                        {
                            result = Vector3.LerpUnclamped(tween.startPos, tween.endPos, k);
                            float jumpProgress = k * tween.jumpCount * PI;
                            if(k < 1f) result.y += Mathf.Sin(jumpProgress) * tween.jumpHeight;
                        }
                        else if (tween.type == TinyTweenType.Punch)
                        {
                            float punchTime = k * tween.jumpCount * PI * 2f;
                            float decay = 1f - k;
                            float finalDecay = decay * decay * decay;
                            result += tween.punch * (Mathf.Sin(punchTime) * finalDecay);
                        }

                        if (tween.useLocal) tween.target.localPosition = result;
                        else tween.target.position = result;
                    }
                }

                if (progress >= 1f)
                {
                    bool loopFinished = false;

                    if (tween.loops == -1) 
                    {
                        loopFinished = false; 
                    }
                    else if (tween.loops > 1) 
                    {
                        tween.loops--;
                        loopFinished = false;
                    }
                    else
                    {
                        loopFinished = true;
                    }

                    if (!loopFinished)
                    {
                        tween.elapsed = tween.delay;

                        if (tween.type == TinyTweenType.Rotate)
                        {
                            if (tween.loopType == TinyLoopType.Incremental)
                            {
                                Quaternion diff = tween.endRot * Quaternion.Inverse(tween.startRot);
                                tween.startRot = tween.endRot;
                                tween.endRot = tween.endRot * diff;
                            }
                            else if (tween.loopType == TinyLoopType.Yoyo)
                            {
                                Quaternion temp = tween.startRot;
                                tween.startRot = tween.endRot;
                                tween.endRot = temp;
                            }
                        }
                        else if (tween.type != TinyTweenType.Punch && tween.type != TinyTweenType.PunchScale)
                        {
                            if (tween.loopType == TinyLoopType.Incremental)
                            {
                                Vector3 diff = tween.endPos - tween.startPos;
                                tween.startPos = tween.endPos;
                                tween.endPos += diff;
                            }
                            else if (tween.loopType == TinyLoopType.Yoyo)
                            {
                                Vector3 temp = tween.startPos;
                                tween.startPos = tween.endPos;
                                tween.endPos = temp;
                            }
                        }
                    }
                    else
                    {
                        ApplyFinalPosition(tween);
                        tween.isCompleted = true;
                        
                        var callback = tween.onComplete;
                        RemoveAtSwapBack(i, tween);
                        callback?.Invoke();
                    }
                }
            }
        }

        private void RemoveAtSwapBack(int index, TinyTweenInstance tween)
        {
            int lastIndex = activeTweens.Count - 1;
            if (index < lastIndex) activeTweens[index] = activeTweens[lastIndex];
            activeTweens.RemoveAt(lastIndex);
            Despawn(tween);
        }
    }

    public static class TinyTweener
    {
        public static TinySequence Sequence() => new TinySequence();

        public static TinyTweenHandle Move(Transform target, Vector3 startValue, Vector3 endValue, float duration, bool useLocal = false)
            => TinyTweenRunner.Instance.StartTween(target, startValue, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Move, useLocal);

        public static TinyTweenHandle MoveTo(Transform target, Vector3 endValue, float duration, bool useLocal = false)
            => Move(target, useLocal ? target.localPosition : target.position, endValue, duration, useLocal);

        public static TinyTweenHandle MoveBy(Transform target, Vector3 amount, float duration, bool useLocal = false)
        {
            Vector3 start = useLocal ? target.localPosition : target.position;
            Vector3 end = start + amount;
            return Move(target, start, end, duration, useLocal);
        }

        public static TinyTweenHandle Rotate(Transform target, Quaternion startValue, Quaternion endValue, float duration, bool useLocal = false)
            => TinyTweenRunner.Instance.StartTween(target, Vector3.zero, Vector3.zero, startValue, endValue, duration, TinyTweenType.Rotate, useLocal);

        public static TinyTweenHandle RotateTo(Transform target, Quaternion endValue, float duration, bool useLocal = false)
            => Rotate(target, useLocal ? target.localRotation : target.rotation, endValue, duration, useLocal);

        public static TinyTweenHandle RotateTo(Transform target, Vector3 endValue, float duration, bool useLocal = false)
            => Rotate(target, useLocal ? target.localRotation : target.rotation, Quaternion.Euler(endValue), duration, useLocal);

        public static TinyTweenHandle RotateBy(Transform target, Vector3 amount, float duration, bool useLocal = false)
        {
            Quaternion start = useLocal ? target.localRotation : target.rotation;
            Quaternion end = start * Quaternion.Euler(amount); 
            return Rotate(target, start, end, duration, useLocal);
        }

        public static TinyTweenHandle Jump(Transform target, Vector3 startValue, Vector3 endValue, float height, int count, float duration, bool useLocal = false)
            => TinyTweenRunner.Instance.StartTween(target, startValue, endValue, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Jump, useLocal, height, count);

        public static TinyTweenHandle JumpTo(Transform target, Vector3 endValue, float height, int count, float duration, bool useLocal = false)
            => Jump(target, useLocal ? target.localPosition : target.position, endValue, height, count, duration, useLocal);

        public static TinyTweenHandle Punch(Transform target, Vector3 punchVector, float duration, int vibrato = 3, bool useLocal = false)
            => TinyTweenRunner.Instance.StartTween(target, useLocal ? target.localPosition : target.position, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.Punch, useLocal, 0, vibrato, punchVector);

        public static TinyTweenHandle PunchScale(Transform target, Vector3 punchVector, float duration, int vibrato = 3)
            => TinyTweenRunner.Instance.StartTween(target, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, duration, TinyTweenType.PunchScale, true, 0, vibrato, punchVector);
            
        public static TinyTweenHandle Float(float from, float to, float duration, Action<float> onUpdate)
        {
            var handle = TinyTweenRunner.Instance.StartTween(null, new Vector3(from, 0, 0), new Vector3(to, 0, 0), Quaternion.identity, Quaternion.identity, duration, TinyTweenType.CustomFloat, false);
            
            var tw = handle.tween;
            handle.OnUpdate((k) => {
                float lerpedVal = Mathf.LerpUnclamped(tw.startPos.x, tw.endPos.x, k);
                onUpdate?.Invoke(lerpedVal);
            });
            return handle;
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

        public static TinyTweenHandle TRotate(this Transform target, Vector3 endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, false);
        
        public static TinyTweenHandle TRotate(this Transform target, Quaternion endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, false);

        public static TinyTweenHandle TLocalRotate(this Transform target, Vector3 endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, true);

        public static TinyTweenHandle TLocalRotate(this Transform target, Quaternion endValue, float duration)
            => TinyTweener.RotateTo(target, endValue, duration, true);

        public static TinyTweenHandle TRotateBy(this Transform target, Vector3 amount, float duration)
            => TinyTweener.RotateBy(target, amount, duration, false);

        public static TinyTweenHandle TLocalRotateBy(this Transform target, Vector3 amount, float duration)
            => TinyTweener.RotateBy(target, amount, duration, true);

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
