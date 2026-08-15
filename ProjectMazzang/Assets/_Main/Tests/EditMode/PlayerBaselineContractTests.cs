using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class PlayerBaselineContractTests
    {
        private static readonly IReadOnlyDictionary<string, int>
            ExpectedExecutionOrders =
                new Dictionary<string, int>
                {
                    ["PlayerController"] = -1000,
                    ["PlayerHealth"] = -300,
                    ["PlayerWeaponController"] = -210,
                    ["PlayerCombat"] = -200,
                    ["PlayerMovement"] = -100,
                    ["PlayerAim"] = -90,
                    ["PlayerSkillController"] = -80,
                    ["PlayerAnimation"] = 0
                };

        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void PlayerScripts_KeepCurrentExecutionOrder()
        {
            foreach (KeyValuePair<string, int> pair
                     in ExpectedExecutionOrders)
            {
                Type type =
                    GetRuntimeType(
                        pair.Key);

                DefaultExecutionOrder attribute =
                    type.GetCustomAttribute<
                        DefaultExecutionOrder>();

                int actualOrder =
                    attribute != null
                        ? attribute.order
                        : 0;

                Assert.That(
                    actualOrder,
                    Is.EqualTo(pair.Value),
                    $"{pair.Key}의 실행 순서가 기준과 달라졌습니다.");
            }
        }


        [Test]
        public void CoreModules_KeepCurrentContextContracts()
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
        public void OrderedStateModules_ProvideTickStateSources()
        {
            AssertInterfaces(
                "PlayerHealth",
                "IPlayerTickStateSource");

            AssertInterfaces(
                "PlayerCombat",
                "IPlayerTickStateSource");

            AssertInterfaces(
                "PlayerMovement",
                "IPlayerTickStateSource");
        }


        [Test]
        public void PlayerTickState_ContainsMovementAndCombatSnapshot()
        {
            Type tickStateType =
                GetRuntimeType(
                    "PlayerTickState");

            AssertPropertyType(
                tickStateType,
                "HasMovement",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "FacingRight",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "IsWallSliding",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "IsMovementControlLocked",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "HasCombat",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "IsCombatMovementLocked",
                typeof(bool));
        }


        [Test]
        public void PlayerTickState_ResetClearsPreviousTickValues()
        {
            Type tickStateType =
                GetRuntimeType(
                    "PlayerTickState");

            object tickState =
                Activator.CreateInstance(
                    tickStateType);

            tickStateType
                .GetProperty("HasMovement")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("FacingRight")
                .SetValue(tickState, false);

            tickStateType
                .GetProperty("IsWallSliding")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("IsMovementControlLocked")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("HasCombat")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("IsCombatMovementLocked")
                .SetValue(tickState, true);

            MethodInfo reset =
                tickStateType.GetMethod(
                    "Reset",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                reset,
                Is.Not.Null);

            reset.Invoke(
                tickState,
                null);

            AssertProperty(
                tickState,
                "HasMovement",
                false);

            AssertProperty(
                tickState,
                "FacingRight",
                true);

            AssertProperty(
                tickState,
                "IsWallSliding",
                false);

            AssertProperty(
                tickState,
                "IsMovementControlLocked",
                false);

            AssertProperty(
                tickState,
                "HasCombat",
                false);

            AssertProperty(
                tickState,
                "IsCombatMovementLocked",
                false);
        }


        [Test]
        public void CoreModules_KeepCurrentConsumedContextFields()
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


        [Test]
        public void AttackData_KeepsCurrentSerializedControlLockField()
        {
            Type attackDataType =
                GetRuntimeType(
                    "AttackData");

            ScriptableObject attackData =
                ScriptableObject.CreateInstance(
                    attackDataType);

            try
            {
                SerializedObject serializedObject =
                    new(
                        attackData);

                SerializedProperty property =
                    serializedObject.FindProperty(
                        "knockbackControlLock");

                Assert.That(
                    property,
                    Is.Not.Null,
                    "현재 AttackData 직렬화 필드가 사라졌습니다.");

                Assert.That(
                    property.floatValue,
                    Is.EqualTo(0.12f).Within(0.0001f),
                    "기본 control lock 시간이 달라졌습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    attackData);
            }
        }


        [Test]
        public void DamageInfo_KeepsKnockbackControlLockValue()
        {
            Type damageInfoType =
                GetRuntimeType(
                    "DamageInfo");

            ConstructorInfo constructor =
                damageInfoType
                    .GetConstructors()
                    .Single();

            object damageInfo =
                constructor.Invoke(
                    new object[]
                    {
                        17,
                        null,
                        new Vector2(3f, 4f),
                        0.35f
                    });

            AssertProperty(
                damageInfo,
                "Damage",
                17);

            AssertProperty(
                damageInfo,
                "Knockback",
                new Vector2(3f, 4f));

            AssertProperty(
                damageInfo,
                "KnockbackControlLock",
                0.35f);
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
                    Does.Contain(expected),
                    $"{typeName}이 {expected} 계약을 더 이상 제공하지 않습니다.");
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
                    Is.Not.Null,
                    $"{typeName}.{fieldName} 필드를 찾을 수 없습니다.");

                Assert.That(
                    field.FieldType.Name,
                    Is.EqualTo(fieldType),
                    $"{typeName}.{fieldName}의 계약 타입이 달라졌습니다.");
            }
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
                Is.Not.Null,
                $"{propertyName} 프로퍼티를 찾을 수 없습니다.");

            Assert.That(
                property.GetValue(instance),
                Is.EqualTo(expected));
        }


        private static void AssertPropertyType(
            Type type,
            string propertyName,
            Type propertyType)
        {
            PropertyInfo property =
                type.GetProperty(
                    propertyName);

            Assert.That(
                property,
                Is.Not.Null,
                $"{type.Name}.{propertyName} 프로퍼티를 찾을 수 없습니다.");

            Assert.That(
                property.PropertyType,
                Is.EqualTo(propertyType),
                $"{type.Name}.{propertyName}의 타입이 달라졌습니다.");
        }


        private static Type GetRuntimeType(
            string typeName)
        {
            Type type =
                RuntimeAssembly.GetType(
                    typeName);

            Assert.That(
                type,
                Is.Not.Null,
                $"Assembly-CSharp에서 {typeName}을 찾을 수 없습니다.");

            return type;
        }
    }
}
