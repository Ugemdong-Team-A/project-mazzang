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
            SupportedPlayerPrefabCases =
            {
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PC_Mary.prefab",
                    7f,
                    8f,
                    1,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PC_Aron.prefab",
                    7f,
                    8f,
                    1,
                    100
                },
                new object[]
                {
                    "Assets/_Main/Prefabs/Characters/PC_MasterCharacter.prefab",
                    7f,
                    6f,
                    1,
                    100
                }
            };


        [Test]
        public void LegacyStatsInstaller_IsPassiveAndNotATickModule()
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

            MethodInfo awakeMethod =
                installerType.GetMethod(
                    "Awake",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

            Assert.That(
                awakeMethod,
                Is.Null);
        }


        [TestCaseSource(nameof(SupportedPlayerPrefabCases))]
        public void SupportedPlayerPrefabs_OwnComponentStats(
            string prefabPath,
            float expectedMoveSpeed,
            float expectedJumpSpeed,
            int expectedAirJumps,
            int expectedMaxHealth)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Assert.That(
                prefab,
                Is.Not.Null,
                prefabPath);

            Assert.That(
                prefab.GetComponent(
                    GetRuntimeType(
                        "PlayerStatsInstaller")),
                Is.Null,
                prefabPath);

            Component movement =
                GetRequiredComponent(
                    prefab,
                    "PlayerMovement",
                    prefabPath);

            Component health =
                GetRequiredComponent(
                    prefab,
                    "PlayerHealth",
                    prefabPath);

            AssertSerializedValue(
                movement,
                "moveSpeed",
                expectedMoveSpeed,
                prefabPath);

            AssertSerializedValue(
                movement,
                "jumpSpeed",
                expectedJumpSpeed,
                prefabPath);

            AssertSerializedValue(
                movement,
                "maxAirJumps",
                expectedAirJumps,
                prefabPath);

            AssertSerializedValue(
                health,
                "maxHealth",
                expectedMaxHealth,
                prefabPath);
        }


        private static Component GetRequiredComponent(
            GameObject prefab,
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

            return component;
        }


        private static void AssertSerializedValue(
            Component component,
            string propertyName,
            float expectedValue,
            string prefabPath)
        {
            SerializedProperty property =
                new SerializedObject(
                    component)
                    .FindProperty(
                        propertyName);

            Assert.That(
                property,
                Is.Not.Null,
                propertyName);

            Assert.That(
                property.floatValue,
                Is.EqualTo(expectedValue),
                prefabPath);
        }


        private static void AssertSerializedValue(
            Component component,
            string propertyName,
            int expectedValue,
            string prefabPath)
        {
            SerializedProperty property =
                new SerializedObject(
                    component)
                    .FindProperty(
                        propertyName);

            Assert.That(
                property,
                Is.Not.Null,
                propertyName);

            Assert.That(
                property.intValue,
                Is.EqualTo(expectedValue),
                prefabPath);
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
