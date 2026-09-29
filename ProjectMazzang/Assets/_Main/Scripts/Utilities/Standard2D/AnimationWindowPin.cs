#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal sealed class AnimationWindowPin : IDisposable
{
    private const BindingFlags Flags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private static readonly FieldInfo LockField =
        typeof(AnimationWindow).GetField(
            "m_LockTracker",
            Flags);

    private static readonly PropertyInfo StateProperty =
        typeof(AnimationWindow).GetProperty(
            "state",
            Flags);

    private static readonly MethodInfo SelectionChanged =
        typeof(AnimationWindow).GetMethod(
            "OnSelectionChange",
            Flags);

    private AnimationWindow _window;
    private object _tracker;
    private PropertyInfo _lockedProperty;
    private bool _wasLocked;
    private Animator _root;
    private AnimationClip _clip;
    private bool _restoring;

    public bool IsPinned =>
        IsLocked &&
        ActiveRoot == _root?.gameObject &&
        _window.animationClip == _clip;

    private bool IsLocked =>
        _window != null &&
        _lockedProperty != null &&
        (bool)_lockedProperty.GetValue(_tracker);

    public void Pin(
        AnimationWindow window,
        Animator root,
        AnimationClip clip)
    {
        Dispose();

        _tracker = LockField?.GetValue(window);
        _lockedProperty = _tracker?
            .GetType()
            .GetProperty(
                "isLocked",
                Flags);

        if (_lockedProperty == null ||
            StateProperty == null ||
            SelectionChanged == null)
        {
            throw new InvalidOperationException(
                "현재 Unity 버전에서는 Animation 창 고정을 지원하지 않습니다.");
        }

        _window = window;
        _root = root;
        _clip = clip;
        _wasLocked =
            (bool)_lockedProperty.GetValue(_tracker);

        if (!TryRestore(
                root.gameObject,
                out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryRestore(
        UnityEngine.Object selectionToKeep,
        out string error)
    {
        error = null;

        if (_restoring)
            return true;

        if (_window == null ||
            _root == null ||
            _clip == null ||
            _lockedProperty == null)
        {
            error = "Animation 창의 무기 편집 기준이 사라졌습니다.";
            return false;
        }

        _restoring = true;
        bool wasPreviewing = _window.previewing;
        bool wasRecording = _window.recording;

        try
        {
            _lockedProperty.SetValue(
                _tracker,
                false);
            Selection.activeGameObject =
                _root.gameObject;
            SelectionChanged.Invoke(
                _window,
                null);

            if (ActiveRoot != _root.gameObject)
            {
                error = "Animation 창에 무기 편집 기준을 연결하지 못했습니다.";
                return false;
            }

            _window.animationClip = _clip;

            if (_window.animationClip != _clip)
            {
                error = "Animation 창에 무기 클립을 표시하지 못했습니다.";
                return false;
            }

            _lockedProperty.SetValue(
                _tracker,
                true);

            if (selectionToKeep != null)
                Selection.activeObject = selectionToKeep;

            if (wasPreviewing || wasRecording)
                _window.previewing = true;

            if (wasRecording && _window.canRecord)
                _window.recording = true;

            _window.Repaint();
            return IsPinned;
        }
        finally
        {
            _restoring = false;
        }
    }

    public void Dispose()
    {
        if (_window != null &&
            _lockedProperty != null)
        {
            _lockedProperty.SetValue(
                _tracker,
                _wasLocked);
            _window.Repaint();
        }

        _window = null;
        _tracker = null;
        _lockedProperty = null;
        _root = null;
        _clip = null;
        _restoring = false;
    }

    private GameObject ActiveRoot
    {
        get
        {
            object state =
                _window != null
                    ? StateProperty?.GetValue(_window)
                    : null;

            return state?
                .GetType()
                .GetProperty(
                    "activeRootGameObject",
                    Flags)?
                .GetValue(state) as GameObject;
        }
    }
}

#endif
