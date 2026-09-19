using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectMazzang.Tests
{
    public sealed class WeaponPoseTests
    {
        private const BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private GameObject _socketObject;

        private static Type RuntimeType(string name) =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp")
                .GetType(
                    name,
                    true);

        private static object Invoke(
            object target,
            string method,
            params object[] arguments) =>
            target
                .GetType()
                .GetMethod(
                    method,
                    Flags)
                .Invoke(
                    target,
                    arguments);

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            Type type =
                target.GetType();

            while (type != null)
            {
                FieldInfo field =
                    type.GetField(
                        fieldName,
                        Flags);

                if (field != null)
                {
                    field.SetValue(
                        target,
                        value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(
                $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
        }

        private static Vector2 ReadPoseVector(
            object pose,
            string fieldName) =>
            (Vector2)pose
                .GetType()
                .GetField(
                    fieldName,
                    Flags)
                .GetValue(pose);

        [SetUp]
        public void SetUp()
        {
            _socketObject =
                new GameObject("WeaponSocket");
        }

        [TearDown]
        public void TearDown()
        {
            if (_socketObject != null)
                Object.DestroyImmediate(_socketObject);
        }

        [Test]
        public void Initialize_CreatesStablePoseHierarchy()
        {
            Component view =
                new GameObject("Weapon")
                    .AddComponent(
                        RuntimeType("HeldWeaponView"));

            Invoke(
                view,
                "Initialize",
                _socketObject.transform,
                9);

            Component pose =
                (Component)view
                    .GetType()
                    .GetProperty("Pose")
                    .GetValue(view);
            string runtimeViewName =
                (string)view
                    .GetType()
                    .GetField("RuntimeViewName")
                    .GetRawConstantValue();

            Assert.That(pose, Is.Not.Null);
            Assert.That(
                pose.transform.parent,
                Is.EqualTo(_socketObject.transform));
            Assert.That(
                view.transform.parent,
                Is.EqualTo(pose.transform));
            Assert.That(
                view.name,
                Is.EqualTo(runtimeViewName));
            Assert.That(
                view.transform.localPosition,
                Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Animation_UsesViewRelativePath_AndRestoresPose()
        {
            Component view =
                new GameObject("Weapon")
                    .AddComponent(
                        RuntimeType("HeldWeaponView"));
            Invoke(
                view,
                "Initialize",
                _socketObject.transform,
                9);

            string runtimeViewName =
                (string)view
                    .GetType()
                    .GetField("RuntimeViewName")
                    .GetRawConstantValue();

            AnimationClip clip = new();
            clip.SetCurve(
                runtimeViewName,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Constant(
                    0f,
                    1f,
                    2f));

            Invoke(
                view,
                "PlayAnimation",
                clip);

            Assert.That(
                view.transform.localPosition.x,
                Is.EqualTo(2f).Within(0.001f));

            Invoke(
                view,
                "StopAnimation");

            Assert.That(
                view.transform.localPosition,
                Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Mirroring_DoesNotOverwriteAnimatedViewTransform()
        {
            Component view =
                new GameObject("Weapon")
                    .AddComponent(
                        RuntimeType("HeldWeaponView"));
            Invoke(
                view,
                "Initialize",
                _socketObject.transform,
                9);
            view.transform.localScale =
                new Vector3(2f, 3f, 1f);

            Invoke(
                view,
                "SetMirrored",
                true);

            Component pose =
                (Component)view
                    .GetType()
                    .GetProperty("Pose")
                    .GetValue(view);

            Assert.That(
                pose.transform.localScale,
                Is.EqualTo(new Vector3(1f, -1f, 1f)));
            Assert.That(
                view.transform.localScale,
                Is.EqualTo(new Vector3(2f, 3f, 1f)));
        }

        [Test]
        public void WeaponAction_KeepsCharacterAndWeaponClipsSeparate()
        {
            Type actionType =
                RuntimeType("WeaponActionData");
            object action =
                Activator.CreateInstance(actionType);
            AnimationClip weaponClip = new();
            FieldInfo field =
                actionType.GetField(
                    "weaponAnimation",
                    Flags);

            field.SetValue(
                action,
                weaponClip);

            Assert.That(
                actionType
                    .GetProperty("WeaponAnimation")
                    .GetValue(action),
                Is.SameAs(weaponClip));
            Assert.That(
                actionType
                    .GetProperty("Animation")
                    .GetValue(action),
                Is.Null);
        }

        [Test]
        public void MuzzlePose_UsesRenderedMuzzlePositionAndVisualForward()
        {
            GameObject viewObject =
                new GameObject("HeldWeaponView");
            viewObject.transform.SetParent(
                _socketObject.transform);
            viewObject.transform.SetPositionAndRotation(
                new Vector3(3f, 4f, 0f),
                Quaternion.Euler(0f, 0f, 25f));
            viewObject.transform.localScale =
                new Vector3(-1f, 1f, 1f);

            Component view =
                viewObject.AddComponent(
                    RuntimeType("HeldWeaponView"));

            Transform muzzle =
                new GameObject("Muzzle").transform;
            muzzle.SetParent(
                viewObject.transform,
                false);
            muzzle.localPosition =
                new Vector3(1.2f, 0.3f, 0f);
            muzzle.localRotation =
                Quaternion.Euler(0f, 0f, 18f);

            SetField(
                view,
                "muzzle",
                muzzle);

            Component gun =
                new GameObject("Gun")
                    .AddComponent(
                        RuntimeType("ProjectileGun"));
            gun.transform.SetParent(
                _socketObject.transform);

            SetField(
                gun,
                "presentationTemplate",
                view);
            SetField(
                gun,
                "_heldView",
                view);

            object pose =
                Invoke(
                    gun,
                    "ResolveMuzzlePose",
                    Vector2.zero,
                    Vector2.down,
                    false);

            Assert.That(
                Vector2.Distance(
                    ReadPoseVector(
                        pose,
                        "Origin"),
                    muzzle.position),
                Is.LessThan(0.0001f));

            Assert.That(
                Vector2.Angle(
                    ReadPoseVector(
                        pose,
                        "Direction"),
                    muzzle.TransformVector(
                        Vector3.right)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void MuzzlePose_FallbackUsesAuthoredLocalPoseAndMirror()
        {
            GameObject viewObject =
                new GameObject("HeldWeaponView");
            viewObject.transform.SetParent(
                _socketObject.transform);
            viewObject.transform.SetPositionAndRotation(
                new Vector3(7f, -3f, 0f),
                Quaternion.Euler(0f, 0f, 31f));

            Component view =
                viewObject.AddComponent(
                    RuntimeType("HeldWeaponView"));

            Transform muzzle =
                new GameObject("Muzzle").transform;
            muzzle.SetParent(
                viewObject.transform,
                false);
            muzzle.localPosition =
                new Vector3(1f, 0.25f, 0f);
            muzzle.localRotation =
                Quaternion.Euler(0f, 0f, 15f);

            SetField(
                view,
                "muzzle",
                muzzle);

            Component gun =
                new GameObject("Gun")
                    .AddComponent(
                        RuntimeType("ProjectileGun"));
            gun.transform.SetParent(
                _socketObject.transform);

            SetField(
                gun,
                "presentationTemplate",
                view);

            Vector2 fallbackOrigin =
                new Vector2(10f, 20f);

            object pose =
                Invoke(
                    gun,
                    "ResolveMuzzlePose",
                    fallbackOrigin,
                    Vector2.up,
                    true);

            Vector2 expectedOrigin =
                fallbackOrigin +
                new Vector2(0.25f, 1f);

            Vector2 expectedDirection =
                Quaternion.Euler(0f, 0f, 75f) *
                Vector2.right;

            Assert.That(
                Vector2.Distance(
                    ReadPoseVector(
                        pose,
                        "Origin"),
                    expectedOrigin),
                Is.LessThan(0.0001f));

            Assert.That(
                Vector2.Angle(
                    ReadPoseVector(
                        pose,
                        "Direction"),
                    expectedDirection),
                Is.LessThan(0.001f));
        }
    }
}
