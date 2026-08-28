#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>
/// 실제 IK 생성 / 삭제를 담당하는 Editor 전용 Builder.
/// 아티스트는 이 클래스를 직접 사용할 필요가 없다.
/// </summary>
public static class Standard2DIKBuilder
{
    public static bool Validate(
        Standard2DRigIKSetup setup,
        bool logSuccess = true)
    {
        if (!Standard2DRigResolver.TryResolve(
                setup.RigSearchRoot,
                out Standard2DRigResolver.Result rig,
                out List<string> errors))
        {
            Debug.LogError(
                $"[{nameof(Standard2DRigIKSetup)} v{Standard2DRigIKSetup.ToolVersion}] " +
                $"Rig Invalid: '{setup.name}'\n- " +
                string.Join(
                    "\n- ",
                    errors),
                setup);

            return false;
        }

        if (logSuccess)
        {
            string aliases =
                rig.Aliases.Count > 0
                    ? "\nPSB aliases: " +
                      string.Join(
                          ", ",
                          rig.Aliases)
                    : string.Empty;

            Debug.Log(
                $"[{nameof(Standard2DRigIKSetup)} v{Standard2DRigIKSetup.ToolVersion}] " +
                $"Rig Valid: '{setup.name}'\n" +
                $"Skeleton Root: {Standard2DRigResolver.GetPath(rig.SkeletonRoot)}" +
                aliases,
                setup);
        }

        return true;
    }

