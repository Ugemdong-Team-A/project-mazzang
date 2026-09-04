using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// Animator가 있는 캐릭터 Root에 붙는 표준 제작 도구의 진입점.
///
/// 관리 대상의 참조와 전체 유효 상태만 알고,
/// 실제 생성 규칙은 각 Editor Builder가 독립적으로 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Standard2DRigIKSetup))]
[AddComponentMenu("Mazzang/Animation/Standard 2D Character Setup")]
public sealed class Standard2DCharacterSetup : MonoBehaviour
{
    public const string ToolVersion = "1.4";

    [SerializeField, HideInInspector]
    private Animator _animator;

    [SerializeField, HideInInspector]
    private Standard2DRigIKSetup _rigIKSetup;

    [SerializeField, HideInInspector]
    private Transform _bodyAimReferenceBone;

    [SerializeField, HideInInspector]
    private Standard2DAimAnchor _aimAnchor;

    [SerializeField, HideInInspector]
    private SpriteLibrary _spriteLibrary;

    [SerializeField, HideInInspector]
    private SpriteResolver[] _spriteResolvers =
        Array.Empty<SpriteResolver>();

    public Animator Animator =>
        _animator;

    public Standard2DRigIKSetup RigIKSetup =>
        _rigIKSetup;

    public Transform BodyAimReferenceBone =>
        _bodyAimReferenceBone;

    public Standard2DAimAnchor AimAnchor =>
        _aimAnchor;

    public SpriteLibrary SpriteLibrary =>
        _spriteLibrary;

    public IReadOnlyList<SpriteResolver> SpriteResolvers =>
        _spriteResolvers;

    public Transform CharacterRoot =>
        transform;

    public bool HasValidManagedReferences =>
        _animator != null &&
        _animator.gameObject == gameObject &&
        _rigIKSetup != null &&
        _rigIKSetup.gameObject == gameObject &&
        _bodyAimReferenceBone != null &&
        _aimAnchor != null &&
        _aimAnchor.IsValid &&
        _aimAnchor.ReferenceBone == _bodyAimReferenceBone &&
        _spriteLibrary != null &&
        _spriteResolvers != null &&
        _spriteResolvers.Length > 0;

    private void Reset()
    {
        RefreshManagedReferences();
    }

    private void OnValidate()
    {
        RefreshManagedReferences();
    }

    /// <summary>
    /// Character Root와 그 하위에 이미 존재하는 관리 컴포넌트를 다시 연결한다.
    /// 관리 컴포넌트 쪽에는 CharacterSetup 참조를 만들지 않는다.
    /// </summary>
    public bool RefreshManagedReferences()
    {
        Animator animator =
            GetComponent<Animator>();

        Standard2DRigIKSetup rigIKSetup =
            GetComponent<Standard2DRigIKSetup>();

        Standard2DAimAnchor aimAnchor =
            GetComponentInChildren<Standard2DAimAnchor>(
                true);

        SpriteLibrary spriteLibrary =
            GetComponentInChildren<SpriteLibrary>(
                true);

        SpriteResolver[] spriteResolvers =
            GetComponentsInChildren<SpriteResolver>(
                true);

        bool changed =
            _animator != animator ||
            _rigIKSetup != rigIKSetup ||
            _aimAnchor != aimAnchor ||
            _spriteLibrary != spriteLibrary ||
            !HaveSameResolvers(
                _spriteResolvers,
                spriteResolvers);

        _animator = animator;
        _rigIKSetup = rigIKSetup;
        _aimAnchor = aimAnchor;
        _spriteLibrary = spriteLibrary;
        _spriteResolvers = spriteResolvers;

        Transform defaultReferenceBone =
            ResolveDefaultBodyAimReferenceBone();

        if (_bodyAimReferenceBone == null &&
            defaultReferenceBone != null)
        {
            _bodyAimReferenceBone = defaultReferenceBone;
            changed = true;
        }

        return changed;
    }

    public bool SetBodyAimReferenceBone(
        Transform referenceBone)
    {
        if (_bodyAimReferenceBone == referenceBone)
            return false;

        _bodyAimReferenceBone = referenceBone;
        return true;
    }

    private Transform ResolveDefaultBodyAimReferenceBone()
    {
        if (_aimAnchor != null &&
            _aimAnchor.ReferenceBone != null)
        {
            return _aimAnchor.ReferenceBone;
        }

        if (_rigIKSetup == null ||
            !Standard2DRigResolver.TryResolve(
                _rigIKSetup.RigSearchRoot,
                out Standard2DRigResolver.Result rig,
                out _))
        {
            return null;
        }

        return rig.Bones.TryGetValue(
            Standard2DRigDefinition.DefaultBodyAimReferenceBone,
            out Transform referenceBone)
                ? referenceBone
                : null;
    }

    private static bool HaveSameResolvers(
        SpriteResolver[] current,
        SpriteResolver[] found)
    {
        if (current == null ||
            current.Length != found.Length)
        {
            return false;
        }

        for (int i = 0;
             i < current.Length;
             i++)
        {
            if (current[i] != found[i])
                return false;
        }

        return true;
    }
}
