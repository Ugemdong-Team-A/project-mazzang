using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ProjectMazzang.Tests.Legacy
{
    /// <summary>
    /// UI와 Render 호환을 위해 남겨 둔 PlayerContext 계약만 검증합니다.
    /// 새 Tick 모듈 구조의 요구사항으로 사용하지 않습니다.
    /// </summary>
    public sealed class LegacyPlayerContextContractTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void LegacyModules_KeepPresentationContextContracts()
        {
            AssertInterfaces(
                "PlayerMovement",
                "IPlayerMovementState",
                "IPlayerMovementControl",
                "IPlayerKnockbackReceiver",
                "IPlayerFacingControl");

            AssertInterfaces(
                "PlayerAim",
                "IPlayerAimState",
                "IPlayerAimControl");

            AssertInterfaces(
                "PlayerCombat",
                "IPlayerCombatState",
                "IPlayerCombatControl");

            AssertInterfaces(
                "PlayerHealth",
                "IPlayerHealthState",
                "IPlayerDamageReceiver");
        }


        [Test]
        public void LegacyFallback_KeepsContextFields()
        {
            AssertFieldTypes(
                "PlayerMovement",
                ("_healthState", "IPlayerHealthState"),
                ("_combatState", "IPlayerCombatState"));

            AssertFieldTypes(
                "PlayerAim",
                ("_movementState", "IPlayerMovementState"),
                ("_facingControl", "IPlayerFacingControl"));

            AssertFieldTypes(
                "PlayerHealth",
                ("_knockbackReceiver", "IPlayerKnockbackReceiver"),
                ("_combatControl", "IPlayerCombatControl"));
        }


        private static void AssertInterfaces(
            string typeName,
            params string[] expectedInterfaces)
        {
            HashSet<string> actualInterfaces =
                GetRuntimeType(typeName)
                    .GetInterfaces()
                    .Select(
                        type =>
                            type.Name)
                    .ToHashSet();

            foreach (string expected
                     in expectedInterfaces)
            {
                Assert.That(
                    actualInterfaces,
                    Does.Contain(expected));
            }
        }


        private static void AssertFieldTypes(
            string typeName,
            params (string FieldName, string FieldType)[] fields)
        {
            Type type =
                GetRuntimeType(
                    typeName);

            foreach ((string fieldName, string fieldType)
                     in fields)
            {
                FieldInfo field =
                    type.GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(
                    field,
                    Is.Not.Null);

                Assert.That(
                    field.FieldType.Name,
                    Is.EqualTo(fieldType));
            }
        }


        private static Type GetRuntimeType(
            string typeName)
        {
            Type type =
                RuntimeAssembly.GetType(
                    typeName);

            Assert.That(
                type,
                Is.Not.Null);

            return type;
        }
    }
}