    public static bool BuildOrRebuild(
        Standard2DRigIKSetup setup)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "Play Mode에서는 IK 자동 생성을 실행하지 않습니다.",
                setup);

            return false;
        }

        if (!Standard2DRigResolver.TryResolve(
                setup.RigSearchRoot,
                out Standard2DRigResolver.Result rig,
                out List<string> errors))
        {
            Debug.LogError(
                $"[{nameof(Standard2DRigIKSetup)} v{Standard2DRigIKSetup.ToolVersion}] " +
                $"IK 생성 중단: '{setup.name}'\n- " +
                string.Join(
                    "\n- ",
                    errors),
                setup);

            return false;
        }

        Undo.SetCurrentGroupName(
            "Build Standard 2D IK");

        int undoGroup =
            Undo.GetCurrentGroup();

        IKManager2D manager =
            EnsureManager(setup);

        CleanupGenerated(
            setup,
            rig);

        ConfigureManager(
            manager,
            setup);

        LimbSolver2D leftHandSolver = null;
        LimbSolver2D rightHandSolver = null;

        foreach (Standard2DRigDefinition.LimbSpec spec
                 in Standard2DRigDefinition.LimbSpecs)
        {
            LimbSolver2D solver = CreateLimb(
                setup,
                rig,
                manager,
                spec);

            if (spec.Prefix == "arm_l")
            {
                leftHandSolver = solver;
            }
            else if (spec.Prefix == "arm_r")
            {
                rightHandSolver = solver;
            }
        }

        CreateCcd(
            setup,
            rig,
            manager,
            Standard2DRigDefinition.BodyAimCcd);

        ConfigurePlayerWeaponRig(
            setup,
            leftHandSolver,
            rightHandSolver);

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(setup);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            manager);

        Undo.CollapseUndoOperations(
            undoGroup);

        string aliases =
            rig.Aliases.Count > 0
                ? "\nPSB aliases: " +
                  string.Join(
                      ", ",
                      rig.Aliases)
                : string.Empty;

        Debug.Log(
            $"[{nameof(Standard2DRigIKSetup)} v{Standard2DRigIKSetup.ToolVersion}] " +
            $"'{setup.name}' IK 생성 완료\n" +
            "Player Root: IKManager2D\n" +
            "Player Root 직계 자식: LimbSolver2D x6 (Arm 2 / Leg 2 / Foot 2) + CCDSolver2D x1 (Body Aim)\n" +
            "각 Solver 직계 자식: Target x1\n" +
            "Skeleton: Effector x7\n" +
            "Manager/Solver/Target/Effector 참조 연결 완료" +
            aliases,
            setup);

        return true;
    }

    public static void RemoveGenerated(
        Standard2DRigIKSetup setup)
    {
        Standard2DRigResolver.TryResolve(
            setup.RigSearchRoot,
            out Standard2DRigResolver.Result rig,
            out _);

        Undo.SetCurrentGroupName(
            "Remove Standard 2D IK");

        int undoGroup =
            Undo.GetCurrentGroup();

        CleanupGenerated(
            setup,
            rig);

        Undo.CollapseUndoOperations(
            undoGroup);

        Debug.Log(
            $"[{nameof(Standard2DRigIKSetup)} v{Standard2DRigIKSetup.ToolVersion}] " +
            $"자동 생성 IK 제거 완료: '{setup.name}'",
            setup);
    }

    private static IKManager2D EnsureManager(
        Standard2DRigIKSetup setup)
    {
        IKManager2D manager =
            setup.GetComponent<IKManager2D>();

        if (manager != null)
            return manager;

        return
            Undo.AddComponent<IKManager2D>(
                setup.gameObject);
    }

    private static void ConfigureManager(
        IKManager2D manager,
        Standard2DRigIKSetup setup)
    {
        Undo.RecordObject(
            manager,
            "Configure IK Manager 2D");

        manager.weight =
            setup.ManagerWeight;

        manager.alwaysUpdate =
            setup.AlwaysUpdate;
    }

    private static LimbSolver2D CreateLimb(
        Standard2DRigIKSetup setup,
        Standard2DRigResolver.Result rig,
        IKManager2D manager,
        Standard2DRigDefinition.LimbSpec spec)
    {
        Transform chainRoot =
            rig.Bones[spec.ChainRoot];

        Transform effectorParent =
            rig.Bones[spec.EffectorParent];

        // -------------------------------------------------------------
        // 1) Effector
        // -------------------------------------------------------------
        //
        // 팔: forearm 자식
        // 다리: calf 자식
        // 발: foot_02 자식
        // 머리: head 자식
        //
        // 손/발의 위치를 그대로 쓰는 게 아니라
        // EffectorParent의 Local +X(right) 방향으로
        // 실제 segment 길이보다 조금 더 바깥에 둔다.
        //
        // 따라서:
        // arm -> forearm -> effector
        // thigh -> calf -> effector
        // foot_01 -> foot_02 -> effector
        // neck -> head -> effector
        //
        // transformCount = 3에서 어깨/허벅지까지 정확히 포함된다.
        GameObject effectorObject =
            new(spec.EffectorName);

        Undo.RegisterCreatedObjectUndo(
            effectorObject,
            $"Create {spec.EffectorName}");

        Transform effector =
            effectorObject.transform;

        effector.SetParent(
            effectorParent,
            false);

        float localReach =
            CalculateLocalReach(
                rig,
                spec.TipReference,
                spec.PreviousBone,
                effectorParent);

        effector.localPosition =
            Vector3.right *
            localReach *
            setup.GetEffectorReachScale(
                spec.ReachGroup);

        effector.localRotation =
            Quaternion.identity;

        effector.localScale =
            Vector3.one;

        AddMarker(
            effectorObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Effector);

        // -------------------------------------------------------------
        // 2) Solver
        // -------------------------------------------------------------
        //
        // 요청한 구조대로 Player Root 바로 아래.
        GameObject solverObject =
            new(spec.SolverName);

        Undo.RegisterCreatedObjectUndo(
            solverObject,
            $"Create {spec.SolverName}");

        Transform solverTransform =
            solverObject.transform;

        solverTransform.SetParent(
            setup.PlayerRoot,
            false);

        solverTransform.localPosition =
            Vector3.zero;

        solverTransform.localRotation =
            Quaternion.identity;

        solverTransform.localScale =
            Vector3.one;

        LimbSolver2D solver =
            Undo.AddComponent<LimbSolver2D>(
                solverObject);

        Undo.RecordObject(
            solver,
            $"Configure {spec.SolverName}");

        solver.weight = 1f;

        solver.constrainRotation =
            setup.ConstrainRotation;

        solver.solveFromDefaultPose =
            setup.SolveFromDefaultPose;

        solver.flip =
            spec.Flip;

        AddMarker(
            solverObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Solver);

        // -------------------------------------------------------------
        // 3) Target
        // -------------------------------------------------------------
        //
        // Solver의 바로 아래.
        // World Position/Rotation은 Effector와 정확히 동일하게 시작.
        GameObject targetObject =
            new(spec.TargetName);

        Undo.RegisterCreatedObjectUndo(
            targetObject,
            $"Create {spec.TargetName}");

        Transform target =
            targetObject.transform;

        target.SetParent(
            solverTransform,
            false);

        target.position =
            effector.position;

        target.rotation =
            effector.rotation;

        target.localScale =
            Vector3.one;

        AddMarker(
            targetObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Target);

        // -------------------------------------------------------------
        // 4) Solver Chain Reference
        // -------------------------------------------------------------
        IKChain2D chain =
            solver.GetChain(0);

        chain.effector =
            effector;

        chain.target =
            target;

        chain.transformCount = 3;

        // Effector parent hierarchy를 기준으로
        // chain transforms를 실제로 계산/초기화.
        solver.Initialize();

        // Initialize 이후에도 참조가 유지되는지 명시적으로 다시 보장.
        chain =
            solver.GetChain(0);

        chain.effector =
            effector;

        chain.target =
            target;

        chain.transformCount = 3;

        // -------------------------------------------------------------
        // 5) Manager Reference
        // -------------------------------------------------------------
        manager.AddSolver(
            solver);

        EditorUtility.SetDirty(
            solver);

        EditorUtility.SetDirty(
            manager);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            solver);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            manager);

        // 안전 검증.
        IKChain2D verifyChain =
            solver.GetChain(0);

        if (verifyChain.effector != effector ||
            verifyChain.target != target ||
            verifyChain.transformCount != 3)
        {
            Debug.LogError(
                $"[{nameof(Standard2DIKBuilder)}] " +
                $"{spec.SolverName} 참조 연결 검증 실패.",
                solver);
        }

        return solver;
    }

    private static void CreateCcd(
        Standard2DRigIKSetup setup,
        Standard2DRigResolver.Result rig,
        IKManager2D manager,
        Standard2DRigDefinition.CcdSpec spec)
    {
        Transform effectorParent =
            rig.Bones[spec.EffectorParent];

        GameObject effectorObject =
            new(spec.EffectorName);

        Undo.RegisterCreatedObjectUndo(
            effectorObject,
            $"Create {spec.EffectorName}");

        Transform effector =
            effectorObject.transform;

        effector.SetParent(
            effectorParent,
            false);

        float localReach =
            CalculateLocalReach(
                rig,
                null,
                spec.PreviousBone,
                effectorParent);

        effector.localPosition =
            Vector3.right *
            localReach *
            setup.GetEffectorReachScale(
                spec.ReachGroup);

        effector.localRotation =
            Quaternion.identity;

        effector.localScale =
            Vector3.one;

        AddMarker(
            effectorObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Effector);

        GameObject solverObject =
            new(spec.SolverName);

        Undo.RegisterCreatedObjectUndo(
            solverObject,
            $"Create {spec.SolverName}");

        Transform solverTransform =
            solverObject.transform;

        solverTransform.SetParent(
            setup.PlayerRoot,
            false);

        solverTransform.localPosition =
            Vector3.zero;

        solverTransform.localRotation =
            Quaternion.identity;

        solverTransform.localScale =
            Vector3.one;

        CCDSolver2D solver =
            Undo.AddComponent<CCDSolver2D>(
                solverObject);

        Undo.RecordObject(
            solver,
            $"Configure {spec.SolverName}");

        solver.weight = 1f;

        // 편집 중에는 클립 미리보기를 건드리지 않고,
        // 런타임 PlayerAim의 ProceduralAim에서만 켭니다.
        solver.enabled = false;
        solver.constrainRotation = false;
        solver.solveFromDefaultPose = true;
        solver.iterations = setup.CcdIterations;
        solver.tolerance = setup.CcdTolerance;
        solver.velocity = setup.CcdVelocity;

        AddMarker(
            solverObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Solver);

        GameObject targetObject =
            new(spec.TargetName);

        Undo.RegisterCreatedObjectUndo(
            targetObject,
            $"Create {spec.TargetName}");

        Transform target =
            targetObject.transform;

        target.SetParent(
            solverTransform,
            false);

        target.position =
            effector.position;

        target.rotation =
            effector.rotation;

        target.localScale =
            Vector3.one;

        AddMarker(
            targetObject,
            setup,
            Standard2DGeneratedIKMarker.GeneratedKind.Target);

        IKChain2D chain =
            solver.GetChain(0);

        chain.effector = effector;
        chain.target = target;
        chain.transformCount = spec.TransformCount;

        solver.Initialize();

        chain =
            solver.GetChain(0);

        chain.effector = effector;
        chain.target = target;
        chain.transformCount = spec.TransformCount;

        manager.AddSolver(
            solver);

        EditorUtility.SetDirty(
            solver);

        EditorUtility.SetDirty(
            manager);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            solver);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            manager);

        PlayerAim playerAim =
            setup.GetComponent<PlayerAim>();

        if (playerAim != null)
        {
            Undo.RecordObject(
                playerAim,
                "Connect Body Aim CCD");

            playerAim.ConfigureUpperBodyAimRig(
                target,
                solver);

            EditorUtility.SetDirty(
                playerAim);

            PrefabUtility.RecordPrefabInstancePropertyModifications(
                playerAim);
        }

        IKChain2D verifyChain =
            solver.GetChain(0);

        if (verifyChain.effector != effector ||
            verifyChain.target != target ||
            verifyChain.transformCount != spec.TransformCount ||
            verifyChain.rootTransform != rig.Bones[spec.ChainRoot])
        {
            Debug.LogError(
                $"[{nameof(Standard2DIKBuilder)}] " +
                $"{spec.SolverName} CCD 참조 연결 검증 실패.",
                solver);
        }
    }


    private static void ConfigurePlayerWeaponRig(
        Standard2DRigIKSetup setup,
        LimbSolver2D leftHandSolver,
        LimbSolver2D rightHandSolver)
    {
        PlayerWeaponController weaponController =
            setup.GetComponent<PlayerWeaponController>();

        if (weaponController == null)
            return;

        PlayerAim playerAim =
            setup.GetComponent<PlayerAim>();

        Undo.RecordObject(
            weaponController,
            "Connect Player Weapon Rig");

        weaponController.ConfigureWeaponPresentationRig(
            playerAim != null
                ? playerAim.ResolvedAimPivot
                : null,
            leftHandSolver,
            rightHandSolver);

        EditorUtility.SetDirty(
            weaponController);

        PrefabUtility.RecordPrefabInstancePropertyModifications(
            weaponController);
    }

    /// <summary>
    /// EffectorParent의 local +X 방향으로 사용할 실제 길이.
    ///
    /// arm:
    /// forearm -> hand 거리를 forearm local space에서 측정.
    ///
    /// leg:
    /// calf -> foot_01 거리를 calf local space에서 측정.
    ///
    /// foot:
    /// terminal foot_02의 다음 Bone이 없으므로
    /// foot_02 기준으로 foot_01까지의 local-space 거리를 사용.
    ///
    /// head:
    /// terminal head의 다음 Bone이 없으므로
    /// head 기준으로 neck까지의 local-space 거리를 사용.
    /// </summary>
    private static float CalculateLocalReach(
        Standard2DRigResolver.Result rig,
        string tipReference,
        string previousBone,
        Transform effectorParent)
    {
        const float minimumReach = 0.001f;

        if (!string.IsNullOrEmpty(
                tipReference))
        {
            Transform tip =
                rig.Bones[tipReference];

            Vector3 tipLocal =
                effectorParent.InverseTransformPoint(
                    tip.position);

            return
                Mathf.Max(
                    tipLocal.magnitude,
                    minimumReach);
        }

        Transform previous =
            rig.Bones[previousBone];

        Vector3 previousLocal =
            effectorParent.InverseTransformPoint(
                previous.position);

        return
            Mathf.Max(
                previousLocal.magnitude,
                minimumReach);
    }

    private static void AddMarker(
        GameObject target,
        Standard2DRigIKSetup setup,
        Standard2DGeneratedIKMarker.GeneratedKind kind)
    {
        Standard2DGeneratedIKMarker marker =
            Undo.AddComponent<Standard2DGeneratedIKMarker>(
                target);

        marker.Initialize(
            setup.PlayerRoot,
            kind);

        EditorUtility.SetDirty(
            marker);
    }

    private static void CleanupGenerated(
    Standard2DRigIKSetup setup,
    Standard2DRigResolver.Result rig)
    {
        Standard2DGeneratedIKMarker[] markers =
            setup.GetComponentsInChildren<Standard2DGeneratedIKMarker>(
                true);

        List<Standard2DGeneratedIKMarker> ownedMarkers =
            markers
                .Where(
                    marker =>
                        marker != null &&
                        marker.OwnerRoot == setup.PlayerRoot)
                .ToList();

        IKManager2D manager =
            setup.GetComponent<IKManager2D>();

        if (manager != null)
        {
            Undo.RecordObject(
                manager,
                "Clear IK Solvers");

            manager.solvers.Clear();

            EditorUtility.SetDirty(
                manager);
        }

        ownedMarkers.Sort(
            (a, b) =>
                GetDepth(b.transform)
                    .CompareTo(
                        GetDepth(a.transform)));

        HashSet<GameObject> destroyed =
            new();

        foreach (Standard2DGeneratedIKMarker marker
                 in ownedMarkers)
        {
            if (marker == null)
                continue;

            GameObject go =
                marker.gameObject;

            if (go == null ||
                destroyed.Contains(go))
            {
                continue;
            }

            destroyed.Add(go);

            Undo.DestroyObjectImmediate(
                go);
        }

        CleanupLegacyGenerated(
            setup,
            rig);
    }

    private static void CleanupLegacyGenerated(
        Standard2DRigIKSetup setup,
        Standard2DRigResolver.Result rig)
    {
        // 예전 __ik_generated wrapper 제거.
        Transform legacyRoot =
            setup.PlayerRoot.Find(
                "__ik_generated");

        if (legacyRoot != null)
        {
            RemoveSolversFromAllManagers(
                setup,
                legacyRoot.GetComponentsInChildren<Solver2D>(
                    true));

            Undo.DestroyObjectImmediate(
                legacyRoot.gameObject);
        }

        // 이전 버전에서 Skeleton root 아래에 만들었을 수도 있으므로
        // 이름 + LimbSolver2D 조합으로 우리 Solver만 찾아 제거.
        HashSet<string> solverNames =
            Standard2DRigDefinition.LimbSpecs
                .Select(
                    spec => spec.SolverName)
                .ToHashSet();

        solverNames.Add(
            Standard2DRigDefinition
                .BodyAimCcd
                .SolverName);

        Solver2D[] allSolvers =
            setup.GetComponentsInChildren<Solver2D>(
                true);

        foreach (Solver2D solver
                 in allSolvers)
        {
            if (solver == null ||
                !solverNames.Contains(
                    solver.gameObject.name))
            {
                continue;
            }

            RemoveSolversFromAllManagers(
                setup,
                new Solver2D[] { solver });

            Undo.DestroyObjectImmediate(
                solver.gameObject);
        }

        if (rig == null)
            return;

        // 이전 Effector 이름 제거.
        foreach (Standard2DRigDefinition.LimbSpec spec
                 in Standard2DRigDefinition.LimbSpecs)
        {
            if (!rig.Bones.TryGetValue(
                    spec.EffectorParent,
                    out Transform parent))
            {
                continue;
            }

            Transform effector =
                FindDirectChildExact(
                    parent,
                    spec.EffectorName);

            if (effector != null)
            {
                Undo.DestroyObjectImmediate(
                    effector.gameObject);
            }
        }

        Standard2DRigDefinition.CcdSpec ccdSpec =
            Standard2DRigDefinition.BodyAimCcd;

        if (rig.Bones.TryGetValue(
                ccdSpec.EffectorParent,
                out Transform ccdEffectorParent))
        {
            Transform ccdEffector =
                FindDirectChildExact(
                    ccdEffectorParent,
                    ccdSpec.EffectorName);

            if (ccdEffector != null)
            {
                Undo.DestroyObjectImmediate(
                    ccdEffector.gameObject);
            }
        }
    }

    private static void RemoveSolversFromAllManagers(
        Standard2DRigIKSetup setup,
        IEnumerable<Solver2D> solvers)
    {
        IKManager2D[] managers =
            setup.GetComponentsInChildren<IKManager2D>(
                true);

        foreach (Solver2D solver
                 in solvers)
        {
            if (solver == null)
                continue;

            foreach (IKManager2D manager
                     in managers)
            {
                if (manager == null)
                    continue;

                Undo.RecordObject(
                    manager,
                    "Remove Legacy Solver");

                manager.RemoveSolver(
                    solver);
            }
        }
    }

    private static int GetDepth(
        Transform target)
    {
        int depth = 0;

        Transform current =
            target;

        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static Transform FindDirectChildExact(
        Transform parent,
        string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform child =
                parent.GetChild(i);

            if (child.name ==
                childName)
            {
                return child;
            }
        }

        return null;
    }
}

#endif
