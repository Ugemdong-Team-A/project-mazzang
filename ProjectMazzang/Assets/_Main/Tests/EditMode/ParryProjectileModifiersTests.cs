using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class ParryProjectileModifiersTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void Identity_DoesNotChangeProjectileValues()
        {
            Type modifierType =
                GetRuntimeType(
                    "ParryProjectileModifiers");

            object modifiers =
                modifierType
                    .GetProperty(
                        "Identity",
                        BindingFlags.Public |
                        BindingFlags.Static)
                    ?.GetValue(null);

            AssertProperty(modifiers, "DamageMultiplier", 1f);
            AssertProperty(modifiers, "SpeedMultiplier", 1f);
            AssertProperty(modifiers, "KnockbackMultiplier", 1f);
            AssertProperty(modifiers, "ScaleMultiplier", 1f);
        }


        [Test]
        public void Constructor_ClampsInvalidMultipliers()
        {
            object modifiers =
                Activator.CreateInstance(
                    GetRuntimeType(
                        "ParryProjectileModifiers"),
                    -1f,
                    -2f,
                    -3f,
                    0f);

            AssertProperty(modifiers, "DamageMultiplier", 0f);
            AssertProperty(modifiers, "SpeedMultiplier", 0f);
            AssertProperty(modifiers, "KnockbackMultiplier", 0f);
            AssertProperty(modifiers, "ScaleMultiplier", 0.01f);
        }


        [Test]
        public void ParryData_ProvidesConfiguredDefaults()
        {
            ScriptableObject data =
                ScriptableObject.CreateInstance(
                    GetRuntimeType(
                        "ParryData"));

            try
            {
                object modifiers =
                    data.GetType()
                        .GetProperty(
                            "ProjectileModifiers")
                        ?.GetValue(data);

                AssertProperty(modifiers, "DamageMultiplier", 1f);
                AssertProperty(modifiers, "SpeedMultiplier", 1.15f);
                AssertProperty(modifiers, "KnockbackMultiplier", 1f);
                AssertProperty(modifiers, "ScaleMultiplier", 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
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
