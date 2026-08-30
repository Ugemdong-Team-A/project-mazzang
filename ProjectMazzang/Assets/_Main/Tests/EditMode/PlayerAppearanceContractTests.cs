using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class PlayerAppearanceContractTests
    {
        private const string MaryPrefabPath =
            "Assets/_Main/Prefabs/Characters/PC_Mary.prefab";

        private static Assembly RuntimeAssembly =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(
                    assembly =>
                        assembly.GetName().Name ==
                        "Assembly-CSharp");


        [TestCase("AwakeningSkill")]
        [TestCase("UltimateAwakeningSkill")]
        public void AwakeningSkills_ExposeAppearanceThroughContract(
            string skillTypeName)
        {
            Type contractType =
                GetRuntimeType(
                    "IAppearanceModifierSkill");

            Type skillType =
                GetRuntimeType(
                    skillTypeName);

            Assert.That(
                contractType.IsAssignableFrom(
                    skillType),
                Is.True,
                skillTypeName);
        }


        [Test]
        public void Mary_IsTheOnlyPlayerPrefabWithSpriteLibraryAppearanceModule()
        {
            Type moduleType =
                GetRuntimeType(
                    "PlayerSpriteLibraryAppearance");

            string[] prefabPaths =
                AssetDatabase.FindAssets(
                        "t:Prefab",
                        new[]
                        {
                            "Assets/_Main/Prefabs/Characters"
                        })
                    .Select(
                        AssetDatabase.GUIDToAssetPath)
                    .Where(
                        path =>
                            System.IO.Path
                                .GetFileName(path)
                                .StartsWith(
                                    "PC_") ||
                            System.IO.Path
                                .GetFileName(path)
                                .StartsWith(
                                    "PlayerCharacter_"))
                    .ToArray();

            foreach (string prefabPath in prefabPaths)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                bool hasModule =
                    prefab != null &&
                    prefab.GetComponent(
                        moduleType) != null;

                Assert.That(
                    hasModule,
                    Is.EqualTo(
                        prefabPath == MaryPrefabPath),
                    prefabPath);
            }
        }


        [Test]
        public void MaryVisual_HasSpriteLibraryAndResolvers()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    MaryPrefabPath);

            Assert.That(
                prefab,
                Is.Not.Null);

            Type libraryType =
                GetRuntimeType(
                    "UnityEngine.U2D.Animation.SpriteLibrary",
                    "Unity.2D.Animation.Runtime");

            Type resolverType =
                GetRuntimeType(
                    "UnityEngine.U2D.Animation.SpriteResolver",
                    "Unity.2D.Animation.Runtime");

            Assert.That(
                prefab.GetComponentInChildren(
                    libraryType,
                    true),
                Is.Not.Null);

            Assert.That(
                prefab.GetComponentsInChildren(
                    resolverType,
                    true).Length,
                Is.EqualTo(9));
        }


        [Test]
        public void MaryAwakening_AllowsEmptyAppearanceAsset()
        {
            ScriptableObject data =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/_Main/Data/Skill/" +
                    "MaryAwakeningSkill.asset");

            Assert.That(
                data,
                Is.Not.Null);

            SerializedProperty property =
                new SerializedObject(data)
                    .FindProperty(
                        "appearanceLibraryAsset");

            Assert.That(
                property,
                Is.Not.Null);

            Assert.That(
                property.objectReferenceValue,
                Is.Null);
        }


        [Test]
        public void TickState_ResetClearsAppearanceRequest()
        {
            Type stateType =
                GetRuntimeType(
                    "PlayerTickState");

            object state =
                Activator.CreateInstance(
                    stateType);

            PropertyInfo property =
                stateType.GetProperty(
                    "ActiveAppearanceLibraryAsset");

            Assert.That(
                property,
                Is.Not.Null);

            MethodInfo reset =
                stateType.GetMethod(
                    "Reset",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                reset,
                Is.Not.Null);

            ScriptableObject asset =
                ScriptableObject.CreateInstance(
                    property.PropertyType);

            try
            {
                property.SetValue(
                    state,
                    asset);

                reset.Invoke(
                    state,
                    null);

                Assert.That(
                    property.GetValue(state),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    asset);
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
                typeName);

            return type;
        }


        private static Type GetRuntimeType(
            string typeName,
            string assemblyName)
        {
            Type type =
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Single(
                        assembly =>
                            assembly.GetName().Name ==
                            assemblyName)
                    .GetType(typeName);

            Assert.That(
                type,
                Is.Not.Null,
                typeName);

            return type;
        }
    }
}
