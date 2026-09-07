using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class SkillPatternSettingsTests
    {
        private ScriptableObject data;
        private SerializedObject serialized;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance(Type.GetType("DashSkillData, Assembly-CSharp", true));
            serialized = new SerializedObject(data);
        }

        [TearDown]
        public void TearDown()
        {
            serialized.Dispose();
            UnityEngine.Object.DestroyImmediate(data);
        }

        [Test]
        public void DisabledPatternsRetainValuesAndDoNotRejectLegacyData()
        {
            serialized.FindProperty("patterns.meter.cost").floatValue = 200f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);

            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);

            serialized.FindProperty("patterns.meter.enabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
            Assert.That(serialized.FindProperty("patterns.meter.cost").floatValue, Is.EqualTo(200f));
        }

        [Test]
        public void ChargeAndMeterCanBeCombinedWithoutChangingEitherOption()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
            serialized.Update();
            Assert.That(serialized.FindProperty("patterns.charge.enabled").boolValue, Is.True);
            Assert.That(serialized.FindProperty("patterns.meter.enabled").boolValue, Is.True);
        }

        [Test]
        public void IndependentPatternsCanBeCombined()
        {
            foreach (string pattern in new[] { "meter", "cast", "duration", "recovery", "actionLock", "statModifier", "appearance" })
                serialized.FindProperty("patterns." + pattern + ".enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [Test]
        public void BehaviorDurationDoesNotRequireDuplicatedTime()
        {
            serialized.FindProperty("patterns.duration.enabled").boolValue = true;
            serialized.FindProperty("patterns.duration.seconds").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
            serialized.FindProperty("patterns.duration.source").enumValueIndex = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void EnabledMeterRejectsInvalidGain(float gain)
        {
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.passiveGainPerSecond").floatValue = gain;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
        }

        [Test]
        public void DisabledMeterIsAbsentFromRuntimeView()
        {
            object skill = CreateRuntime(data);
            object view = skill.GetType().GetProperty("Patterns").GetValue(skill);
            Assert.That(view.GetType().GetProperty("Meter").GetValue(view), Is.Null);
        }

        [Test]
        public void RuntimeDurationResolvesBehaviorInsteadOfStoredSeconds()
        {
            var dash = ScriptableObject.CreateInstance(Type.GetType("DashData, Assembly-CSharp", true));
            try
            {
                using (var dashSerialized = new SerializedObject(dash))
                {
                    dashSerialized.FindProperty("duration").floatValue = 0.25f;
                    dashSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
                serialized.FindProperty("dash").objectReferenceValue = dash;
                serialized.FindProperty("patterns.duration.enabled").boolValue = true;
                serialized.FindProperty("patterns.duration.seconds").floatValue = 7f;
                serialized.FindProperty("patterns.duration.source").enumValueIndex = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                object skill = CreateRuntime(data);
                object view = skill.GetType().GetProperty("Patterns").GetValue(skill);
                PropertyInfo duration = view.GetType().GetProperty("Duration");
                Assert.That(duration.GetValue(view), Is.EqualTo(0.25f));

                serialized.FindProperty("patterns.duration.source").enumValueIndex = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(duration.GetValue(view), Is.EqualTo(7f));

                serialized.FindProperty("patterns.duration.enabled").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(duration.GetValue(view), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dash);
            }
        }

        [Test]
        public void ProjectilePresentationUsesCommonCastSetting()
        {
            var projectile = ScriptableObject.CreateInstance(Type.GetType("ProjectileSkillData, Assembly-CSharp", true));
            try
            {
                using var settings = new SerializedObject(projectile);
                settings.FindProperty("castDuration").floatValue = 9f;
                settings.FindProperty("patterns.cast.enabled").boolValue = true;
                settings.FindProperty("patterns.cast.seconds").floatValue = 0.4f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                object skill = CreateRuntime(projectile);
                PropertyInfo cast = skill.GetType().GetProperty("CastDuration");
                Assert.That(cast.GetValue(skill), Is.EqualTo(0.4f));

                settings.FindProperty("patterns.cast.enabled").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(cast.GetValue(skill), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
            }
        }

        private static object CreateRuntime(ScriptableObject definition)
        {
            object skill = definition.GetType().GetMethod("CreateSkill").Invoke(definition, null);
            // 정적 설정 조회만 검증하므로 NetworkRunner와 대시 초기화는 실행하지 않습니다.
            Type.GetType("Skill, Assembly-CSharp", true).GetProperty("Data")
                .GetSetMethod(true).Invoke(skill, new object[] { definition });
            return skill;
        }

        private bool Validate()
        {
            object[] arguments = { null };
            return (bool)data.GetType().GetMethod("ValidatePatterns", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(data, arguments);
        }
    }
}
