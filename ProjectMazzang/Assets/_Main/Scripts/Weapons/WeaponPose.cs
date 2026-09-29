using System;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class WeaponPose : MonoBehaviour
{
    public const string ObjectName = "WeaponPose";

    private AnimationClip _clip;
    private float _elapsed;
    private TransformState[] _transforms =
        Array.Empty<TransformState>();
    private SpriteState[] _sprites =
        Array.Empty<SpriteState>();
    private DriverState[] _drivers =
        Array.Empty<DriverState>();
    private AnimatorState[] _animators =
        Array.Empty<AnimatorState>();
    private Vector3 _rootPosition;
    private Quaternion _rootRotation;
    private Vector3 _rootScale = Vector3.one;
    private bool _mirrored;

    public bool IsPlaying =>
        _clip != null;

    public static WeaponPose GetOrCreate(
        Transform socket)
    {
        if (socket == null)
            return null;

        Transform existing =
            socket.Find(ObjectName);

        WeaponPose pose =
            existing != null
                ? existing.GetComponent<WeaponPose>()
                : null;

        if (pose != null)
            return pose;

        GameObject poseObject =
            existing != null
                ? existing.gameObject
                : new GameObject(ObjectName);

        poseObject.transform.SetParent(
            socket,
            false);
        poseObject.transform.localPosition =
            Vector3.zero;
        poseObject.transform.localRotation =
            Quaternion.identity;
        poseObject.transform.localScale =
            Vector3.one;

        return poseObject.AddComponent<WeaponPose>();
    }

    public void Attach(
        HeldWeaponView view)
    {
        Stop();

        if (view == null)
            return;

        view.transform.SetParent(
            transform,
            false);
        view.transform.localPosition =
            Vector3.zero;
        view.transform.localRotation =
            Quaternion.identity;
        view.transform.localScale =
            Vector3.one;

        CaptureDefaultPose();
    }

    public void SetMirrored(
        bool mirrored)
    {
        _mirrored = mirrored;
        ApplyMirror();
    }

    public void Play(
        AnimationClip clip)
    {
        Stop();

        if (clip == null)
            return;

        CaptureDefaultPose();
        SetChildAnimatorsEnabled(false);
        _clip = clip;
        _elapsed = 0f;
        Sample(0f);
    }

    public void Stop()
    {
        if (_clip == null)
            return;

        RestoreDefaultPose();
        SetChildAnimatorsEnabled(true);
        _clip = null;
        _elapsed = 0f;
    }

    private void LateUpdate()
    {
        if (_clip == null)
            return;

        _elapsed += Time.deltaTime;

        if (_elapsed >= _clip.length)
        {
            Stop();
            return;
        }

        Sample(_elapsed);
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Sample(float time)
    {
        RestoreDefaultPose();
        _clip.SampleAnimation(
            gameObject,
            Mathf.Clamp(
                time,
                0f,
                _clip.length));
        ApplyMirror();
    }

    private void CaptureDefaultPose()
    {
        _rootPosition = transform.localPosition;
        _rootRotation = transform.localRotation;
        _rootScale = new Vector3(
            transform.localScale.x,
            Mathf.Abs(transform.localScale.y),
            transform.localScale.z);

        Transform[] transforms =
            GetComponentsInChildren<Transform>(true);
        _transforms = new TransformState[
            Mathf.Max(0, transforms.Length - 1)];

        for (int i = 1; i < transforms.Length; i++)
        {
            _transforms[i - 1] =
                new TransformState(transforms[i]);
        }

        SpriteRenderer[] sprites =
            GetComponentsInChildren<SpriteRenderer>(true);
        _sprites = new SpriteState[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
            _sprites[i] = new SpriteState(sprites[i]);

        SpriteVisualAnimationDriver[] drivers =
            GetComponentsInChildren<
                SpriteVisualAnimationDriver>(true);
        _drivers = new DriverState[drivers.Length];

        for (int i = 0; i < drivers.Length; i++)
            _drivers[i] = new DriverState(drivers[i]);

        Animator[] animators =
            GetComponentsInChildren<Animator>(true);
        _animators = new AnimatorState[animators.Length];

        for (int i = 0; i < animators.Length; i++)
            _animators[i] = new AnimatorState(animators[i]);
    }

    private void RestoreDefaultPose()
    {
        transform.localPosition = _rootPosition;
        transform.localRotation = _rootRotation;
        transform.localScale = _rootScale;
        ApplyMirror();

        foreach (TransformState state in _transforms)
            state.Restore();

        foreach (SpriteState state in _sprites)
            state.Restore();

        foreach (DriverState state in _drivers)
            state.Restore();
    }

    private void SetChildAnimatorsEnabled(
        bool restore)
    {
        foreach (AnimatorState state in _animators)
            state.SetEnabled(restore);
    }

    private void ApplyMirror()
    {
        Vector3 scale = transform.localScale;
        scale.y =
            Mathf.Abs(scale.y) *
            (_mirrored ? -1f : 1f);
        transform.localScale = scale;
    }

    private readonly struct TransformState
    {
        private readonly Transform _target;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Vector3 _scale;

        public TransformState(Transform target)
        {
            _target = target;
            _position = target.localPosition;
            _rotation = target.localRotation;
            _scale = target.localScale;
        }

        public void Restore()
        {
            if (_target == null)
                return;

            _target.localPosition = _position;
            _target.localRotation = _rotation;
            _target.localScale = _scale;
        }
    }

    private readonly struct SpriteState
    {
        private readonly SpriteRenderer _target;
        private readonly Sprite _sprite;
        private readonly Color _color;
        private readonly bool _enabled;
        private readonly bool _flipX;
        private readonly bool _flipY;
        private readonly int _sortingLayer;
        private readonly int _sortingOrder;

        public SpriteState(SpriteRenderer target)
        {
            _target = target;
            _sprite = target.sprite;
            _color = target.color;
            _enabled = target.enabled;
            _flipX = target.flipX;
            _flipY = target.flipY;
            _sortingLayer = target.sortingLayerID;
            _sortingOrder = target.sortingOrder;
        }

        public void Restore()
        {
            if (_target == null)
                return;

            _target.sprite = _sprite;
            _target.color = _color;
            _target.enabled = _enabled;
            _target.flipX = _flipX;
            _target.flipY = _flipY;
            _target.sortingLayerID = _sortingLayer;
            _target.sortingOrder = _sortingOrder;
        }
    }

    private readonly struct DriverState
    {
        private readonly SpriteVisualAnimationDriver _target;
        private readonly int _label;
        private readonly int _order;

        public DriverState(
            SpriteVisualAnimationDriver target)
        {
            _target = target;
            _label = target.LabelIndex;
            _order = target.SortingOrder;
        }

        public void Restore()
        {
            if (_target == null)
                return;

            _target.PreviewLabel(_label);
            _target.PreviewSortingOrder(_order);
        }
    }

    private readonly struct AnimatorState
    {
        private readonly Animator _target;
        private readonly bool _enabled;

        public AnimatorState(Animator target)
        {
            _target = target;
            _enabled = target.enabled;
        }

        public void SetEnabled(bool restore)
        {
            if (_target != null)
                _target.enabled = restore && _enabled;
        }
    }
}
