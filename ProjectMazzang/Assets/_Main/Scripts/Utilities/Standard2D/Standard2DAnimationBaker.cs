#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;

public static class Standard2DAnimationBaker
{
    public sealed class Result
    {
        public AnimationClip Clip { get; }

        public int FrameCount { get; }

        public int BoneCount { get; }

        public int TargetCount { get; }

        internal Result(
            AnimationClip clip,
            int frameCount,
            int boneCount,
            int targetCount)
        {
            Clip = clip;
            FrameCount = frameCount;
            BoneCount = boneCount;
            TargetCount = targetCount;
        }
    }

    private readonly struct LocalPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public LocalPose(Transform target)
        {
            Position = target.localPosition;
            Rotation = target.localRotation;
            Scale = target.localScale;
        }

        public void Apply(Transform target)
        {
            target.localPosition = Position;
            target.localRotation = Rotation;
            target.localScale = Scale;
        }
    }

    private sealed class TransformTrack
    {
        public Transform Target { get; }

        public string Path { get; }

        public List<float> Times { get; } = new();

        public List<Vector3> Positions { get; } = new();

        public List<Quaternion> Rotations { get; } = new();

        public List<Vector3> Scales { get; } = new();

        public TransformTrack(
            Transform target,
            Transform root)
        {
            Target = target;
            Path = AnimationUtility.CalculateTransformPath(
                target,
                root);
        }

        public void Capture(float time)
        {
            Quaternion rotation = Target.localRotation;

            if (Rotations.Count > 0 &&
                Quaternion.Dot(
                    Rotations[^1],
                    rotation) < 0f)
            {
                rotation = new Quaternion(
                    -rotation.x,
                    -rotation.y,
                    -rotation.z,
                    -rotation.w);
            }

            Times.Add(time);
            Positions.Add(Target.localPosition);
            Rotations.Add(rotation);
            Scales.Add(Target.localScale);
        }
    }

    public static bool TryBake(
        Animator sourceAnimator,
        AnimationClip sourceClip,
        out Result result,
        out string error)
    {
        result = null;
        error = null;

        if (sourceAnimator == null)
        {
            error = "캐릭터 기준 Animator가 없습니다.";
            return false;
        }

        if (sourceClip == null)
        {
            error = "베이크할 Animation Clip이 없습니다.";
            return false;
        }

        if (sourceClip.length <= 0f)
        {
            error = $"'{sourceClip.name}' 클립의 길이가 0입니다.";
            return false;
        }

        GameObject previewRoot = null;
        AnimationClip bakedClip = null;

        try
        {
            previewRoot = UnityEngine.Object.Instantiate(
                sourceAnimator.gameObject);
            previewRoot.name =
                sourceAnimator.gameObject.name + " (FK Bake Preview)";
            previewRoot.hideFlags = HideFlags.HideAndDontSave;

            Animator previewAnimator =
                previewRoot.GetComponent<Animator>();

            if (previewAnimator != null)
            {
                previewAnimator.Rebind();
                previewAnimator.WriteDefaultValues();
                previewAnimator.enabled = false;
            }

            if (!TryResolveRig(
                    previewRoot,
                    out Standard2DRigResolver.Result rig,
                    out IKManager2D manager,
                    out List<LimbSolver2D> limbSolvers,
                    out List<Transform> limbTargets,
                    out error))
            {
                return false;
            }

            List<Transform> bones =
                GetOrderedBones(rig);

            List<TransformTrack> boneTracks =
                CreateTracks(
                    bones,
                    previewRoot.transform);
            List<TransformTrack> targetTracks =
                CreateTracks(
                    limbTargets,
                    previewRoot.transform);

            Transform[] allTransforms =
                previewRoot.GetComponentsInChildren<Transform>(
                    true);
            LocalPose[] defaultPoses =
                allTransforms
                    .Select(transform => new LocalPose(transform))
                    .ToArray();

            float frameRate = Mathf.Max(
                1f,
                sourceClip.frameRate);
            int frameCount = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    sourceClip.length * frameRate));

            for (int frame = 0;
                 frame <= frameCount;
                 frame++)
            {
                RestoreDefaultPose(
                    allTransforms,
                    defaultPoses);

                float time = Mathf.Min(
                    frame / frameRate,
                    sourceClip.length);

                sourceClip.SampleAnimation(
                    previewRoot,
                    time);

                CaptureTracks(
                    targetTracks,
                    time);

                foreach (LimbSolver2D solver
                         in limbSolvers)
                {
                    solver.UpdateIK(
                        manager.weight);
                }

                CaptureTracks(
                    boneTracks,
                    time);
            }

            bakedClip =
                UnityEngine.Object.Instantiate(
                    sourceClip);
            bakedClip.name =
                sourceClip.name + "_FK";
            bakedClip.hideFlags = HideFlags.None;
            bakedClip.frameRate = frameRate;

            ReplaceTransformCurves(
                bakedClip,
                boneTracks.Concat(targetTracks));

            bakedClip.EnsureQuaternionContinuity();

            result = new Result(
                bakedClip,
                frameCount + 1,
                boneTracks.Count,
                targetTracks.Count);

            return true;
        }
        catch (Exception exception)
        {
            if (bakedClip != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    bakedClip);
            }

            error =
                "FK 베이크 도중 예외가 발생했습니다.\n" +
                exception.Message;
            return false;
        }
        finally
        {
            if (previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    previewRoot);
            }
        }
    }

    private static bool TryResolveRig(
        GameObject previewRoot,
        out Standard2DRigResolver.Result rig,
        out IKManager2D manager,
        out List<LimbSolver2D> limbSolvers,
        out List<Transform> limbTargets,
        out string error)
    {
        rig = null;
        manager = null;
        limbSolvers = new List<LimbSolver2D>();
        limbTargets = new List<Transform>();
        error = null;

        Standard2DRigIKSetup setup =
            previewRoot.GetComponent<Standard2DRigIKSetup>();

        if (setup == null)
        {
            error =
                "캐릭터 기준에 Standard 2D Rig IK Setup이 없습니다.";
            return false;
        }

        if (!Standard2DRigResolver.TryResolve(
                setup.RigSearchRoot,
                out rig,
                out List<string> rigErrors))
        {
            error =
                "표준 Skeleton을 찾지 못했습니다.\n- " +
                string.Join("\n- ", rigErrors);
            return false;
        }

        manager =
            previewRoot.GetComponent<IKManager2D>();

        if (manager == null)
        {
            error = "캐릭터 기준에 IK Manager 2D가 없습니다.";
            return false;
        }

        if (manager.weight <= 0f)
        {
            error = "IK Manager 2D의 Weight가 0이라 베이크할 수 없습니다.";
            return false;
        }

        foreach (Standard2DRigDefinition.LimbSpec spec
                 in Standard2DRigDefinition.LimbSpecs)
        {
            LimbSolver2D solver =
                manager.solvers
                    .OfType<LimbSolver2D>()
                    .FirstOrDefault(
                        item => item.name == spec.SolverName);

            IKChain2D chain =
                solver != null
                    ? solver.GetChain(0)
                    : null;

            if (solver == null ||
                chain == null ||
                chain.target == null)
            {
                error =
                    $"IK Manager 2D에서 '{spec.SolverName}'과 Target을 " +
                    "정상적으로 찾지 못했습니다. Character Setup을 먼저 새로고침해주세요.";
                return false;
            }

            if (solver.weight <= 0f)
            {
                error =
                    $"'{spec.SolverName}'의 Weight가 0이라 베이크할 수 없습니다.";
                return false;
            }

            limbSolvers.Add(solver);
            limbTargets.Add(chain.target);
        }

        return true;
    }

    private static List<Transform> GetOrderedBones(
        Standard2DRigResolver.Result rig)
    {
        List<Transform> bones = new();

        AddBone("root");

        foreach (Standard2DRigDefinition.BoneLink link
                 in Standard2DRigDefinition.RequiredHierarchy)
        {
            AddBone(link.Child);
        }

        return bones;

        void AddBone(string logicalName)
        {
            if (rig.Bones.TryGetValue(
                    logicalName,
                    out Transform bone) &&
                bone != null &&
                !bones.Contains(bone))
            {
                bones.Add(bone);
            }
        }
    }

    private static List<TransformTrack> CreateTracks(
        IEnumerable<Transform> transforms,
        Transform root)
    {
        return transforms
            .Where(transform => transform != null)
            .Distinct()
            .Select(transform => new TransformTrack(transform, root))
            .ToList();
    }

    private static void RestoreDefaultPose(
        IReadOnlyList<Transform> transforms,
        IReadOnlyList<LocalPose> poses)
    {
        for (int index = 0;
             index < transforms.Count;
             index++)
        {
            poses[index].Apply(
                transforms[index]);
        }
    }

    private static void CaptureTracks(
        IEnumerable<TransformTrack> tracks,
        float time)
    {
        foreach (TransformTrack track in tracks)
            track.Capture(time);
    }

    private static void ReplaceTransformCurves(
        AnimationClip clip,
        IEnumerable<TransformTrack> tracks)
    {
        TransformTrack[] trackArray =
            tracks.ToArray();
        HashSet<string> paths =
            trackArray
                .Select(track => track.Path)
                .ToHashSet(StringComparer.Ordinal);

        foreach (EditorCurveBinding binding
                 in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type == typeof(Transform) &&
                paths.Contains(binding.path))
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    null);
            }
        }

        List<EditorCurveBinding> bindings = new();
        List<AnimationCurve> curves = new();

        foreach (TransformTrack track in trackArray)
        {
            AddVector3Curves(
                bindings,
                curves,
                track,
                "m_LocalPosition",
                track.Positions);
            AddQuaternionCurves(
                bindings,
                curves,
                track,
                track.Rotations);
            AddVector3Curves(
                bindings,
                curves,
                track,
                "m_LocalScale",
                track.Scales);
        }

        AnimationUtility.SetEditorCurves(
            clip,
            bindings.ToArray(),
            curves.ToArray());
    }

    private static void AddVector3Curves(
        ICollection<EditorCurveBinding> bindings,
        ICollection<AnimationCurve> curves,
        TransformTrack track,
        string property,
        IReadOnlyList<Vector3> values)
    {
        AddCurve(
            bindings,
            curves,
            track,
            property + ".x",
            values.Select(value => value.x));
        AddCurve(
            bindings,
            curves,
            track,
            property + ".y",
            values.Select(value => value.y));
        AddCurve(
            bindings,
            curves,
            track,
            property + ".z",
            values.Select(value => value.z));
    }

    private static void AddQuaternionCurves(
        ICollection<EditorCurveBinding> bindings,
        ICollection<AnimationCurve> curves,
        TransformTrack track,
        IReadOnlyList<Quaternion> values)
    {
        AddCurve(
            bindings,
            curves,
            track,
            "m_LocalRotation.x",
            values.Select(value => value.x));
        AddCurve(
            bindings,
            curves,
            track,
            "m_LocalRotation.y",
            values.Select(value => value.y));
        AddCurve(
            bindings,
            curves,
            track,
            "m_LocalRotation.z",
            values.Select(value => value.z));
        AddCurve(
            bindings,
            curves,
            track,
            "m_LocalRotation.w",
            values.Select(value => value.w));
    }

    private static void AddCurve(
        ICollection<EditorCurveBinding> bindings,
        ICollection<AnimationCurve> curves,
        TransformTrack track,
        string property,
        IEnumerable<float> values)
    {
        Keyframe[] keys =
            track.Times
                .Zip(
                    values,
                    (time, value) => new Keyframe(time, value))
                .ToArray();
        AnimationCurve curve =
            new(keys);

        for (int index = 0;
             index < curve.length;
             index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(
                curve,
                index,
                AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(
                curve,
                index,
                AnimationUtility.TangentMode.Linear);
        }

        bindings.Add(
            EditorCurveBinding.FloatCurve(
                track.Path,
                typeof(Transform),
                property));
        curves.Add(curve);
    }
}

