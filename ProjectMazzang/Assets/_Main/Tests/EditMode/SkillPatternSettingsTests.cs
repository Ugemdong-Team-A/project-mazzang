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
        public void ResourceConflictIsReportedWithoutChangingEitherOption()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
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

        private bool Validate()
        {
            object[] arguments = { null };
            return (bool)data.GetType().GetMethod("ValidatePatterns", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(data, arguments);
        }
    }
}
