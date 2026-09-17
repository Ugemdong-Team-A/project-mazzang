#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.IK;
using Object = UnityEngine.Object;

internal sealed class WeaponAnimationAuthoringSession : IDisposable
{
    private readonly AnimationWindowPin _pin = new();
    private readonly List<HandBinding> _hands = new();

    private GameObject _poseObject;
    private Animator _poseAnimator;
    private AnimatorController _controller;
    private AnimatorStateMachine _stateMachine;
    private AnimatorState _state;
    private Animator _character;
    private AnimationWindow _window;
    private AnimationClip _characterClip;
    private AnimationClip _weaponClip;
    private bool _handsApplied;

    public bool IsActive =>
        _poseObject != null;

    public GameObject PoseObject =>
        _poseObject;

    public HeldWeaponView View
    {
        get;
        private set;
    }

    public string SelectionNotice
    {
        get;
        private set;
    }

    public string EndReason
    {
        get;
        private set;
    }

    public bool TryStart(
        Animator character,
        HeldWeaponView template,
        AnimationClip characterClip,
        AnimationClip weaponClip,
        ActionHandIkPolicy handPolicy,
        AnimationWindow window,
        out string error)
    {
        Dispose();
        EndReason = null;
        SelectionNotice = null;
        error = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            character == null ||
            EditorUtility.IsPersistent(character) ||
            !character.gameObject.scene.IsValid())
        {
            error = "씬 또는 프리팹 편집 모드의 캐릭터가 필요합니다.";
            return false;
        }

        if (template == null)
        {
            error = "무기의 장착 모습 원본이 없습니다.";
            return false;
        }

        if (characterClip == null ||
            weaponClip == null)
        {
            error = "비교할 캐릭터 클립과 녹화할 무기 클립이 모두 필요합니다.";
            return false;
        }

        if (characterClip == weaponClip)
        {
            error = "캐릭터 클립과 무기 클립은 서로 다른 파일이어야 합니다.";
            return false;
        }

        if (window == null)
        {
            error = "Animation Window(Ctrl+6)을 열어주세요.";
            return false;
        }

        Standard2DAimAnchor[] anchors =
            character
                .GetComponentsInChildren<
                    Standard2DAimAnchor>(true)
                .Where(anchor => anchor.IsValid)
                .ToArray();

        if (anchors.Length != 1)
        {
            error =
                "유효한 RAP·WeaponSocket 구성이 하나 있어야 합니다. " +
                "Character Setup을 확인해주세요.";
            return false;
        }

        _character = character;
        _window = window;
        _characterClip = characterClip;
        _weaponClip = weaponClip;

