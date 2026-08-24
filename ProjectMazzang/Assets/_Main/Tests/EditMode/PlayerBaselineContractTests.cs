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
                    ["PlayerParry"] = -75,
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

            AssertInterfaces(
                "PlayerAim",
                "IPlayerTickStateSource");

            AssertInterfaces(
                "PlayerSkillController",
                "IPlayerTickStateSource");

            AssertInterfaces(
                "PlayerWeaponController",
                "IPlayerTickStateSource");
        }


        [Test]
        public void PlayerHealth_ProvidesDamageContract()
        {
            AssertInterfaces(
                "PlayerHealth",
                "IDamageable");
        }


        [Test]
        public void CommandModules_ProvideTickCommandSinks()
        {
            AssertInterfaces(
                "PlayerCombat",
                "IPlayerTickCommandSink");

            AssertInterfaces(
                "PlayerMovement",
                "IPlayerTickCommandSink");

            AssertInterfaces(
                "PlayerAim",
                "IPlayerTickCommandSink");

            AssertInterfaces(
                "PlayerWeaponController",
                "IPlayerTickCommandSink");
        }


        [Test]
        public void PlayerTickCommands_TracksAndConsumesPendingRequest()
        {
            Type commandsType =
                GetRuntimeType(
                    "PlayerTickCommands");

            object commands =
                Activator.CreateInstance(
                    commandsType);

            AssertProperty(
                commands,
                "HasPending",
                false);

            commandsType
                .GetMethod("RequestCancelAttack")
                .Invoke(commands, null);

            AssertProperty(
                commands,
                "HasPending",
                true);

            MethodInfo consume =
                commandsType.GetMethod(
                    "TryConsumeCancelAttack",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                consume,
                Is.Not.Null);

            Assert.That(
                consume.Invoke(commands, null),
                Is.EqualTo(true));

            AssertProperty(
                commands,
                "HasPending",
                false);
        }


        [Test]
        public void PlayerTickCommands_SetsMovementVelocityOnce()
        {
            Type commandsType =
                GetRuntimeType(
                    "PlayerTickCommands");

            object commands =
                Activator.CreateInstance(
                    commandsType);

            Vector2 expectedVelocity =
                new(-7f, 2.5f);

            commandsType
                .GetMethod("RequestSetMovementVelocity")
                .Invoke(
                    commands,
                    new object[]
                    {
                        expectedVelocity
                    });

            AssertProperty(
                commands,
                "HasPending",
                true);

            MethodInfo consume =
                commandsType.GetMethod(
                    "TryConsumeSetMovementVelocity",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                consume,
                Is.Not.Null);

            object[] arguments =
            {
                Vector2.zero
            };

            Assert.That(
                consume.Invoke(
                    commands,
                    arguments),
                Is.EqualTo(true));

            Assert.That(
                arguments[0],
                Is.EqualTo(expectedVelocity));

            AssertProperty(
                commands,
                "HasPending",
                false);

            arguments[0] = Vector2.one;

            Assert.That(
                consume.Invoke(
                    commands,
                    arguments),
                Is.EqualTo(false));

            Assert.That(
                arguments[0],
                Is.EqualTo(Vector2.zero));
        }


        [Test]
        public void PlayerTickState_ContainsModuleSnapshotContracts()
        {
            Type tickStateType =
                GetRuntimeType(
                    "PlayerTickState");

            AssertPropertyType(
                tickStateType,
                "Health",
                typeof(int));

            AssertPropertyType(
                tickStateType,
                "MaxHealth",
                typeof(int));

            AssertPropertyType(
                tickStateType,
                "Lives",
                typeof(int));

            AssertPropertyType(
                tickStateType,
                "DeathSequence",
                typeof(byte));

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
                "IsAttacking",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "IsCombatMovementLocked",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "SkillAnimationSequence",
                typeof(byte));

            AssertPropertyType(
                tickStateType,
                "SkillAnimationId",
                GetRuntimeType(
                    "PlayerSkillAnimationId"));

            AssertPropertyType(
                tickStateType,
                "HasAim",
                typeof(bool));

            AssertPropertyType(
                tickStateType,
                "AimDirection",
                typeof(Vector2));

            AssertPropertyType(
                tickStateType,
                "HasEquippedWeapon",
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

            PropertyInfo[] properties =
                tickStateType.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .Where(
                    property =>
                        property.GetSetMethod(true) != null)
                .ToArray();

            foreach (PropertyInfo property
                     in properties)
            {
                property.SetValue(
                    tickState,
                    property.Name == "FacingRight"
                        ? false
                        : CreateNonDefaultValue(
                            property.PropertyType));
            }

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

            foreach (PropertyInfo property
                     in properties)
            {
                AssertProperty(
                    tickState,
                    property.Name,
                    property.Name == "FacingRight"
                        ? true
                        : Activator.CreateInstance(
                            property.PropertyType));
            }
        }


        [Test]
        public void PlayerTickState_ResolvesAimFromCapturedOrigin()
        {
            Type tickStateType =
                GetRuntimeType(
                    "PlayerTickState");

            object tickState =
                Activator.CreateInstance(
                    tickStateType);

            tickStateType
                .GetProperty("HasAim")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("HasAimOrigin")
                .SetValue(tickState, true);

            tickStateType
                .GetProperty("AimOriginPosition")
                .SetValue(
                    tickState,
                    new Vector2(2f, 3f));

            Vector2 direction =
                (Vector2)tickStateType
                    .GetMethod("ResolveAimDirectionTo")
                    .Invoke(
                        tickState,
                        new object[]
                        {
                            new Vector2(5f, 7f)
                        });

            Assert.That(
                direction,
                Is.EqualTo(new Vector2(0.6f, 0.8f)));
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


        private static object CreateNonDefaultValue(
            Type type)
        {
            if (type == typeof(bool))
                return true;

            if (type == typeof(byte))
                return (byte)1;

            if (type == typeof(int))
                return 1;

            if (type == typeof(Vector2))
                return Vector2.one;

            if (type.IsEnum)
                return Enum.ToObject(type, 1);

            Assert.Fail(
                $"{type.Name}의 비기본 테스트 값을 정의해야 합니다.");

            return null;
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
