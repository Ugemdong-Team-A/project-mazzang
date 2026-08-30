using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class PlayerStatsContractTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");

        private static readonly object[]
            PlayerPrefabCases =
            {
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PC_Mary.prefab",
                    7f,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PlayerCharacter_Knight.prefab",
                    7f,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PlayerCharacter_TestChar.prefab",
                    7f,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PlayerCharacter_Werewolf.prefab",
                    9f,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PlayerCharacter_Witch.prefab",
                    7f,
                    100
                }
            };


        [Test]
        public void StatsInstaller_IsNotATickModule()
        {
            Type installerType =
                GetRuntimeType(
                    "PlayerStatsInstaller");

            Type moduleType =
                GetRuntimeType(
                    "PlayerTickModule");

            Assert.That(
                installerType.IsSubclassOf(
                    moduleType),
                Is.False);
        }


        [Test]
        public void StatsConsumers_UseSafeDefaultsWithoutData()
        {
            Assert.That(
                ResolveFallback<float>(
                    GetRuntimeType(
                        "PlayerMovement"),
                    "ResolveBaseMoveSpeed"),
                Is.EqualTo(7f));

            Assert.That(
                ResolveFallback<int>(
                    GetRuntimeType(
                        "PlayerHealth"),
                    "ResolveBaseMaxHealth"),
                Is.EqualTo(100));
        }


        [TestCaseSource(nameof(PlayerPrefabCases))]
        public void PlayerPrefabs_OwnConfiguredStatsData(
            string prefabPath,
            float expectedMoveSpeed,
            int expectedMaxHealth)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Assert.That(
                prefab,
                Is.Not.Null,
                prefabPath);

            Type installerType =
                GetRuntimeType(
                    "PlayerStatsInstaller");

            Component installer =
                prefab.GetComponent(
                    installerType);

            Assert.That(
                installer,
                Is.Not.Null,
                prefabPath);

            PropertyInfo statsDataProperty =
                installerType.GetProperty(
                    "StatsData");

            Assert.That(
                statsDataProperty,
                Is.Not.Null,
                prefabPath);

            object statsData =
                statsDataProperty.GetValue(
                    installer);

            Assert.That(
                statsData,
                Is.Not.Null,
                prefabPath);

            Assert.That(
                ReadProperty<float>(
                    statsData,
                    "MoveSpeed"),
                Is.EqualTo(expectedMoveSpeed),
                prefabPath);

            Assert.That(
                ReadProperty<int>(
                    statsData,
                    "MaxHealth"),
                Is.EqualTo(expectedMaxHealth),
                prefabPath);

            Type consumerType =
                GetRuntimeType(
                    "IStatsConsumer");

            AssertConsumer(
                prefab,
                consumerType,
                "PlayerMovement",
                prefabPath);

            AssertConsumer(
                prefab,
                consumerType,
                "PlayerHealth",
                prefabPath);
        }


        private static T ResolveFallback<T>(
            System.Type consumerType,
            string methodName)
        {
            MethodInfo method =
                consumerType.GetMethod(
                    methodName,
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                consumerType.Name);

            return (T)method.Invoke(
                null,
                new object[]
                {
                    null
                });
        }


        private static void AssertConsumer(
            GameObject prefab,
            Type consumerType,
            string componentTypeName,
            string prefabPath)
        {
            Component component =
                prefab.GetComponent(
                    GetRuntimeType(
                        componentTypeName));

            Assert.That(
                component,
                Is.Not.Null,
                prefabPath);

            Assert.That(
                consumerType.IsInstanceOfType(
                    component),
                Is.True,
                prefabPath);
        }


        private static T ReadProperty<T>(
            object target,
            string propertyName)
        {
            PropertyInfo property =
                target.GetType().GetProperty(
                    propertyName);

            Assert.That(
                property,
                Is.Not.Null,
                propertyName);

            return (T)property.GetValue(
                target);
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
                typeName);

            return type;
        }
    }
}
