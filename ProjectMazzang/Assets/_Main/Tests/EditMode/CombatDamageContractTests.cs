using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class CombatDamageContractTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void DamageResult_ReportsProcessedDamage()
        {
            Type resultType =
                GetRuntimeType(
                    "DamageResult");

            object result =
                Activator.CreateInstance(
                    resultType,
                    17,
                    true);

            AssertProperty(
                result,
                "WasProcessed",
                true);

            AssertProperty(
                result,
                "AppliedDamage",
                17);

            AssertProperty(
                result,
                "WasFatal",
                true);
        }


        [Test]
        public void CombatDamageService_RejectsMissingTarget()
        {
            Type damageInfoType =
                GetRuntimeType(
                    "DamageInfo");

            object damageInfo =
                Activator.CreateInstance(
                    damageInfoType,
                    10,
                    null,
                    Vector2.zero,
                    0f);

            MethodInfo applyDamage =
                GetRuntimeType(
                        "CombatDamageService")
                    .GetMethod(
                        "ApplyDamage",
                        BindingFlags.Public |
                        BindingFlags.Static);

            Assert.That(
                applyDamage,
                Is.Not.Null);

            Assert.That(
                applyDamage.ReturnType.Name,
                Is.EqualTo(
                    "DamageResult"));

            object result =
                applyDamage.Invoke(
                    null,
                    new[]
                    {
                        null,
                        damageInfo
                    });

            AssertProperty(
                result,
                "WasProcessed",
                false);

            AssertProperty(
                result,
                "AppliedDamage",
                0);

            AssertProperty(
                result,
                "WasFatal",
                false);
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
            object expected)
        {
            PropertyInfo property =
                instance.GetType()
                    .GetProperty(
                        propertyName);

            Assert.That(
                property,
                Is.Not.Null);

            Assert.That(
                property.GetValue(
                    instance),
                Is.EqualTo(
                    expected));
        }
    }
}
