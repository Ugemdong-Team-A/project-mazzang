#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// 적합한 캐릭터를 선택한 상태에서 Animation Window에 포커스가 오면
/// Sprite Visual Keyer를 함께 연다. Unity 기본 Ctrl+6 단축키는 변경하지 않는다.
/// </summary>
[InitializeOnLoad]
public static class SpriteVisualKeyingWindowCompanion
{
    private static bool _wasAnimationFocused;
    private static bool _suppressUntilAnimationLosesFocus;

    static SpriteVisualKeyingWindowCompanion()
    {
        EditorApplication.update += DetectAnimationWindowFocus;
    }

    public static void SuppressUntilAnimationLosesFocus()
    {
        _suppressUntilAnimationLosesFocus = true;
    }

    private static void DetectAnimationWindowFocus()
    {
        bool animationFocused =
            EditorWindow.focusedWindow is AnimationWindow;

        if (!animationFocused)
        {
            _wasAnimationFocused = false;

            if (!(EditorWindow.focusedWindow is SpriteVisualKeyingWindow))
                _suppressUntilAnimationLosesFocus = false;

            return;
        }

        if (_wasAnimationFocused)
            return;

        _wasAnimationFocused = true;

        if (_suppressUntilAnimationLosesFocus ||
            !HasSuitableSelection() ||
            IsKeyingWindowOpen())
        {
            return;
        }

        SpriteVisualKeyingWindow.ShowWindow();
    }

    private static bool HasSuitableSelection()
    {
        GameObject selected =
            Selection.activeGameObject;

        if (selected == null)
            return false;

        Standard2DCharacterSetup setup =
            selected.GetComponentInParent<Standard2DCharacterSetup>();

        if (setup == null)
        {
            setup =
                selected.GetComponentInChildren<Standard2DCharacterSetup>(
                    true);
        }

        if (setup != null)
            return true;

        Animator animator =
            selected.GetComponentInParent<Animator>();

        if (animator == null)
        {
            animator =
                selected.GetComponentInChildren<Animator>(
                    true);
        }

        return
            animator != null &&
            animator.GetComponentInChildren<SpriteVisualAnimationDriver>(
                true) != null;
    }

    private static bool IsKeyingWindowOpen()
    {
        return
            Resources.FindObjectsOfTypeAll<SpriteVisualKeyingWindow>()
                .Length > 0;
    }
}

#endif