public static class Standard2DAnimationBakeAssets
{
    public static string GetDefaultDirectory(
        AnimationClip sourceClip)
    {
        string sourcePath =
            AssetDatabase.GetAssetPath(sourceClip);
        string directory =
            string.IsNullOrEmpty(sourcePath)
                ? "Assets"
                : Path.GetDirectoryName(sourcePath)
                    ?.Replace('\\', '/');

        return !string.IsNullOrEmpty(directory) &&
               (directory == "Assets" ||
                directory.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal)) &&
               AssetDatabase.IsValidFolder(directory)
            ? directory
            : "Assets";
    }

    public static bool TrySave(
        AnimationClip sourceClip,
        Standard2DAnimationBaker.Result bakeResult,
        string assetPath,
        out AnimationClip savedClip,
        out string error)
    {
        savedClip = null;
        error = null;

        if (bakeResult?.Clip == null)
        {
            error = "저장할 FK 베이크 결과가 없습니다.";
            return false;
        }

        string sourcePath =
            AssetDatabase.GetAssetPath(sourceClip);

        if (string.Equals(
                sourcePath,
                assetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            error = "원본 Animation Clip 위에는 저장할 수 없습니다.";
            return false;
        }

        try
        {
            UnityEngine.Object existingAsset =
                AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (existingAsset != null &&
                existingAsset is not AnimationClip)
            {
                error = "선택한 경로에 Animation Clip이 아닌 에셋이 있습니다.";
                return false;
            }

            string clipName =
                Path.GetFileNameWithoutExtension(assetPath);

            if (existingAsset is AnimationClip existingClip)
            {
                Undo.RegisterCompleteObjectUndo(
                    existingClip,
                    "Rebake FK Animation");
                EditorUtility.CopySerialized(
                    bakeResult.Clip,
                    existingClip);
                existingClip.name = clipName;
                EditorUtility.SetDirty(existingClip);
                UnityEngine.Object.DestroyImmediate(
                    bakeResult.Clip);
                savedClip = existingClip;
            }
            else
            {
                bakeResult.Clip.name = clipName;
                AssetDatabase.CreateAsset(
                    bakeResult.Clip,
                    assetPath);
                savedClip = bakeResult.Clip;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate);

            return true;
        }
        catch (Exception exception)
        {
            error =
                "FK Animation Clip 저장 중 예외가 발생했습니다.\n" +
                exception.Message;
            return false;
        }
    }
}

#endif
