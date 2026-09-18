using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
            serialized.FindProperty("patterns.meter.consumeMode").enumValueIndex = 1;
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
                PropertyInfo duration = view.GetType().GetProperty("ActiveDuration");
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
        public void ActionPhaseDurationsUseOneCommonTimeline()
        {
            serialized.FindProperty("patterns.cast.enabled").boolValue = true;
            serialized.FindProperty("patterns.cast.seconds").floatValue = 0.4f;
            serialized.FindProperty("patterns.duration.enabled").boolValue = true;
            serialized.FindProperty("patterns.duration.source").enumValueIndex = 0;
            serialized.FindProperty("patterns.duration.seconds").floatValue = 0.25f;
            serialized.FindProperty("patterns.recovery.enabled").boolValue = true;
            serialized.FindProperty("patterns.recovery.seconds").floatValue = 0.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            object view = RuntimeView();
            MethodInfo getDuration = view.GetType().GetMethod("GetPhaseDuration");
            Type phaseType = Type.GetType("SkillUsePhase, Assembly-CSharp", true);

            Assert.That(getDuration.Invoke(view, new[] { Enum.ToObject(phaseType, 1) }), Is.EqualTo(0.4f));
            Assert.That(getDuration.Invoke(view, new[] { Enum.ToObject(phaseType, 2) }), Is.EqualTo(0.25f));
            Assert.That(getDuration.Invoke(view, new[] { Enum.ToObject(phaseType, 3) }), Is.EqualTo(0.2f));
            Assert.That(
                view.GetType().GetProperty("TotalUseDuration").GetValue(view),
                Is.EqualTo(0.85f).Within(0.0001f));
        }

        [TestCase("ProjectileSkillData", "castDuration")]
        [TestCase("ProjectileSkillData", "recoveryDuration")]
        [TestCase("DeployableSkillData", "castDuration")]
        [TestCase("DeployableSkillData", "recoveryDuration")]
        [TestCase("AwakeningSkillData", "duration")]
        [TestCase("AwakeningSkillData", "moveSpeedMultiplier")]
        [TestCase("AwakeningSkillData", "appearanceLibraryAsset")]
        [TestCase("UltimateAwakeningSkillData", "maxMeter")]
        [TestCase("UltimateAwakeningSkillData", "meterCost")]
        [TestCase("UltimateAwakeningSkillData", "passiveGainPerSecond")]
        [TestCase("UltimateAwakeningSkillData", "damageGainPerDamage")]
        [TestCase("UltimateAwakeningSkillData", "duration")]
        [TestCase("UltimateAwakeningSkillData", "moveSpeedMultiplier")]
        [TestCase("UltimateAwakeningSkillData", "appearanceLibraryAsset")]
        public void ConcreteSkillDataDoesNotDuplicateCommonPatternFields(
            string typeName,
            string fieldName)
        {
            FieldInfo field =
                Type.GetType(typeName + ", Assembly-CSharp", true)
                    .GetField(
                        fieldName,
                        BindingFlags.DeclaredOnly |
                        BindingFlags.Instance |
                        BindingFlags.NonPublic |
                        BindingFlags.Public);

            Assert.That(field, Is.Null);
        }

        [TestCase(0, false, true)]
        [TestCase(0, true, false)]
        [TestCase(1, false, true)]
        [TestCase(1, true, true)]
        public void MeterGainUsesSamePolicyForEverySource(int refillMode, bool windowOpen, bool expected)
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.meterRechargePolicy").enumValueIndex = refillMode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            object view = RuntimeView();
            Assert.That(view.GetType().GetMethod("CanGainMeter").Invoke(view, new object[] { 0, windowOpen }),
                Is.EqualTo(expected));
            Assert.That(view.GetType().GetMethod("CanGainMeter").Invoke(view, new object[] { 2, windowOpen }),
                Is.False);
        }

        [TestCase(0, false, false)]
        [TestCase(0, true, false)]
        [TestCase(1, false, true)]
        [TestCase(1, true, false)]
        public void ChargeWindowPaysMeterOnlyAtTimedWindowEntry(int rechargeMode, bool open, bool expected)
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = rechargeMode;
            serialized.FindProperty("patterns.charge.useWindowMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.useWindowDuration").floatValue = 5f;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            object view = RuntimeView();
            Assert.That(view.GetType().GetMethod("NeedsMeterPayment").Invoke(view, new object[] { open }),
                Is.EqualTo(expected));
            // 제한 시간이 대시의 개별 Active 시간으로 들어가지 않는다.
            Assert.That(view.GetType().GetProperty("ActiveDuration").GetValue(view), Is.EqualTo(0f));
            Assert.That(view.GetType().GetProperty("ChargeWindowDuration").GetValue(view), Is.EqualTo(5f));
        }

        [Test]
        public void TimedWithoutWindowRequiresMeterOnEveryUse()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            object view = RuntimeView();
            Assert.That(view.GetType().GetMethod("NeedsMeterPayment").Invoke(view, new object[] { true }), Is.True);
        }

        [Test]
        public void PassiveRequiresMeterButTimedAllowsZeroInitialChargesAndZeroRechargeTime()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.initialCharges").intValue = 0;
            serialized.FindProperty("patterns.charge.rechargeDuration").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [Test]
        public void MeterRechargeIgnoresMeterUsePaymentSettings()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 0;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.maxMeter").floatValue = 50f;
            serialized.FindProperty("patterns.meter.requiredMeter").floatValue = 100f;
            serialized.FindProperty("patterns.meter.consumeMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.meter.cost").floatValue = 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(Validate(), Is.True);
        }

        [Test]
        public void MeterRechargeStillValidatesSharedMeterSettings()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 0;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.passiveGainPerSecond").floatValue = -1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(Validate(), Is.False);
        }

        [Test]
        public void MeterUseValidatesOnlySelectedConsumeMode()
        {
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.maxMeter").floatValue = 50f;
            serialized.FindProperty("patterns.meter.requiredMeter").floatValue = 50f;
            serialized.FindProperty("patterns.meter.cost").floatValue = 100f;
            serialized.FindProperty("patterns.meter.consumeMode").enumValueIndex = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);

            serialized.FindProperty("patterns.meter.consumeMode").enumValueIndex = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
        }

        [Test]
        public void MeterRechargeIgnoresTimedRechargeSettings()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 0;
            serialized.FindProperty("patterns.charge.initialCharges").intValue = 255;
            serialized.FindProperty("patterns.charge.rechargeDuration").floatValue = -1f;
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(Validate(), Is.True);
        }

        [TestCase("statModifier")]
        [TestCase("appearance")]
        public void ActiveOnlyPatternRequiresDuration(string pattern)
        {
            serialized.FindProperty("patterns." + pattern + ".enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);

            serialized.FindProperty("patterns.duration.enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [Test]
        public void TimedChargeWindowRequiresExplicitTime()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.useWindowMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.useWindowDuration").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);

            serialized.FindProperty("patterns.charge.useWindowDuration").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [Test]
        public void ChargeWindowDoesNotReplaceActiveDuration()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.useWindowMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.useWindowDuration").floatValue = 5f;
            serialized.FindProperty("patterns.duration.enabled").boolValue = true;
            serialized.FindProperty("patterns.duration.source").enumValueIndex = 0;
            serialized.FindProperty("patterns.duration.seconds").floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            object view = RuntimeView();
            Assert.That(view.GetType().GetProperty("ActiveDuration").GetValue(view), Is.EqualTo(0.25f));
            Assert.That(view.GetType().GetProperty("ChargeWindowDuration").GetValue(view), Is.EqualTo(5f));
        }

        [Test]
        public void PersistentChargesDoNotExposeAUseWindow()
        {
            serialized.FindProperty("patterns.charge.enabled").boolValue = true;
            serialized.FindProperty("patterns.charge.rechargeMode").enumValueIndex = 1;
            serialized.FindProperty("patterns.charge.useWindowMode").enumValueIndex = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            object view = RuntimeView();
            Assert.That(view.GetType().GetProperty("UsesChargeWindow").GetValue(view), Is.False);
            Assert.That(view.GetType().GetProperty("ChargeWindowDuration").GetValue(view), Is.EqualTo(0f));
        }

        [Test]
        public void ChargeWindowBorderBuildsFourNonBlockingSegments()
        {
            var slotObject = new GameObject("SkillSlot", typeof(RectTransform));
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            try
            {
                iconObject.transform.SetParent(slotObject.transform, false);
                Type slotType = Type.GetType("SkillSlotUI, Assembly-CSharp", true);
                Component slot = slotObject.AddComponent(slotType);
                Image icon = iconObject.GetComponent<Image>();

                slotType.GetField("iconImage", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(slot, icon);
                slotType.GetMethod("EnsureChargeWindowBorder", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(slot, null);

                var border = (GameObject)slotType
                    .GetField("_chargeWindowBorderRoot", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(slot);
                var segments = (Image[])slotType
                    .GetField("_chargeWindowBorderSegments", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(slot);

                Assert.That(border, Is.Not.Null);
                Assert.That(border.transform.parent, Is.EqualTo(icon.transform));
                Assert.That(segments, Has.Length.EqualTo(4));
                foreach (Image segment in segments)
                {
                    Assert.That(segment, Is.Not.Null);
                    Assert.That(segment.raycastTarget, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(slotObject);
            }
        }

        private object RuntimeView()
        {
            object skill = CreateRuntime(data);
            return skill.GetType().GetProperty("Patterns").GetValue(skill);
        }

        [TestCase("ChargeSettings", "meterRechargePolicy", "resetByMeterMode")]
        [TestCase("MeterSettings", "consumeMode", "comsumeMode")]
        public void RenamedSettingsPreserveSerializedFieldAliases(string typeName, string fieldName, string oldName)
        {
            FieldInfo field = Type.GetType(typeName + ", Assembly-CSharp", true)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            var alias = field.GetCustomAttribute<UnityEngine.Serialization.FormerlySerializedAsAttribute>();
            Assert.That(alias, Is.Not.Null);
            Assert.That(alias.oldName, Is.EqualTo(oldName));
        }

        [TestCase("SkillChargeRechargeMode", "Meter", 0)]
        [TestCase("SkillChargeRechargeMode", "Timed", 1)]
        [TestCase("SkillMeterRechargePolicy", "Full", 0)]
        [TestCase("SkillMeterRechargePolicy", "OneByOne", 1)]
        [TestCase("SkillChargeUseWindowMode", "Persistent", 0)]
        [TestCase("SkillChargeUseWindowMode", "Timed", 1)]
        public void RenamedEnumsPreserveSerializedValues(string typeName, string name, int value)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp", true);
            Assert.That(Convert.ToInt32(Enum.Parse(type, name)), Is.EqualTo(value));
        }

        [Test]
        public void UnreachableMeterRequirementIsRejected()
        {
            serialized.FindProperty("patterns.meter.enabled").boolValue = true;
            serialized.FindProperty("patterns.meter.maxMeter").floatValue = 50f;
            serialized.FindProperty("patterns.meter.cost").floatValue = 50f;
            serialized.FindProperty("patterns.meter.requiredMeter").floatValue = 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.False);
            serialized.FindProperty("patterns.meter.requiredMeter").floatValue = 50f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Validate(), Is.True);
        }

        [TestCase("ChargeSettings", "maxCharges", "최대 보유 횟수")]
        [TestCase("ChargeSettings", "rechargeMode", "횟수 보충 방식")]
        [TestCase("MeterSettings", "consumeMode", "사용 후 처리")]
        [TestCase("SkillDurationSettings", "source", "시간 결정 방식")]
        [TestCase("SkillStatSettings", "damageTaken", "받는 피해 배율")]
        public void PatternFieldsExposeHumanReadableInspectorNames(
            string typeName,
            string fieldName,
            string expectedName)
        {
            FieldInfo field = Type.GetType(typeName + ", Assembly-CSharp", true)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            var displayName = field.GetCustomAttribute<InspectorNameAttribute>();

            Assert.That(displayName, Is.Not.Null);
            Assert.That(displayName.displayName, Is.EqualTo(expectedName));
        }

        [TestCase("SkillChargeRechargeMode", "Meter", "게이지가 차면 보충")]
        [TestCase("SkillChargeRechargeMode", "Timed", "시간이 지나면 보충")]
        [TestCase("SkillMeterConsumeMode", "None", "유지")]
        [TestCase("SkillMeterConsumeMode", "Cost", "지정량 차감")]
        [TestCase("SkillDurationSource", "Behavior", "스킬 동작에서 결정")]
        public void PatternEnumsExposeHumanReadableInspectorNames(
            string typeName,
            string valueName,
            string expectedName)
        {
            FieldInfo value = Type.GetType(typeName + ", Assembly-CSharp", true)
                .GetField(valueName, BindingFlags.Public | BindingFlags.Static);
            var displayName = value.GetCustomAttribute<InspectorNameAttribute>();

            Assert.That(displayName, Is.Not.Null);
            Assert.That(displayName.displayName, Is.EqualTo(expectedName));
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
