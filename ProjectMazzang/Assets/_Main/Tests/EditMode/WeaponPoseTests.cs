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
    }
}
