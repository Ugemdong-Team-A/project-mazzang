using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class PlayerArchitectureContractTests
    {
        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [Test]
        public void WeaponController_DoesNotReferencePlayerAim()
        {
            MonoScript weaponControllerScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/_Main/Scripts/Player/" +
                    "PlayerWeaponController.cs");

            Assert.That(
                weaponControllerScript,
                Is.Not.Null);

            Assert.That(
                weaponControllerScript.text,
                Does.Not.Contain(
                    "PlayerAim"));
        }


        [Test]
        public void TickModules_DoNotNamePeerConcreteTypes()
        {
            Type moduleBaseType =
                GetRuntimeType(
                    "PlayerTickModule");

            Type[] moduleTypes =
                RuntimeAssembly
                    .GetTypes()
                    .Where(
                        type =>
                            !type.IsAbstract &&
                            moduleBaseType.IsAssignableFrom(type))
                    .ToArray();

            MonoScript[] playerScripts =
                AssetDatabase.FindAssets(
                        "t:MonoScript",
                        new[]
                        {
                            "Assets/_Main/Scripts/Player"
                        })
                    .Select(
                        AssetDatabase.GUIDToAssetPath)
                    .Select(
                        AssetDatabase.LoadAssetAtPath<MonoScript>)
                    .Where(
                        script =>
                            script != null)
                    .ToArray();

            foreach (Type moduleType in moduleTypes)
            {
                MonoScript moduleScript =
                    playerScripts.SingleOrDefault(
                        script =>
                            script.GetClass() ==
                            moduleType);

                Assert.That(
                    moduleScript,
                    Is.Not.Null,
                    $"{moduleType.Name}의 MonoScript를 찾을 수 없습니다.");

                foreach (Type peerType in moduleTypes)
                {
                    if (peerType == moduleType)
                        continue;

                    Assert.That(
                        moduleScript.text,
                        Does.Not.Contain(peerType.Name),
                        $"{moduleType.Name}이 동료 모듈 " +
                        $"{peerType.Name}의 구체 타입을 직접 언급합니다.");
                }
            }
        }


        [Test]
        public void PlayerTickState_ProvidesBodyAimLimitSnapshot()
        {
            Type tickStateType =
                GetRuntimeType(
                    "PlayerTickState");

            AssertPropertyType(
                tickStateType,
                "BodyAimAngle",
                typeof(float));

            AssertPropertyType(
                tickStateType,
                "MaxBodyAimAngle",
                typeof(float));

            Assert.That(
                tickStateType.GetMethod(
                    "ResolveLimitedAimDirection"),
                Is.Not.Null);
        }


        [Test]
        public void PlayerPrefabs_AuthorWeaponSocketUnderResolvedAimPivot()
        {
            Type aimType =
                GetRuntimeType(
                    "PlayerAim");

            Type weaponControllerType =
                GetRuntimeType(
                    "PlayerWeaponController");

            string[] prefabPaths =
            {
                "Assets/_Main/Prefabs/Characters/" +
                "PC_Mary.prefab",
                "Assets/_Main/Prefabs/Characters/" +
                "PlayerCharacter_TestChar.prefab"
            };

            foreach (string prefabPath in prefabPaths)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                Component aim =
                    prefab?.GetComponent(
                        aimType);

                Component weaponController =
                    prefab?.GetComponent(
                        weaponControllerType);

                Assert.That(
                    aim,
                    Is.Not.Null,
                    $"{prefabPath}에 PlayerAim이 없습니다.");

                Assert.That(
                    weaponController,
                    Is.Not.Null,
                    $"{prefabPath}에 PlayerWeaponController가 없습니다.");

                Transform resolvedAimPivot =
                    new SerializedObject(aim)
                        .FindProperty("resolvedAimPivot")
                        ?.objectReferenceValue as Transform;

                Transform weaponSocket =
                    new SerializedObject(weaponController)
                        .FindProperty("weaponSocket")
                        ?.objectReferenceValue as Transform;

                Assert.That(
                    resolvedAimPivot,
                    Is.Not.Null,
                    $"{prefabPath}에 ResolvedAimPivot이 할당되지 않았습니다.");

                Assert.That(
                    weaponSocket,
                    Is.Not.Null,
                    $"{prefabPath}에 WeaponSocket이 할당되지 않았습니다.");

                Assert.That(
                    weaponSocket.parent,
                    Is.SameAs(resolvedAimPivot),
                    $"{prefabPath}의 WeaponSocket은 ResolvedAimPivot의 직접 자식이어야 합니다.");

                Assert.That(
                    weaponSocket.localPosition.sqrMagnitude,
                    Is.LessThan(0.000001f),
                    $"{prefabPath}의 WeaponSocket 로컬 위치는 원점이어야 합니다.");

                Assert.That(
                    Quaternion.Angle(
                        weaponSocket.localRotation,
                        Quaternion.Euler(
                            0f,
                            0f,
                            -90f)),
                    Is.LessThan(0.01f),
                    $"{prefabPath}의 WeaponSocket 로컬 회전은 -90도여야 합니다.");

                Assert.That(
                    Vector3.Distance(
                        weaponSocket.localScale,
                        Vector3.one),
                    Is.LessThan(0.0001f),
                    $"{prefabPath}의 WeaponSocket 로컬 크기는 1이어야 합니다.");
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
                Is.Not.Null,
                $"Assembly-CSharp에서 {typeName}을 찾을 수 없습니다.");

            return type;
        }


        private static void AssertPropertyType(
            Type type,
            string propertyName,
            Type expectedType)
        {
            PropertyInfo property =
                type.GetProperty(
                    propertyName);

            Assert.That(
                property,
                Is.Not.Null,
                $"{type.Name}.{propertyName}을 찾을 수 없습니다.");

            Assert.That(
                property.PropertyType,
                Is.EqualTo(expectedType));
        }
    }
}
