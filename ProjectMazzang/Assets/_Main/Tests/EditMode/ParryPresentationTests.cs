using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class ParryPresentationTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");

        private static MethodInfo SetArcMethod =>
            RuntimeAssembly
                .GetTypes()
                .Single(
                    type =>
                        type.Name ==
                        "ParryPresentation")
                .GetMethod(
                    "SetArc",
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

        private static MethodInfo ResolveOriginMethod =>
            RuntimeAssembly
                .GetTypes()
                .Single(
                    type =>
                        type.Name ==
                        "ParryGeometry")
                .GetMethod(
                    "ResolveOrigin",
                    BindingFlags.Static |
                    BindingFlags.Public);

        private static MethodInfo ResolveLocalOriginMethod =>
            RuntimeAssembly
                .GetTypes()
                .Single(
                    type =>
                        type.Name ==
                        "ParryGeometry")
                .GetMethod(
                    "ResolveLocalOrigin",
                    BindingFlags.Static |
                    BindingFlags.Public);


        [Test]
        public void SetArc_KeepsEndpointsSymmetricAroundDirection()
        {
            GameObject lineObject =
                new("Parry Arc Test");

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            try
            {
                InvokeSetArc(
                    line,
                    70f);

                Vector3 first =
                    line.GetPosition(0);

                Vector3 last =
                    line.GetPosition(
                        line.positionCount - 1);

                Assert.That(
                    first.x,
                    Is.EqualTo(last.x).Within(0.0001f));

                Assert.That(
                    first.y,
                    Is.EqualTo(-last.y).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    lineObject);
            }
        }


        [Test]
        public void SetArc_AddsSegmentsForWiderAngles()
        {
            GameObject lineObject =
                new("Parry Arc Density Test");

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            try
            {
                InvokeSetArc(
                    line,
                    30f);

                int narrowCount =
                    line.positionCount;

                InvokeSetArc(
                    line,
                    120f);

                Assert.That(
                    line.positionCount,
                    Is.GreaterThan(narrowCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    lineObject);
            }
        }


        [Test]
        public void ResolveOrigin_MirrorsOnlyCenterOffsetX()
        {
            Vector2 rightFacingOrigin =
                InvokeResolveOrigin(
                    new Vector2(10f, 20f),
                    true,
                    new Vector2(2f, 3f));

            Assert.That(
                rightFacingOrigin,
                Is.EqualTo(
                    new Vector2(12f, 23f)));

            Vector2 leftFacingOrigin =
                InvokeResolveOrigin(
                    new Vector2(10f, 20f),
                    false,
                    new Vector2(2f, 3f));

            Assert.That(
                leftFacingOrigin,
                Is.EqualTo(
                    new Vector2(8f, 23f)));
        }


        [Test]
        public void ResolveLocalOrigin_RotatesOffsetWithForward()
        {
            Assert.That(
                ResolveLocalOriginMethod,
                Is.Not.Null);

            Vector2 rightOrigin =
                (Vector2)ResolveLocalOriginMethod.Invoke(
                    null,
                    new object[]
                    {
                        new Vector2(10f, 20f),
                        Vector2.right,
                        new Vector2(2f, 3f),
                        false
                    });

            Assert.That(
                rightOrigin,
                Is.EqualTo(
                    new Vector2(12f, 23f)));

            Vector2 leftOrigin =
                (Vector2)ResolveLocalOriginMethod.Invoke(
                    null,
                    new object[]
                    {
                        new Vector2(10f, 20f),
                        Vector2.left,
                        new Vector2(2f, 3f),
                        true
                    });

            Assert.That(
                leftOrigin,
                Is.EqualTo(
                    new Vector2(8f, 23f)));

            Vector2 upOrigin =
                (Vector2)ResolveLocalOriginMethod.Invoke(
                    null,
                    new object[]
                    {
                        new Vector2(10f, 20f),
                        Vector2.up,
                        new Vector2(2f, 3f),
                        false
                    });

            Assert.That(
                upOrigin,
                Is.EqualTo(
                    new Vector2(7f, 22f)));
        }


        private static void InvokeSetArc(
            LineRenderer line,
            float halfAngle)
        {
            Assert.That(
                SetArcMethod,
                Is.Not.Null);

            SetArcMethod.Invoke(
                null,
                new object[]
                {
                    line,
                    Vector2.zero,
                    Vector2.right,
                    1f,
                    halfAngle,
                    1f
                });
        }


        private static Vector2 InvokeResolveOrigin(
            Vector2 rootPosition,
            bool facingRight,
            Vector2 centerOffset)
        {
            Assert.That(
                ResolveOriginMethod,
                Is.Not.Null);

            return (Vector2)ResolveOriginMethod.Invoke(
                null,
                new object[]
                {
                    rootPosition,
                    facingRight,
                    centerOffset
                });
        }
    }
}
