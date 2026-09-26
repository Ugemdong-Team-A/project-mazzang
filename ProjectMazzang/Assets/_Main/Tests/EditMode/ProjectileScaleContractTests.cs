using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ProjectMazzang.Tests
{
    public sealed class ProjectileScaleContractTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void LaunchSettings_KeepBaseRadiusSeparateFromScale()
        {
            object baseSettings =
                Activator.CreateInstance(
                    GetRuntimeType(
                        "ProjectileBaseSettings"),
                    16f,
                    2.5f,
                    0f,
                    9.81f,
                    0f,
                    true,
                    0.2f);

            object stats =
                Activator.CreateInstance(
                    GetRuntimeType(
                        "ProjectileStatSnapshot"),
                    1f,
                    1f,
                    1f,
                    1f,
                    1.5f,
                    1f);

            object launchSettings =
                baseSettings.GetType()
                    .GetMethod(
                        "Resolve")
                    ?.Invoke(
                        baseSettings,
                        new[] { stats });

            AssertProperty(
                launchSettings,
                "BaseCollisionRadius",
                0.2f);

            Assert.That(
                InvokeResolveCollisionRadius(
                    launchSettings,
                    1.5f),
                Is.EqualTo(0.3f).Within(0.0001f));
        }


        [Test]
        public void CollisionRadius_UsesIdentityForInvalidScale()
        {
            object launchSettings =
                Activator.CreateInstance(
                    GetRuntimeType(
                        "ProjectileLaunchSettings"),
                    16f,
                    2.5f,
                    0f,
                    9.81f,
                    0f,
                    true,
                    0.2f);

            Assert.That(
                InvokeResolveCollisionRadius(
                    launchSettings,
                    0f),
                Is.EqualTo(0.2f));
        }


        private static Type GetRuntimeType(
            string typeName)
        {
            return RuntimeAssembly
                .GetTypes()
                .Single(
                    type =>
                        type.Name ==
                        typeName);
        }


        private static float InvokeResolveCollisionRadius(
            object launchSettings,
            float scaleMultiplier)
        {
            Assert.That(
                launchSettings,
                Is.Not.Null);

            object result =
                launchSettings.GetType()
                    .GetMethod(
                        "ResolveCollisionRadius")
                    ?.Invoke(
                        launchSettings,
                        new object[]
                        {
                            scaleMultiplier
                        });

            Assert.That(
                result,
                Is.Not.Null);

            return (float)result;
        }


        private static void AssertProperty(
            object instance,
            string propertyName,
            float expected)
        {
            Assert.That(
                instance,
                Is.Not.Null);

            PropertyInfo property =
                instance.GetType()
                    .GetProperty(
                        propertyName);

            Assert.That(
                property,
                Is.Not.Null);

            Assert.That(
                property.GetValue(instance),
                Is.EqualTo(expected));
        }
    }
}
