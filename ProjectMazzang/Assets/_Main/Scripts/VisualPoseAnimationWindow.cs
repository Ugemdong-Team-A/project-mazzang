using System.Linq;
using UnityEditor;
using UnityEngine;

public class VisualPoseAnimationWindow : EditorWindow
{
    private SpriteVisualAnimationTest _target;

    [MenuItem("Tools/Animation/Visual Pose Keyer")]
    private static void Open()
    {
        GetWindow<VisualPoseAnimationWindow>(
            "Visual Pose");
    }

    private void OnEnable()
    {
        EditorApplication.update += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    private void OnGUI()
    {
        AnimationWindow animationWindow =
            GetAnimationWindow();

        if (animationWindow == null)
        {
            EditorGUILayout.HelpBox(
                "Animation Window(Ctrl+6)을 열어주세요.",
                MessageType.Info);

            return;
        }

        AnimationClip clip =
            animationWindow.animationClip;

        if (clip == null)
        {
            EditorGUILayout.HelpBox(
                "Animation Clip을 선택해주세요.",
                MessageType.Info);

            return;
        }

        FindTargetAutomatically();

        EditorGUILayout.LabelField(
            "Clip",
            clip.name);

        EditorGUILayout.LabelField(
            "Frame",
            animationWindow.frame.ToString());

        EditorGUILayout.Space(8);

        _target =
            (SpriteVisualAnimationTest)
            EditorGUILayout.ObjectField(
                "Target",
                _target,
                typeof(SpriteVisualAnimationTest),
                true);

        if (_target == null)
        {
            EditorGUILayout.HelpBox(
                "SpriteVisualAnimationTest를 찾을 수 없습니다.",
                MessageType.Warning);

            return;
        }

        EditorGUILayout.Space(8);

        TestVisualPose currentPose =
            GetPoseAtCurrentTime(
                animationWindow,
                clip);

        EditorGUI.BeginChangeCheck();

        TestVisualPose newPose =
            (TestVisualPose)
            EditorGUILayout.EnumPopup(
                "Pose",
                currentPose);

        if (EditorGUI.EndChangeCheck())
        {
            AddPoseKey(
                animationWindow,
                clip,
                newPose);
        }
    }

    private void FindTargetAutomatically()
    {
        if (_target != null)
            return;

        GameObject selected =
            Selection.activeGameObject;

        if (selected == null)
            return;

        _target =
            selected.GetComponent<SpriteVisualAnimationTest>();

        if (_target != null)
            return;

        _target =
            selected.GetComponentInChildren<
                SpriteVisualAnimationTest>(true);
    }

    private AnimationWindow GetAnimationWindow()
    {
        return Resources
            .FindObjectsOfTypeAll<AnimationWindow>()
            .FirstOrDefault();
    }

    private TestVisualPose GetPoseAtCurrentTime(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        EditorCurveBinding binding =
            CreateBinding();

        AnimationCurve curve =
            AnimationUtility.GetEditorCurve(
                clip,
                binding);

        if (curve == null ||
            curve.length == 0)
        {
            return _target.Pose;
        }

        float value =
            curve.Evaluate(
                animationWindow.time);

        return (TestVisualPose)
            Mathf.RoundToInt(value);
    }

    private void AddPoseKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        TestVisualPose pose)
    {
        EditorCurveBinding binding =
            CreateBinding();

        AnimationCurve curve =
            AnimationUtility.GetEditorCurve(
                clip,
                binding);

        if (curve == null)
            curve = new AnimationCurve();

        float time =
            animationWindow.time;

        float value =
            (int)pose;

        Undo.RegisterCompleteObjectUndo(
            clip,
            "Set Visual Pose");

        int existingIndex =
            FindKeyAtTime(
                curve,
                time);

        int keyIndex;

        if (existingIndex >= 0)
        {
            keyIndex =
                curve.MoveKey(
                    existingIndex,
                    new Keyframe(
                        time,
                        value));
        }
        else
        {
            keyIndex =
                curve.AddKey(
                    new Keyframe(
                        time,
                        value));
        }

        // Enum이므로 키 사이를 보간하면 안 됨.
        AnimationUtility.SetKeyLeftTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.Constant);

        AnimationUtility.SetKeyRightTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.Constant);

        AnimationUtility.SetEditorCurve(
            clip,
            binding,
            curve);

        EditorUtility.SetDirty(clip);

        // 선택하자마자 아트분 화면에서도 즉시 반영.
        _target.PreviewPose(pose);

        animationWindow.Repaint();
        SceneView.RepaintAll();
    }

    private EditorCurveBinding CreateBinding()
    {
        Transform animationRoot =
            FindAnimationRoot();

        string path =
            AnimationUtility.CalculateTransformPath(
                _target.transform,
                animationRoot);

        return EditorCurveBinding.FloatCurve(
            path,
            typeof(SpriteVisualAnimationTest),
            "pose");
    }

    private Transform FindAnimationRoot()
    {
        Animator animator =
            _target.GetComponentInParent<Animator>();

        if (animator != null)
            return animator.transform;

        return _target.transform;
    }

    private static int FindKeyAtTime(
        AnimationCurve curve,
        float time)
    {
        const float tolerance = 0.0001f;

        for (int i = 0;
             i < curve.length;
             i++)
        {
            if (Mathf.Abs(
                    curve.keys[i].time -
                    time)
                < tolerance)
            {
                return i;
            }
        }

        return -1;
    }
}
