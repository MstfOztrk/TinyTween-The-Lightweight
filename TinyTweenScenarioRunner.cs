using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TinyTween;
using UnityEngine;
using Unity.Profiling;

[AddComponentMenu("Tests/TinyTween Scenario Runner")]
public sealed class TinyTweenScenarioRunner : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] bool autoRunOnStart;
    [SerializeField] bool includeObservationProbes = true;
    [SerializeField] bool includeAllocationProbes = true;
    [SerializeField] bool stopOnFirstFailure;
    [SerializeField] bool verbosePassLogs;
    [SerializeField] bool keepSpawnedObjectsAfterRun;

    [Header("Tolerance")]
    [SerializeField] float positionTolerance = 0.035f;
    [SerializeField] float rotationTolerance = 2f;
    [SerializeField] float floatTolerance = 0.05f;
    [SerializeField] float idleTolerance = 0.02f;
    [SerializeField] float timeTolerance = 0.18f;

    [Header("Spawn")]
    [SerializeField] Vector3 spawnOrigin = new Vector3(1000f, 1000f, 1000f);
    [SerializeField] float scenarioSpacing = 6f;

    [Header("Last Run")]
    [TextArea(6, 24)]
    [SerializeField] string lastSummary;

    readonly List<string> failures = new List<string>(32);
    readonly List<string> observations = new List<string>(16);

    sealed class GcStats
    {
        public long TotalBytes;
        public long PeakBytes;
        public int Frames;

        public long AverageBytes => Frames > 0 ? TotalBytes / Frames : 0L;

        public void Add(long bytes)
        {
            TotalBytes += bytes;
            if (bytes > PeakBytes)
                PeakBytes = bytes;
            Frames++;
        }

        public void Merge(GcStats other)
        {
            if (other == null || other.Frames == 0)
                return;

            TotalBytes += other.TotalBytes;
            if (other.PeakBytes > PeakBytes)
                PeakBytes = other.PeakBytes;
            Frames += other.Frames;
        }
    }

    Coroutine runRoutine;
    GameObject suiteRoot;
    bool abortRequested;
    bool scenarioFailed;
    string scenarioName;
    int contractPassed;
    int contractFailed;
    int observationCount;
    int spawnIndex;

    public bool IsRunning => runRoutine != null;
    public bool HasSummary => !string.IsNullOrEmpty(lastSummary);
    public string LastSummary => lastSummary;
    public int ContractPassed => contractPassed;
    public int ContractFailed => contractFailed;

    void Start()
    {
        if (autoRunOnStart && Application.isPlaying)
            BeginRun();
    }

    [ContextMenu("Run TinyTween Suite")]
    public void BeginRun()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TinyTweenTest] Enter Play Mode before running the suite.");
            return;
        }

        if (runRoutine != null)
        {
            Debug.LogWarning("[TinyTweenTest] Suite already running.");
            return;
        }

        runRoutine = StartCoroutine(RunSuite());
    }

    [ContextMenu("Clear TinyTween Summary")]
    public void ClearSummary()
    {
        lastSummary = string.Empty;
    }

    public void ConfigureForBatchRun()
    {
        autoRunOnStart = true;
        includeObservationProbes = true;
        includeAllocationProbes = true;
        stopOnFirstFailure = false;
        verbosePassLogs = true;
        keepSpawnedObjectsAfterRun = false;
    }

    IEnumerator RunSuite()
    {
        ResetSuite();
        EnsureRoot();
        Debug.Log("[TinyTweenTest] Starting TinyTween scenario suite.");

        yield return RunContract("MoveTo reaches world end", MoveToReachesWorldEnd);
        yield return RunContract("MoveBy applies world offset", MoveByAppliesWorldOffset);
        yield return RunContract("LocalMove respects parent space", LocalMoveRespectsParentSpace);
        yield return RunContract("RotateTo reaches target rotation", RotateToReachesTargetRotation);
        yield return RunContract("RotateBy applies local delta", RotateByAppliesLocalDelta);
        yield return RunContract("Float updates and completes", FloatUpdatesAndCompletes);
        yield return RunContract("Delay blocks start", DelayBlocksStart);
        yield return RunContract("Kill stops without complete callback", KillStopsWithoutCompleteCallback);
        yield return RunContract("Complete snaps immediately", CompleteSnapsImmediately);
        yield return RunContract("Zero duration completes", ZeroDurationCompletes);
        yield return RunContract("Yoyo even loops end at start", YoyoEvenEndsAtStart);
        yield return RunContract("Incremental move accumulates", IncrementalMoveAccumulates);
        yield return RunContract("Jump reaches end and peaks", JumpReachesEndAndPeaks);
        yield return RunContract("Punch and PunchScale return to base", PunchAndPunchScaleReturnToBase);
        yield return RunContract("IgnoreTimeScale progresses at timeScale zero", IgnoreTimeScaleProgressesAtTimeScaleZero);
        yield return RunContract("SpeedBased move uses distance over speed", SpeedBasedMoveUsesDistanceOverSpeed);
        yield return RunContract("SpeedBased rotate uses angle over speed", SpeedBasedRotateUsesAngleOverSpeed);
        yield return RunContract("Sequence speed based uses future start state", SequenceSpeedBasedUsesFutureStartState);
        yield return RunContract("Sequence join speed based uses in-flight start state", SequenceJoinSpeedBasedUsesInFlightStartState);
        yield return RunContract("Unsupported speed based punch is ignored", UnsupportedSpeedBasedPunchIsIgnored);
        yield return RunContract("Unsupported speed based punch scale is ignored", UnsupportedSpeedBasedPunchScaleIsIgnored);
        yield return RunContract("Sequence append join interval behave", SequenceAppendJoinIntervalBehave);
        yield return RunContract("Sequence locks after infinite tween", SequenceLocksAfterInfiniteTween);
        yield return RunContract("Null and destroyed target invalidate handles", NullAndDestroyedTargetInvalidateHandles);
        yield return RunContract("Ease smoke test reaches final values", EaseSmokeTestReachesFinalValues);
        yield return RunContract("Float yoyo reverses during playback", FloatYoyoReversesDuringPlayback);
        yield return RunContract("Float incremental accumulates during playback", FloatIncrementalAccumulatesDuringPlayback);
        yield return RunContract("Infinite loop stays alive until killed", InfiniteLoopStaysAliveUntilKilled);

        if (!abortRequested && includeObservationProbes)
        {
            yield return RunObservation("Probe speed based punch", ProbeSpeedBasedPunch);
            yield return RunObservation("Probe speed based punch scale", ProbeSpeedBasedPunchScale);
            yield return RunObservation("Probe very short elastic ease", ProbeVeryShortElasticEase);
            yield return RunObservation("Probe very short bounce ease", ProbeVeryShortBounceEase);
            yield return RunObservation("Probe sequence plus speed based capture", ProbeSequencePlusSpeedBasedCapture);
            if (includeAllocationProbes)
            {
                yield return RunObservation("Probe move tween GC alloc", ProbeMoveTweenGcAlloc);
                yield return RunObservation("Probe callback tween GC alloc", ProbeCallbackTweenGcAlloc);
            }
        }

        Time.timeScale = 1f;
        BuildSummary();

        if (!keepSpawnedObjectsAfterRun)
            yield return CleanupRoot();

        Debug.Log(contractFailed > 0
            ? "[TinyTweenTest] Suite finished with failures."
            : "[TinyTweenTest] Suite finished without contract failures.");

        runRoutine = null;
    }

    IEnumerator RunContract(string name, Func<IEnumerator> body)
    {
        if (abortRequested)
            yield break;

        scenarioName = name;
        scenarioFailed = false;
        Debug.Log("[TinyTweenTest] Running: " + name);

        yield return body();

        Time.timeScale = 1f;
        yield return WaitFrames(2);

        if (scenarioFailed)
        {
            contractFailed++;
            failures.Add(name);
            Debug.LogError("[TinyTweenTest] FAILED: " + name);
            if (stopOnFirstFailure)
                abortRequested = true;
        }
        else
        {
            contractPassed++;
            if (verbosePassLogs)
                Debug.Log("[TinyTweenTest] PASSED: " + name);
        }
    }

    IEnumerator RunObservation(string name, Func<IEnumerator> body)
    {
        if (abortRequested)
            yield break;

        scenarioName = name;
        scenarioFailed = false;
        Debug.Log("[TinyTweenTest] Observation: " + name);

        yield return body();

        Time.timeScale = 1f;
        yield return WaitFrames(2);
    }

    IEnumerator MoveToReachesWorldEnd()
    {
        Transform target = MakeTarget("MoveTo");
        Vector3 end = target.position + new Vector3(1.25f, -0.35f, 0.8f);
        TinyTweenHandle handle = target.TMove(end, 0.18f).SetEase(TinyEaseType.OutQuad);

        yield return WaitInvalid(handle, 0.7f);

        Check(!handle.IsValid, "MoveTo handle should be invalid after completion.");
        CheckVec(target.position, end, positionTolerance, "MoveTo final position mismatch.");
    }

    IEnumerator MoveByAppliesWorldOffset()
    {
        Transform target = MakeTarget("MoveBy");
        Vector3 start = target.position;
        Vector3 amount = new Vector3(-0.45f, 0.5f, 0.9f);
        TinyTweenHandle handle = target.TMoveBy(amount, 0.18f).SetEase(TinyEaseType.Linear);

        yield return WaitInvalid(handle, 0.7f);

        CheckVec(target.position, start + amount, positionTolerance, "MoveBy should add the requested world offset.");
    }

    IEnumerator LocalMoveRespectsParentSpace()
    {
        Transform parent = MakeTarget("LocalParent");
        parent.rotation = Quaternion.Euler(0f, 37f, 0f);

        Transform child = MakeChild(parent, "LocalChild");
        child.localPosition = new Vector3(0.8f, 0.25f, -0.35f);
        Vector3 localEnd = new Vector3(1.6f, 0.9f, -0.75f);
        TinyTweenHandle handle = child.TLocalMove(localEnd, 0.2f).SetEase(TinyEaseType.Linear);

        yield return WaitInvalid(handle, 0.8f);

        CheckVec(child.localPosition, localEnd, positionTolerance, "LocalMove should finish in local space.");
        CheckVec(child.position, parent.TransformPoint(localEnd), positionTolerance, "LocalMove world position should match parent transform.");
    }

    IEnumerator RotateToReachesTargetRotation()
    {
        Transform target = MakeTarget("RotateTo");
        Quaternion end = Quaternion.Euler(15f, 120f, -25f);
        TinyTweenHandle handle = target.TRotate(end, 0.2f).SetEase(TinyEaseType.InOutSine);

        yield return WaitInvalid(handle, 0.8f);

        CheckRot(target.rotation, end, rotationTolerance, "RotateTo final rotation mismatch.");
    }

    IEnumerator RotateByAppliesLocalDelta()
    {
        Transform parent = MakeTarget("RotateByParent");
        parent.rotation = Quaternion.Euler(0f, 25f, 0f);

        Transform child = MakeChild(parent, "RotateByChild");
        child.localRotation = Quaternion.Euler(5f, 10f, 0f);
        Quaternion expected = Quaternion.Euler(5f, 90f, 0f);
        TinyTweenHandle handle = child.TLocalRotateBy(new Vector3(0f, 80f, 0f), 0.2f).SetEase(TinyEaseType.Linear);

        yield return WaitInvalid(handle, 0.8f);

        CheckRot(child.localRotation, expected, rotationTolerance, "RotateBy should apply a local relative rotation.");
    }

    IEnumerator FloatUpdatesAndCompletes()
    {
        float setterValue = float.MinValue;
        float updateValue = float.MinValue;
        int updateCount = 0;
        int completeCount = 0;

        TinyTweenHandle handle = TinyTweener.Float(2f, 5f, 0.16f, value => setterValue = value)
            .SetEase(TinyEaseType.Linear)
            .OnUpdate(value =>
            {
                updateValue = value;
                updateCount++;
            })
            .OnComplete(() => completeCount++);

        yield return WaitInvalid(handle, 0.7f);

        CheckFloat(setterValue, 5f, floatTolerance, "Float setter should finish at target value.");
        CheckFloat(updateValue, 5f, floatTolerance, "Float OnUpdate should report final value.");
        Check(updateCount > 0, "Float OnUpdate should run at least once.");
        Check(completeCount == 1, "Float OnComplete should run exactly once.");
    }

    IEnumerator DelayBlocksStart()
    {
        Transform target = MakeTarget("Delay");
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(0.9f, 0f, 0f);
        TinyTweenHandle handle = target.TMove(end, 0.12f).SetEase(TinyEaseType.Linear).SetDelay(0.14f);

        yield return WaitRealtime(0.08f);
        CheckVec(target.position, start, idleTolerance, "Tween should not move before delay expires.");

        yield return WaitInvalid(handle, 0.8f);
        CheckVec(target.position, end, positionTolerance, "Delayed tween should still reach end value.");
    }

    IEnumerator KillStopsWithoutCompleteCallback()
    {
        Transform target = MakeTarget("Kill");
        Vector3 end = target.position + new Vector3(1.4f, 0f, 0f);
        bool completeCalled = false;
        TinyTweenHandle handle = target.TMove(end, 0.3f).SetEase(TinyEaseType.Linear).OnComplete(() => completeCalled = true);

        yield return WaitRealtime(0.1f);

        Vector3 killedAt = target.position;
        handle.Kill();

        Check(!handle.IsValid, "Kill should invalidate the handle immediately.");
        yield return WaitFrames(2);

        Check(!completeCalled, "Kill should not invoke OnComplete.");
        CheckVec(target.position, killedAt, positionTolerance, "Kill should freeze at current value.");
        Check(Vector3.Distance(target.position, end) > 0.1f, "Kill should not snap to the end value.");
    }

    IEnumerator CompleteSnapsImmediately()
    {
        Transform target = MakeTarget("Complete");
        Vector3 end = target.position + new Vector3(0f, 0.75f, 0.5f);
        int completeCount = 0;
        TinyTweenHandle handle = target.TMove(end, 0.35f).SetEase(TinyEaseType.Linear).OnComplete(() => completeCount++);

        yield return WaitFrames(1);
        handle.Complete();

        Check(!handle.IsValid, "Complete should invalidate the handle immediately.");
        Check(completeCount == 1, "Complete should invoke OnComplete exactly once.");
        CheckVec(target.position, end, positionTolerance, "Complete should snap to end value.");
    }

    IEnumerator ZeroDurationCompletes()
    {
        Transform target = MakeTarget("ZeroDuration");
        Vector3 end = target.position + new Vector3(0.5f, 0.3f, -0.4f);
        int completeCount = 0;
        TinyTweenHandle handle = target.TMove(end, 0f).OnComplete(() => completeCount++);

        yield return WaitFrames(2);

        Check(!handle.IsValid, "Zero duration tween should complete immediately.");
        Check(completeCount == 1, "Zero duration tween should still trigger OnComplete.");
        CheckVec(target.position, end, positionTolerance, "Zero duration tween should apply end value.");
    }

    IEnumerator YoyoEvenEndsAtStart()
    {
        Transform target = MakeTarget("YoyoEven");
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(0.8f, 0f, 0f);
        TinyTweenHandle handle = target.TMove(end, 0.08f).SetEase(TinyEaseType.Linear).SetLoops(2, TinyLoopType.Yoyo);

        yield return WaitInvalid(handle, 0.6f);

        CheckVec(target.position, start, positionTolerance, "Even yoyo loops should end at start.");
    }

    IEnumerator IncrementalMoveAccumulates()
    {
        Transform target = MakeTarget("Incremental");
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(0.4f, 0.3f, 0.2f);
        Vector3 expected = start + (end - start) * 3f;
        TinyTweenHandle handle = target.TMove(end, 0.09f).SetEase(TinyEaseType.Linear).SetLoops(3, TinyLoopType.Incremental);

        yield return WaitInvalid(handle, 0.7f);

        CheckVec(target.position, expected, positionTolerance, "Incremental loops should accumulate delta.");
    }

    IEnumerator JumpReachesEndAndPeaks()
    {
        Transform target = MakeTarget("Jump");
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(1f, 0f, 0.4f);
        float peakY = start.y;
        TinyTweenHandle handle = target.TJump(end, 0.22f, 1.1f, 2).SetEase(TinyEaseType.Linear);

        float t0 = Time.realtimeSinceStartup;
        while (handle.IsValid && Time.realtimeSinceStartup - t0 <= 0.8f)
        {
            peakY = Mathf.Max(peakY, target.position.y);
            yield return null;
        }

        Check(!handle.IsValid, "Jump should finish within timeout.");
        CheckVec(target.position, end, positionTolerance, "Jump should land on end position.");
        Check(peakY > Mathf.Max(start.y, end.y) + 0.35f, "Jump should rise above the baseline.");
    }

    IEnumerator PunchAndPunchScaleReturnToBase()
    {
        Transform punchTarget = MakeTarget("Punch");
        Vector3 punchStart = punchTarget.position;
        float maxPunchDistance = 0f;
        TinyTweenHandle punchHandle = punchTarget.TPunch(new Vector3(0.9f, 0f, 0f), 0.22f, 4).SetEase(TinyEaseType.Linear);

        float t0 = Time.realtimeSinceStartup;
        while (punchHandle.IsValid && Time.realtimeSinceStartup - t0 <= 0.8f)
        {
            maxPunchDistance = Mathf.Max(maxPunchDistance, Vector3.Distance(punchStart, punchTarget.position));
            yield return null;
        }

        Check(!punchHandle.IsValid, "Punch should complete within timeout.");
        CheckVec(punchTarget.position, punchStart, positionTolerance, "Punch should return to start position.");
        Check(maxPunchDistance > 0.15f, "Punch should move away from the start during playback.");

        Transform scaleTarget = MakeTarget("PunchScale");
        scaleTarget.localScale = Vector3.one;
        Vector3 scaleStart = scaleTarget.localScale;
        float maxScaleDelta = 0f;
        TinyTweenHandle scaleHandle = scaleTarget.TPunchScale(new Vector3(0.35f, 0.2f, 0.1f), 0.22f, 4).SetEase(TinyEaseType.Linear);

        t0 = Time.realtimeSinceStartup;
        while (scaleHandle.IsValid && Time.realtimeSinceStartup - t0 <= 0.8f)
        {
            maxScaleDelta = Mathf.Max(maxScaleDelta, Vector3.Distance(scaleStart, scaleTarget.localScale));
            yield return null;
        }

        Check(!scaleHandle.IsValid, "PunchScale should complete within timeout.");
        CheckVec(scaleTarget.localScale, scaleStart, positionTolerance, "PunchScale should return to base scale.");
        Check(maxScaleDelta > 0.05f, "PunchScale should visibly change scale during playback.");
    }

    IEnumerator IgnoreTimeScaleProgressesAtTimeScaleZero()
    {
        Transform scaledTarget = MakeTarget("ScaledTime");
        Transform unscaledTarget = MakeTarget("UnscaledTime");

        Vector3 scaledStart = scaledTarget.position;
        Vector3 unscaledStart = unscaledTarget.position;
        Vector3 scaledEnd = scaledStart + new Vector3(1f, 0f, 0f);
        Vector3 unscaledEnd = unscaledStart + new Vector3(0f, 1f, 0f);

        TinyTweenHandle scaledHandle = scaledTarget.TMove(scaledEnd, 0.16f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle unscaledHandle = unscaledTarget.TMove(unscaledEnd, 0.16f).SetEase(TinyEaseType.Linear).SetIgnoreTimeScale(true);

        Time.timeScale = 0f;
        yield return WaitRealtime(0.24f);
        Time.timeScale = 1f;

        CheckVec(scaledTarget.position, scaledStart, idleTolerance, "Scaled tween should not move while timeScale is zero.");
        CheckVec(unscaledTarget.position, unscaledEnd, positionTolerance, "IgnoreTimeScale tween should finish using unscaled time.");
        Check(!unscaledHandle.IsValid, "IgnoreTimeScale tween should complete while timeScale is zero.");

        scaledHandle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator SpeedBasedMoveUsesDistanceOverSpeed()
    {
        Transform target = MakeTarget("SpeedMove");
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(0f, 0f, 1.2f);
        float speed = 3f;
        float expectedDuration = Vector3.Distance(start, end) / speed;
        float t0 = Time.realtimeSinceStartup;

        TinyTweenHandle handle = target.TMove(end, speed).SetEase(TinyEaseType.Linear).SetSpeedBased();

        yield return WaitInvalid(handle, 1f);

        float elapsed = Time.realtimeSinceStartup - t0;
        CheckVec(target.position, end, positionTolerance, "SpeedBased move should reach end position.");
        CheckFloat(elapsed, expectedDuration, timeTolerance, "SpeedBased move duration should be distance / speed.");
    }

    IEnumerator SpeedBasedRotateUsesAngleOverSpeed()
    {
        Transform target = MakeTarget("SpeedRotate");
        Quaternion end = Quaternion.Euler(0f, 90f, 0f);
        float degreesPerSecond = 180f;
        float expectedDuration = 90f / degreesPerSecond;
        float t0 = Time.realtimeSinceStartup;

        TinyTweenHandle handle = target.TRotate(end, degreesPerSecond).SetEase(TinyEaseType.Linear).SetSpeedBased();

        yield return WaitInvalid(handle, 1.2f);

        float elapsed = Time.realtimeSinceStartup - t0;
        CheckRot(target.rotation, end, rotationTolerance, "SpeedBased rotate should reach target rotation.");
        CheckFloat(elapsed, expectedDuration, timeTolerance, "SpeedBased rotate duration should be angle / speed.");
    }

    IEnumerator UnsupportedSpeedBasedPunchIsIgnored()
    {
        Transform target = MakeTarget("UnsupportedSpeedPunch");
        Vector3 start = target.position;
        TinyTweenHandle handle = target.TPunch(new Vector3(0.8f, 0f, 0f), 3f, 3).SetSpeedBased();

        yield return WaitRealtime(0.4f);

        Check(handle.IsValid, "Unsupported SetSpeedBased on Punch should leave the tween alive.");
        Check(Vector3.Distance(start, target.position) > 0.05f, "Punch should still animate using its original duration.");

        handle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator UnsupportedSpeedBasedPunchScaleIsIgnored()
    {
        Transform target = MakeTarget("UnsupportedSpeedPunchScale");
        Vector3 start = target.localScale;
        TinyTweenHandle handle = target.TPunchScale(new Vector3(0.4f, 0.2f, 0.1f), 3f, 3).SetSpeedBased();

        yield return WaitRealtime(0.4f);

        Check(handle.IsValid, "Unsupported SetSpeedBased on PunchScale should leave the tween alive.");
        Check(Vector3.Distance(start, target.localScale) > 0.03f, "PunchScale should still animate using its original duration.");

        handle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator SequenceSpeedBasedUsesFutureStartState()
    {
        Transform target = MakeTarget("SeqSpeedTarget");
        Transform marker = MakeTarget("SeqSpeedMarker");
        Vector3 targetStart = target.position;
        Vector3 markerStart = marker.position;

        TinyTweenHandle first = target.TMove(targetStart + new Vector3(1f, 0f, 0f), 0.12f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle second = target.TMove(targetStart + new Vector3(2f, 0f, 0f), 4f).SetEase(TinyEaseType.Linear).SetSpeedBased();
        TinyTweenHandle markerTween = marker.TMoveBy(new Vector3(0f, 1f, 0f), 0.05f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(first).Append(second).Append(markerTween);

        yield return WaitRealtime(0.46f);
        Check(marker.position.y > markerStart.y + 0.5f, "A tween appended after a speed-based step should start using the step's future start state.");

        yield return WaitInvalid(markerTween, 0.5f);
        CheckVec(target.position, targetStart + new Vector3(2f, 0f, 0f), positionTolerance, "Sequence speed-based tween should still end at its target.");
        CheckVec(marker.position, markerStart + new Vector3(0f, 1f, 0f), positionTolerance, "Sequence tween after a speed-based step should finish on time.");
    }

    IEnumerator SequenceJoinSpeedBasedUsesInFlightStartState()
    {
        Transform target = MakeTarget("SeqJoinSpeedTarget");
        Transform marker = MakeTarget("SeqJoinSpeedMarker");
        Vector3 targetStart = target.position;
        Vector3 markerStart = marker.position;

        TinyTweenHandle first = target.TMove(targetStart + new Vector3(1f, 0f, 0f), 0.2f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle second = target.TMove(targetStart + new Vector3(2f, 0f, 0f), 4f).SetEase(TinyEaseType.Linear).SetSpeedBased().SetDelay(0.1f);
        TinyTweenHandle markerTween = marker.TMoveBy(new Vector3(0f, 1f, 0f), 0.05f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(first).Join(second).Append(markerTween);

        yield return WaitRealtime(0.53f);
        Check(marker.position.y > markerStart.y + 0.5f, "A joined speed-based tween should use the target's in-flight sequence state when scheduling later steps.");

        yield return WaitInvalid(markerTween, 0.5f);
        CheckVec(target.position, targetStart + new Vector3(2f, 0f, 0f), positionTolerance, "Joined sequence speed-based tween should still end at its target.");
        CheckVec(marker.position, markerStart + new Vector3(0f, 1f, 0f), positionTolerance, "Tween appended after a joined speed-based step should finish on time.");
    }

    IEnumerator SequenceAppendJoinIntervalBehave()
    {
        Transform appendTarget = MakeTarget("SeqAppend");
        Vector3 appendStart = appendTarget.position;
        TinyTweenHandle appendA = appendTarget.TMoveBy(new Vector3(1f, 0f, 0f), 0.12f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle appendB = appendTarget.TMoveBy(new Vector3(0f, 1f, 0f), 0.12f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(appendA).Append(appendB);

        yield return WaitRealtime(0.08f);
        Check(appendTarget.position.x > appendStart.x + 0.2f, "First appended tween should be active first.");
        CheckFloat(appendTarget.position.y, appendStart.y, idleTolerance, "Second appended tween should not have started yet.");

        yield return WaitInvalid(appendB, 0.7f);
        CheckVec(appendTarget.position, appendStart + new Vector3(1f, 1f, 0f), positionTolerance, "Append should run steps serially.");

        Transform joinA = MakeTarget("SeqJoinA");
        Transform joinB = MakeTarget("SeqJoinB");
        Vector3 joinAStart = joinA.position;
        Vector3 joinBStart = joinB.position;
        TinyTweenHandle joinHandleA = joinA.TMoveBy(new Vector3(1f, 0f, 0f), 0.12f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle joinHandleB = joinB.TMoveBy(new Vector3(0f, 1f, 0f), 0.12f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(joinHandleA).Join(joinHandleB);

        yield return WaitRealtime(0.07f);
        Check(joinA.position.x > joinAStart.x + 0.15f, "Joined tween A should have started.");
        Check(joinB.position.y > joinBStart.y + 0.15f, "Joined tween B should have started.");

        yield return WaitInvalid(joinHandleB, 0.7f);
        CheckVec(joinA.position, joinAStart + new Vector3(1f, 0f, 0f), positionTolerance, "Join tween A final position mismatch.");
        CheckVec(joinB.position, joinBStart + new Vector3(0f, 1f, 0f), positionTolerance, "Join tween B final position mismatch.");

        Transform intervalTarget = MakeTarget("SeqInterval");
        Vector3 intervalStart = intervalTarget.position;
        TinyTweenHandle intervalA = intervalTarget.TMoveBy(new Vector3(1f, 0f, 0f), 0.08f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle intervalB = intervalTarget.TMoveBy(new Vector3(0f, 1f, 0f), 0.08f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(intervalA).AppendInterval(0.1f).Append(intervalB);

        yield return WaitRealtime(0.12f);
        CheckFloat(intervalTarget.position.x, intervalStart.x + 1f, 0.08f, "First interval step should be done before second starts.");
        CheckFloat(intervalTarget.position.y, intervalStart.y, idleTolerance, "Second interval step should wait for the interval.");

        yield return WaitInvalid(intervalB, 0.8f);
        CheckVec(intervalTarget.position, intervalStart + new Vector3(1f, 1f, 0f), positionTolerance, "AppendInterval should delay the following tween.");
    }

    IEnumerator SequenceLocksAfterInfiniteTween()
    {
        Transform loopTarget = MakeTarget("SeqInfiniteA");
        Transform blockedTarget = MakeTarget("SeqInfiniteB");
        Vector3 blockedStart = blockedTarget.position;

        TinyTweenHandle loopHandle = loopTarget.TMoveBy(new Vector3(0.6f, 0f, 0f), 0.08f).SetEase(TinyEaseType.Linear).SetLoops(-1, TinyLoopType.Yoyo);
        TinyTweenHandle blockedHandle = blockedTarget.TMoveBy(new Vector3(0f, 0.6f, 0f), 0.08f).SetEase(TinyEaseType.Linear);

        TinyTweener.Sequence().Append(loopHandle).Append(blockedHandle);

        Check(!blockedHandle.IsValid, "Tweens appended after an infinite step should be killed immediately.");
        yield return WaitRealtime(0.18f);

        Check(loopHandle.IsValid, "Infinite tween should remain alive.");
        CheckVec(blockedTarget.position, blockedStart, idleTolerance, "Blocked tween should never move.");

        loopHandle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator NullAndDestroyedTargetInvalidateHandles()
    {
        TinyTweenHandle nullHandle = TinyTweener.MoveTo(null, Vector3.one, 0.1f);
        Check(!nullHandle.IsValid, "Null targets should return invalid handles.");

        Transform target = MakeTarget("DestroyedTarget");
        TinyTweenHandle liveHandle = target.TMoveBy(new Vector3(1f, 0f, 0f), 0.25f).SetEase(TinyEaseType.Linear);

        yield return WaitRealtime(0.08f);
        Destroy(target.gameObject);
        yield return WaitFrames(2);

        Check(!liveHandle.IsValid, "Destroying target should invalidate handle on next runner update.");
    }

    IEnumerator EaseSmokeTestReachesFinalValues()
    {
        Array eases = Enum.GetValues(typeof(TinyEaseType));
        for (int i = 0; i < eases.Length; i++)
        {
            TinyEaseType ease = (TinyEaseType)eases.GetValue(i);
            Transform target = MakeTarget("Ease_" + ease);
            Vector3 start = target.position;
            Vector3 end = start + new Vector3(0.4f, 0.25f, -0.3f);
            TinyTweenHandle handle = target.TMove(end, 0.14f).SetEase(ease);

            float t0 = Time.realtimeSinceStartup;
            while (handle.IsValid && Time.realtimeSinceStartup - t0 <= 0.7f)
            {
                Check(IsFinite(target.position), "Ease " + ease + " produced a non-finite position.");
                yield return null;
            }

            Check(!handle.IsValid, "Ease " + ease + " should complete within timeout.");
            CheckVec(target.position, end, positionTolerance, "Ease " + ease + " should finish at end value.");

            if (abortRequested)
                yield break;
        }
    }

    IEnumerator FloatYoyoReversesDuringPlayback()
    {
        float current = 1f;
        TinyTweenHandle handle = TinyTweener.Float(1f, 3f, 0.12f, value => current = value)
            .SetEase(TinyEaseType.Linear)
            .SetLoops(2, TinyLoopType.Yoyo);

        yield return WaitRealtime(0.15f);
        Check(current > 2.1f, "Float yoyo should be travelling back from its peak during the second loop.");

        yield return WaitInvalid(handle, 0.6f);
        CheckFloat(current, 1f, floatTolerance, "Float yoyo should end at start value after an even loop count.");
    }

    IEnumerator FloatIncrementalAccumulatesDuringPlayback()
    {
        float current = 1f;
        TinyTweenHandle handle = TinyTweener.Float(1f, 2f, 0.1f, value => current = value)
            .SetEase(TinyEaseType.Linear)
            .SetLoops(3, TinyLoopType.Incremental);

        yield return WaitRealtime(0.15f);
        Check(current > 2.2f, "Float incremental loops should continue from the previous loop end value.");

        yield return WaitInvalid(handle, 0.7f);
        CheckFloat(current, 4f, floatTolerance, "Float incremental loops should end at the accumulated value.");
    }

    IEnumerator InfiniteLoopStaysAliveUntilKilled()
    {
        Transform target = MakeTarget("InfiniteLoop");
        Vector3 start = target.position;
        float maxDistance = 0f;
        TinyTweenHandle handle = target.TMoveBy(new Vector3(0.7f, 0f, 0f), 0.08f).SetEase(TinyEaseType.Linear).SetLoops(-1, TinyLoopType.Yoyo);

        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 <= 0.25f)
        {
            maxDistance = Mathf.Max(maxDistance, Vector3.Distance(start, target.position));
            yield return null;
        }

        Check(handle.IsValid, "Infinite loop should remain valid until killed.");
        Check(maxDistance > 0.15f, "Infinite loop should keep animating while alive.");

        handle.Kill();
        yield return WaitFrames(2);

        Check(!handle.IsValid, "Killed infinite loop should invalidate its handle.");
    }

    IEnumerator ProbeSpeedBasedPunch()
    {
        Transform target = MakeTarget("ProbeSpeedPunch");
        Vector3 start = target.position;
        float t0 = Time.realtimeSinceStartup;
        TinyTweenHandle handle = target.TPunch(new Vector3(0.8f, 0f, 0f), 3f, 3).SetSpeedBased();

        yield return WaitRealtime(0.4f);

        Observe("SpeedBased Punch elapsed=" + (Time.realtimeSinceStartup - t0).ToString("0.000") +
                "s finalOffset=" + Vector3.Distance(start, target.position).ToString("0.000") +
                " valid=" + handle.IsValid);

        handle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator ProbeSpeedBasedPunchScale()
    {
        Transform target = MakeTarget("ProbeSpeedPunchScale");
        Vector3 start = target.localScale;
        float t0 = Time.realtimeSinceStartup;
        TinyTweenHandle handle = target.TPunchScale(new Vector3(0.4f, 0.2f, 0.1f), 3f, 3).SetSpeedBased();

        yield return WaitRealtime(0.4f);

        Observe("SpeedBased PunchScale elapsed=" + (Time.realtimeSinceStartup - t0).ToString("0.000") +
                "s finalScaleDelta=" + Vector3.Distance(start, target.localScale).ToString("0.000") +
                " valid=" + handle.IsValid);

        handle.Kill();
        yield return WaitFrames(2);
    }

    IEnumerator ProbeVeryShortElasticEase()
    {
        Transform target = MakeTarget("ProbeElastic");
        Vector3 end = target.position + new Vector3(0.75f, 0f, 0f);
        float maxOvershoot = 0f;
        TinyTweenHandle handle = target.TMove(end, 0.016f).SetEase(TinyEaseType.OutElastic);

        float t0 = Time.realtimeSinceStartup;
        while (handle.IsValid && Time.realtimeSinceStartup - t0 <= 0.3f)
        {
            maxOvershoot = Mathf.Max(maxOvershoot, target.position.x - end.x);
            yield return null;
        }

        Observe("Very short OutElastic overshoot=" + maxOvershoot.ToString("0.000") +
                " finalError=" + Vector3.Distance(end, target.position).ToString("0.000"));
    }

    IEnumerator ProbeVeryShortBounceEase()
    {
        Transform target = MakeTarget("ProbeBounce");
        Vector3 end = target.position + new Vector3(0f, 0.75f, 0f);
        float maxUndershoot = 0f;
        TinyTweenHandle handle = target.TMove(end, 0.016f).SetEase(TinyEaseType.OutBounce);

        float t0 = Time.realtimeSinceStartup;
        while (handle.IsValid && Time.realtimeSinceStartup - t0 <= 0.3f)
        {
            maxUndershoot = Mathf.Min(maxUndershoot, target.position.y - end.y);
            yield return null;
        }

        Observe("Very short OutBounce undershoot=" + maxUndershoot.ToString("0.000") +
                " finalError=" + Vector3.Distance(end, target.position).ToString("0.000"));
    }

    IEnumerator ProbeSequencePlusSpeedBasedCapture()
    {
        Transform target = MakeTarget("ProbeSeqSpeed");
        Vector3 start = target.position;
        TinyTweenHandle first = target.TMove(start + new Vector3(1f, 0f, 0f), 0.12f).SetEase(TinyEaseType.Linear);
        TinyTweenHandle second = target.TMove(start + new Vector3(2f, 0f, 0f), 4f).SetEase(TinyEaseType.Linear).SetSpeedBased();

        TinyTweener.Sequence().Append(first).Append(second);

        yield return WaitRealtime(0.18f);
        Vector3 midSample = target.position;
        yield return WaitInvalid(second, 1.2f);

        Observe("Sequence+SpeedBased midX=" + midSample.x.ToString("0.000") +
                " finalX=" + target.position.x.ToString("0.000") +
                " expectedEndX=" + (start.x + 2f).ToString("0.000"));
    }

    IEnumerator ProbeMoveTweenGcAlloc()
    {
        Transform target = MakeTarget("ProbeMoveGc");
        yield return WaitFrames(2);

        using (ProfilerRecorder recorder = StartGcAllocRecorder())
        {
            if (!recorder.Valid)
            {
                Observe("GC Alloc recorder not available on this runtime.");
                yield break;
            }

            TinyTweenHandle handle = target.TMoveBy(new Vector3(2f, 0f, 0f), 0.35f).SetEase(TinyEaseType.Linear);
            yield return MeasureGcAlloc(recorder, handle, "MoveTween");
        }
    }

    IEnumerator ProbeCallbackTweenGcAlloc()
    {
        Transform target = MakeTarget("ProbeCallbackGc");
        float sink = 0f;
        yield return WaitFrames(2);

        using (ProfilerRecorder recorder = StartGcAllocRecorder())
        {
            if (!recorder.Valid)
            {
                Observe("GC Alloc recorder not available on this runtime.");
                yield break;
            }

                TinyTweenHandle handle = TinyTweener.Float(0f, 1f, 0.35f, value => sink = value)
                .SetEase(TinyEaseType.Linear)
                .OnUpdate(value => sink = value)
                .OnComplete(() => sink = 2f);

            yield return MeasureGcAlloc(recorder, handle, "CallbackFloat");
        }
    }

    void ResetSuite()
    {
        abortRequested = false;
        scenarioFailed = false;
        scenarioName = string.Empty;
        contractPassed = 0;
        contractFailed = 0;
        observationCount = 0;
        spawnIndex = 0;
        failures.Clear();
        observations.Clear();
        lastSummary = string.Empty;
        Time.timeScale = 1f;
    }

    void EnsureRoot()
    {
        if (suiteRoot != null)
            return;

        suiteRoot = new GameObject("[TinyTweenScenarioRunnerRoot]");
        suiteRoot.hideFlags = HideFlags.DontSave;
    }

    IEnumerator CleanupRoot()
    {
        if (suiteRoot == null)
            yield break;

        Destroy(suiteRoot);
        suiteRoot = null;
        spawnIndex = 0;
        yield return null;
    }

    Transform MakeTarget(string label)
    {
        EnsureRoot();

        GameObject go = new GameObject(label);
        go.transform.SetParent(suiteRoot.transform, false);
        go.transform.position = NextSpawnPoint();
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    Transform MakeChild(Transform parent, string label)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    Vector3 NextSpawnPoint()
    {
        int column = spawnIndex % 4;
        int row = spawnIndex / 4;
        spawnIndex++;
        return spawnOrigin + new Vector3(column * scenarioSpacing, row * scenarioSpacing, 0f);
    }

    IEnumerator WaitInvalid(TinyTweenHandle handle, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (handle.IsValid && Time.realtimeSinceStartup - t0 <= timeout)
            yield return null;
    }

    IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        while (Time.realtimeSinceStartup < end)
            yield return null;
    }

    IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            yield return null;
    }

    void Check(bool condition, string message)
    {
        if (condition)
            return;

        scenarioFailed = true;
        Debug.LogError("[TinyTweenTest] " + scenarioName + ": " + message);
    }

    void CheckFloat(float actual, float expected, float tolerance, string message)
    {
        if (Mathf.Abs(actual - expected) <= tolerance)
            return;

        scenarioFailed = true;
        Debug.LogError("[TinyTweenTest] " + scenarioName + ": " + message +
                       " Expected=" + expected.ToString("0.000") +
                       " Actual=" + actual.ToString("0.000") +
                       " Tol=" + tolerance.ToString("0.000"));
    }

    void CheckVec(Vector3 actual, Vector3 expected, float tolerance, string message)
    {
        if (Vector3.Distance(actual, expected) <= tolerance)
            return;

        scenarioFailed = true;
        Debug.LogError("[TinyTweenTest] " + scenarioName + ": " + message +
                       " Expected=" + expected.ToString("F3") +
                       " Actual=" + actual.ToString("F3") +
                       " Tol=" + tolerance.ToString("0.000"));
    }

    void CheckRot(Quaternion actual, Quaternion expected, float tolerance, string message)
    {
        float angle = Quaternion.Angle(actual, expected);
        if (angle <= tolerance)
            return;

        scenarioFailed = true;
        Debug.LogError("[TinyTweenTest] " + scenarioName + ": " + message +
                       " Angle=" + angle.ToString("0.000") +
                       " Tol=" + tolerance.ToString("0.000"));
    }

    void Observe(string message)
    {
        observationCount++;
        observations.Add(message);
        Debug.LogWarning("[TinyTweenTest] " + scenarioName + ": " + message);
    }

    void BuildSummary()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("TinyTween Scenario Suite");
        sb.AppendLine("Contracts Passed: " + contractPassed);
        sb.AppendLine("Contracts Failed: " + contractFailed);
        sb.AppendLine("Observations: " + observationCount);

        if (failures.Count > 0)
        {
            sb.AppendLine("Failed Scenarios:");
            for (int i = 0; i < failures.Count; i++)
                sb.AppendLine("- " + failures[i]);
        }

        if (observations.Count > 0)
        {
            sb.AppendLine("Observation Notes:");
            for (int i = 0; i < observations.Count; i++)
                sb.AppendLine("- " + observations[i]);
        }

        lastSummary = sb.ToString();
        Debug.Log("[TinyTweenTest] Summary\n" + lastSummary);
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static ProfilerRecorder StartGcAllocRecorder()
    {
        return ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    IEnumerator MeasureGcAlloc(ProfilerRecorder recorder, TinyTweenHandle handle, string label)
    {
        const int BaselineFrames = 24;
        const int PostFrames = 12;

        GcStats baseline = new GcStats();
        GcStats postBaseline = new GcStats();
        GcStats active = new GcStats();
        long startupBytes = 0L;

        yield return SampleRecorderFrames(recorder, BaselineFrames, baseline);

        float t0 = Time.realtimeSinceStartup;
        bool startupCaptured = false;

        while (handle.IsValid && Time.realtimeSinceStartup - t0 <= 1.2f)
        {
            yield return null;

            long bytes = recorder.LastValue;

            if (!startupCaptured)
            {
                startupBytes = bytes;
                startupCaptured = true;
                continue;
            }

            active.Add(bytes);
        }

        yield return SampleRecorderFrames(recorder, PostFrames, postBaseline);
        baseline.Merge(postBaseline);

        long baselineAvg = baseline.AverageBytes;
        long activeAvg = active.AverageBytes;
        long deltaAvg = Math.Max(0L, activeAvg - baselineAvg);
        long deltaPeak = Math.Max(0L, active.PeakBytes - baseline.PeakBytes);

        Observe(label +
                " idleAvg=" + baselineAvg +
                " activeAvg=" + activeAvg +
                " deltaAvg=" + deltaAvg +
                " startup=" + startupBytes +
                " idlePeak=" + baseline.PeakBytes +
                " activePeak=" + active.PeakBytes +
                " deltaPeak=" + deltaPeak +
                " activeFrames=" + active.Frames);
    }

    IEnumerator SampleRecorderFrames(ProfilerRecorder recorder, int frameCount, GcStats stats)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
            stats.Add(recorder.LastValue);
        }
    }
}