        try
        {
            CreatePreview(
                anchors[0].WeaponSocket,
                template);
            CreateEditController();

            if (handPolicy ==
                ActionHandIkPolicy.WeaponGrips)
            {
                BindHand(
                    "arm_l_solver",
                    View.LeftHandGrip);
                BindHand(
                    "arm_r_solver",
                    View.RightHandGrip);
            }

            _pin.Pin(
                _window,
                _poseAnimator,
                _weaponClip);
            _window.previewing = true;

            if (!_window.previewing ||
                !AnimationMode.InAnimationMode())
            {
                throw new InvalidOperationException(
                    "Animation 창에서 무기 미리보기를 시작하지 못했습니다.");
            }

            ApplyHands();

            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
            EditorApplication.playModeStateChanged +=
                OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            SceneView.RepaintAll();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Dispose();
            return false;
        }
    }

    public void HandleSelection(
        GameObject selected)
    {
        if (!IsActive)
            return;

        bool isWeaponPart =
            selected != null &&
            (selected == _poseObject ||
             selected.transform.IsChildOf(
                 _poseObject.transform));

        if (isWeaponPart)
        {
            SelectionNotice = null;
            return;
        }

        if (_window != null &&
            _window.recording)
        {
            _window.recording = false;
        }

        SelectionNotice =
            "무기 작업 범위 밖을 선택해 녹화만 멈췄습니다. " +
            "편집 세션과 미리보기는 유지됩니다.";
    }

    public void Stop(string reason)
    {
        EndReason = reason;
        Dispose();
    }

    public void Dispose()
    {
        EditorApplication.update -= Update;
        AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
        EditorApplication.quitting -= Dispose;
        EditorApplication.playModeStateChanged -=
            OnPlayModeChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;

        RestoreHands(true);

        if (_window != null)
        {
            _window.recording = false;
            _window.previewing = false;
        }

        _pin.Dispose();

        if (_poseObject != null)
            Object.DestroyImmediate(_poseObject);

        if (_state != null)
            Object.DestroyImmediate(_state);

        if (_stateMachine != null)
            Object.DestroyImmediate(_stateMachine);

        if (_controller != null)
            Object.DestroyImmediate(_controller);

        _poseObject = null;
        _poseAnimator = null;
        _state = null;
        _stateMachine = null;
        _controller = null;
        _character = null;
        _window = null;
        _characterClip = null;
        _weaponClip = null;
        View = null;
        SelectionNotice = null;
        SceneView.RepaintAll();
    }

    private void CreatePreview(
        Transform socket,
        HeldWeaponView template)
    {
        if (socket == null)
            throw new InvalidOperationException("WeaponSocket이 없습니다.");

        if (socket.Find(WeaponPose.ObjectName) != null)
        {
            throw new InvalidOperationException(
                "WeaponSocket 아래에 이미 WeaponPose가 있습니다. " +
                "기존 무기 미리보기를 종료해주세요.");
        }

        _poseObject =
            new GameObject(WeaponPose.ObjectName)
            {
                hideFlags = HideFlags.DontSave
            };
        _poseObject.SetActive(false);
        _poseObject.transform.SetParent(
            socket,
            false);

        WeaponPose pose =
            _poseObject.AddComponent<WeaponPose>();
        _poseAnimator =
            _poseObject.AddComponent<Animator>();

        View = Object.Instantiate(
            template,
            _poseObject.transform,
            false);
        View.name = HeldWeaponView.RuntimeViewName;

        foreach (Animator animator
                 in View.GetComponentsInChildren<Animator>(true))
        {
            Object.DestroyImmediate(animator);
        }

        foreach (Animation animation
                 in View.GetComponentsInChildren<Animation>(true))
        {
            Object.DestroyImmediate(animation);
        }

        foreach (Rigidbody2D body
                 in View.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.simulated = false;
        }

        int sortingOrder =
            View.GetComponentInChildren<SpriteRenderer>(true)
                ?.sortingOrder ??
            0;
        View.Initialize(
            pose,
            sortingOrder);

        foreach (Transform child
                 in _poseObject
                     .GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.hideFlags =
                HideFlags.DontSave;
        }

        _poseObject.SetActive(true);
    }

    private void CreateEditController()
    {
        _controller =
            new AnimatorController
            {
                name = "Weapon Authoring Controller",
                hideFlags = HideFlags.HideAndDontSave
            };
        _controller.AddLayer("Weapon");

        _stateMachine =
            _controller.layers[0].stateMachine;
        _stateMachine.hideFlags =
            HideFlags.HideAndDontSave;
        _state =
            _stateMachine.AddState("Weapon Clip");
        _state.hideFlags =
            HideFlags.HideAndDontSave;
        _state.motion = _weaponClip;
        _stateMachine.defaultState = _state;
        _poseAnimator.runtimeAnimatorController =
            _controller;
    }

    private void Update()
    {
        if (_poseObject == null ||
            _character == null ||
            _window == null)
        {
            Stop(
                "작업 캐릭터·무기 또는 Animation 창이 사라져 편집을 종료했습니다.");
            return;
        }

        if (!_pin.IsPinned &&
            !_pin.TryRestore(
                Selection.activeObject,
                out string error))
        {
            Stop(error);
            return;
        }

        if (!_window.previewing)
        {
            RestoreHands(false);
            return;
        }

        float sampleTime =
            Mathf.Clamp(
                _window.time,
                0f,
                _characterClip.length);

        AnimationMode.BeginSampling();

        try
        {
            AnimationMode.SampleAnimationClip(
                _character.gameObject,
                _characterClip,
                sampleTime);
        }
        finally
        {
            AnimationMode.EndSampling();
        }

        ApplyHands();
        SceneView.RepaintAll();
    }

    private void BindHand(
        string solverName,
        Transform grip)
    {
        if (grip == null)
            return;

        LimbSolver2D[] solvers =
            _character
                .GetComponentsInChildren<LimbSolver2D>(true)
                .Where(
                    solver => string.Equals(
                        solver.name,
                        solverName,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (solvers.Length != 1)
        {
            throw new InvalidOperationException(
                $"'{solverName}' 손 IK를 하나만 찾을 수 있어야 합니다.");
        }

        LimbSolver2D solver = solvers[0];

        if (!solver.isValid)
            solver.Initialize();

        if (!solver.isValid)
        {
            throw new InvalidOperationException(
                $"'{solverName}' 손 IK 구성이 유효하지 않습니다.");
        }

        _hands.Add(
            new HandBinding(
                solver,
                grip));
    }

    private void ApplyHands()
    {
        foreach (HandBinding hand in _hands)
            hand.Apply();

        _handsApplied = _hands.Count > 0;
    }

    private void RestoreHands(bool clear)
    {
        if (!_handsApplied &&
            _hands.Count == 0)
        {
            return;
        }

        foreach (HandBinding hand in _hands)
            hand.Restore();

        if (clear)
            _hands.Clear();

        _handsApplied = false;
    }

    private void OnPlayModeChanged(
        PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            Dispose();
    }

    private void OnSceneSaving(
        Scene scene,
        string path)
    {
        if (_character != null &&
            _character.gameObject.scene == scene)
        {
            Stop(
                "씬 저장을 위해 무기 편집을 종료했습니다. 무기 클립의 키는 유지됩니다.");
        }
    }

    private void OnPrefabSaving(GameObject root)
    {
        if (_character != null &&
            _character.transform.IsChildOf(root.transform))
        {
            Stop(
                "프리팹 저장을 위해 무기 편집을 종료했습니다. 무기 클립의 키는 유지됩니다.");
        }
    }

    private sealed class HandBinding
    {
        private readonly IKChain2D _chain;
        private readonly Transform _originalTarget;
        private readonly Transform _grip;

        public HandBinding(
            LimbSolver2D solver,
            Transform grip)
        {
            _chain = solver.GetChain(0);
            _originalTarget = _chain.target;
            _grip = grip;
        }

        public void Apply()
        {
            if (_chain != null &&
                _grip != null)
            {
                _chain.target = _grip;
            }
        }

        public void Restore()
        {
            if (_chain != null &&
                _chain.target == _grip)
            {
                _chain.target = _originalTarget;
            }
        }
    }
}

#endif
