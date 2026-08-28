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
        public void PlayerPrefabs_UseUniqueTickSlots()
        {
            Type controllerType =
                GetRuntimeType(
                    "PlayerController");

            Type moduleType =
                GetRuntimeType(
                    "PlayerTickModule");

            Type damageReceiverType =
                GetRuntimeType(
                    "IDamageDealtReceiver");

            Type networkObjectType =
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(
                        assembly =>
                            assembly.GetType(
                                "Fusion.NetworkObject"))
                    .FirstOrDefault(
                        type => type != null);

            PropertyInfo stageProperty =
                moduleType.GetProperty(
                    "Stage");

            PropertyInfo orderProperty =
                moduleType.GetProperty(
                    "Order");

            Assert.That(stageProperty, Is.Not.Null);
            Assert.That(orderProperty, Is.Not.Null);
            Assert.That(orderProperty.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(networkObjectType, Is.Not.Null);

            int controllerCount = 0;

            string[] prefabGuids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[]
                    {
                        "Assets/_Main/Prefabs/Characters"
                    });

            foreach (string prefabGuid
                     in prefabGuids)
            {
                string prefabPath =
                    AssetDatabase.GUIDToAssetPath(
                        prefabGuid);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<
                        GameObject>(
                        prefabPath);

                if (prefab == null)
                    continue;

                Component[] controllers =
                    prefab.GetComponentsInChildren(
                        controllerType,
                        true);

                foreach (Component controller
                         in controllers)
                {
                    controllerCount++;

                    Component ownerObject =
                        controller.GetComponent(
                            networkObjectType);

                    Assert.That(ownerObject, Is.Not.Null);

                    Assert.That(
                        controller
                            .GetComponents<Component>()
                            .Any(
                                damageReceiverType
                                    .IsInstanceOfType),
                        Is.True,
                        $"{prefabPath}의 공격 Source와 " +
                        "피해 보상 수신자가 같은 GameObject에 없습니다.");

                    Component[] modules =
                        controller.GetComponentsInChildren(
                                moduleType,
                                true)
                            .Where(
                                module =>
                                    module.GetComponentInParent(
                                        networkObjectType) == ownerObject)
                            .ToArray();

                    HashSet<string> slots =
                        new();

                    foreach (Component module
                             in modules)
                    {
                        object stage =
                            stageProperty.GetValue(
                                module);

                        int order =
                            (int)orderProperty.GetValue(
                                module);

                        string slot =
                            $"{stage}:{order}";

                        Assert.That(
                            slots.Add(slot),
                            Is.True,
                            $"{prefabPath}의 {controller.name}에서 " +
                            $"Player Tick 순서 {slot}가 겹칩니다.");
                    }

                }
            }

            Assert.That(
                controllerCount,
                Is.GreaterThan(0),
                "검사할 PlayerController 프리팹을 찾지 못했습니다.");
        }


        [Test]
        public void PlayerHealth_ProvidesDamageContract()
        {
            AssertInterfaces(
                "PlayerHealth",
                "IDamageable");
        }


        [Test]
        public void AttackPoseModes_KeepSerializedValues()
        {
            Type poseMode =
                GetRuntimeType(
                    "PlayerAttackPoseMode");

            Type rigMode =
                GetRuntimeType(
                    "PlayerAimRigMode");

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        poseMode,
                        "ProceduralAim")),
                Is.EqualTo(0));

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        poseMode,
                        "AnimationOnly")),
                Is.EqualTo(1));

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        poseMode,
                        "AnimationWithBodyAim")),
                Is.EqualTo(2));

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        rigMode,
                        "Procedural")),
                Is.EqualTo(0));

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        rigMode,
                        "AnimationOnly")),
                Is.EqualTo(1));

            Assert.That(
                Convert.ToInt32(
                    Enum.Parse(
                        rigMode,
                        "AnimationWithBodyAim")),
                Is.EqualTo(2));
        }


        [Test]
        public void PlayerSkillController_CreatesRuntimeSkillsOnEveryPeer()
        {
            Type controllerType =
                GetRuntimeType(
                    "PlayerSkillController");

            MethodInfo spawned =
                controllerType.GetMethod(
                    "Spawned");

            MethodInfo despawned =
                controllerType.GetMethod(
                    "Despawned");

            PropertyInfo skill1 =
                controllerType.GetProperty(
                    "Skill1");

            Assert.That(spawned, Is.Not.Null);
            Assert.That(despawned, Is.Not.Null);
            Assert.That(skill1, Is.Not.Null);

            int controllerCount = 0;

            string[] prefabGuids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[]
                    {
                        "Assets/_Main/Prefabs/Characters"
                    });

            foreach (string prefabGuid
                     in prefabGuids)
            {
                string prefabPath =
                    AssetDatabase.GUIDToAssetPath(
                        prefabGuid);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<
                        GameObject>(
                        prefabPath);

                Component controller =
                    prefab?.GetComponent(
                        controllerType);

                if (controller == null)
                    continue;

                controllerCount++;

                try
                {
                    spawned.Invoke(
                        controller,
                        null);

                    Assert.That(
                        skill1.GetValue(
                            controller),
                        Is.Not.Null,
                        $"{prefabPath}에서 비권위 peer용 " +
                        "런타임 Skill을 생성하지 못했습니다.");
                }
                finally
                {
                    despawned.Invoke(
                        controller,
                        new object[]
                        {
                            null,
                            false
                        });
                }
            }

            Assert.That(
                controllerCount,
                Is.GreaterThan(0));
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

            AssertInterfaces(
                "PlayerSkillController",
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
        public void PlayerTickCommands_SeparatesAndMergesControlLocks()
        {
            Type commandsType =
                GetRuntimeType(
                    "PlayerTickCommands");

            Type controlLockType =
                GetRuntimeType(
                    "PlayerControlLock");

            Assert.That(
                controlLockType.IsDefined(
                    typeof(FlagsAttribute),
                    false),
                Is.True);

            object commands =
                Activator.CreateInstance(
                    commandsType);

            object movement =
                Enum.Parse(
                    controlLockType,
                    "Movement");

            object attack =
                Enum.Parse(
                    controlLockType,
                    "Attack");

            object skill =
                Enum.Parse(
                    controlLockType,
                    "Skill");

            object allCurrentControls =
                Enum.ToObject(
                    controlLockType,
                    Convert.ToByte(movement) |
                    Convert.ToByte(attack) |
                    Convert.ToByte(skill));

            MethodInfo request =
                commandsType.GetMethod(
                    "RequestControlLock");

            request.Invoke(
                commands,
                new[]
                {
                    allCurrentControls,
                    (object)0.5f
                });

            // 같은 종류의 더 짧은 요청은 기존 대기 시간을 줄이지 않는다.
            request.Invoke(
                commands,
                new[]
                {
                    movement,
                    (object)0.2f
                });

            AssertControlLockConsumed(
                commandsType,
                commands,
                "TryConsumeMovementControlLock",
                0.5f);

            AssertProperty(
                commands,
                "HasPending",
                true);

            AssertControlLockConsumed(
                commandsType,
                commands,
                "TryConsumeAttackControlLock",
                0.5f);

            AssertControlLockConsumed(
                commandsType,
                commands,
                "TryConsumeSkillControlLock",
                0.5f);

            AssertProperty(
                commands,
                "HasPending",
                false);

            MethodInfo consumeMovement =
                commandsType.GetMethod(
                    "TryConsumeMovementControlLock",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            object[] secondConsume =
            {
                1f
            };

            Assert.That(
                consumeMovement.Invoke(
                    commands,
                    secondConsume),
                Is.EqualTo(false));

            Assert.That(
                secondConsume[0],
                Is.EqualTo(0f));

            request.Invoke(
                commands,
                new[]
                {
                    skill,
                    (object)0f
                });

            AssertProperty(
                commands,
                "HasPending",
                false);
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
                "IsAttackControlLocked",
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
                "IsSkillControlLocked",
                typeof(bool));

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
        public void AttackData_KeepsCrowdControlDefaults()
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
                        "crowdControl");

                Assert.That(
                    property,
                    Is.Not.Null,
                    "AttackData의 군중 제어 정의가 사라졌습니다.");

                Assert.That(
                    property
                        .FindPropertyRelative("type")
                        .intValue,
                    Is.EqualTo(1),
                    "기본 CC는 HitStun이어야 합니다.");

                Assert.That(
                    property
                        .FindPropertyRelative("duration")
                        .floatValue,
                    Is.EqualTo(0.12f).Within(0.0001f),
                    "기본 경직 시간이 달라졌습니다.");

                Assert.That(
                    property
                        .FindPropertyRelative("activationDelay")
                        .floatValue,
                    Is.EqualTo(0f).Within(0.0001f),
                    "기본 CC는 즉시 발동해야 합니다.");

                Assert.That(
                    property
                        .FindPropertyRelative("stopMovementOnApply")
                        .boolValue,
                    Is.False,
                    "일반 공격은 적중 순간 속도를 강제로 지우지 않습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    attackData);
            }
        }


        [Test]
        public void MeterSkill_KeepsGenericRuntimeContract()
        {
            Type meterSkillType =
                GetRuntimeType(
                    "IMeterSkill");

            foreach (string propertyName in
                     new[]
                     {
                         "MaxMeter",
                         "MeterCost",
                         "PassiveGainPerSecond",
                         "DamageGainPerDamage"
                     })
            {
                AssertPropertyType(
                    meterSkillType,
                    propertyName,
                    typeof(float));
            }

            FieldInfo meterField =
                GetRuntimeType(
                        "SkillSlotRuntimeState")
                    .GetField(
                        "Meter");

            Assert.That(
                meterField?.FieldType,
                Is.EqualTo(
                    typeof(float)));
        }


        [Test]
        public void UltimateAwakeningSkill_CombinesGenericPatterns()
        {
            AssertInterfaces(
                "UltimateAwakeningSkill",
                "IMeterSkill",
                "IDurationSkill",
                "IPlayerStatModifierSkill");

            ScriptableObject data =
                AssetDatabase.LoadAssetAtPath<
                    ScriptableObject>(
                    "Assets/_Main/Data/Skill/" +
                    "UltimateAwakeningSkill.asset");

            Assert.That(data, Is.Not.Null);
            Assert.That(
                data.GetType().Name,
                Is.EqualTo(
                    "UltimateAwakeningSkillData"));

            AssertProperty(
                data,
                "MaxMeter",
                100f);

            AssertProperty(
                data,
                "MeterCost",
                100f);

            AssertProperty(
                data,
                "PassiveGainPerSecond",
                2f);

            AssertProperty(
                data,
                "DamageGainPerDamage",
                1f);

            AssertProperty(
                data,
                "Duration",
                8f);

            object runtimeSkill =
                data.GetType()
                    .GetMethod(
                        "CreateSkill")
                    ?.Invoke(
                        data,
                        null);

            Assert.That(runtimeSkill, Is.Not.Null);
            Assert.That(
                runtimeSkill.GetType().Name,
                Is.EqualTo(
                    "UltimateAwakeningSkill"));
        }


        [Test]
        public void KnightAndSkillSlotPrefab_ProvideMeterTestContent()
        {
            Type controllerType =
                GetRuntimeType(
                    "PlayerSkillController");

            GameObject knight =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                    "Assets/_Main/Prefabs/Characters/" +
                    "PlayerCharacter_Knight.prefab");

            Component controller =
                knight?.GetComponent(
                    controllerType);

            Assert.That(controller, Is.Not.Null);

            SerializedObject serializedController =
                new(
                    controller);

            SerializedProperty mainSkill =
                serializedController.FindProperty(
                    "mainSkill");

            SerializedProperty ultimateSkill =
                serializedController.FindProperty(
                    "ultimateSkill");

            Assert.That(mainSkill, Is.Not.Null);
            Assert.That(ultimateSkill, Is.Not.Null);
            Assert.That(
                mainSkill.objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                mainSkill.objectReferenceValue
                    .GetType().Name,
                Is.EqualTo(
                    "DashSkillData"));
            Assert.That(
                ultimateSkill.objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                ultimateSkill.objectReferenceValue
                    .GetType().Name,
                Is.EqualTo(
                    "UltimateAwakeningSkillData"));


            GameObject slotPrefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                    "Assets/_Main/Prefabs/UI/" +
                    "SkillSlot.prefab");

            Component slotUI =
                slotPrefab?.GetComponent(
                    GetRuntimeType(
                        "SkillSlotUI"));

            Assert.That(slotUI, Is.Not.Null);

            SerializedObject serializedSlot =
                new(
                    slotUI);

            foreach (string propertyName in
                     new[]
                     {
                         "meterRoot",
                         "meterFill",
                         "meterOverlay",
                         "meterAccent",
                         "meterText"
                     })
            {
                SerializedProperty property =
                    serializedSlot.FindProperty(
                        propertyName);

                Assert.That(
                    property,
                    Is.Not.Null);

                Assert.That(
                    property.objectReferenceValue,
                    Is.Not.Null,
                    $"SkillSlot prefab의 {propertyName}이 비어 있습니다.");
            }
        }


        [Test]
        public void DamageInfo_KeepsCrowdControlDefinition()
        {
            Type damageInfoType =
                GetRuntimeType(
                    "DamageInfo");

            ConstructorInfo constructor =
                damageInfoType
                    .GetConstructors()
                    .Single();

            Type crowdControlType =
                GetRuntimeType(
                    "CrowdControlDefinition");

            Type crowdControlKind =
                GetRuntimeType(
                    "CrowdControlType");

            object root =
                Enum.Parse(
                    crowdControlKind,
                    "Root");

            object crowdControl =
                Activator.CreateInstance(
                    crowdControlType,
                    root,
                    2f,
                    0.35f,
                    true);

            object damageInfo =
                constructor.Invoke(
                    new object[]
                    {
                        17,
                        null,
                        new Vector2(3f, 4f),
                        crowdControl
                    });

            AssertProperty(
                damageInfo,
                "Damage",
                17);

            AssertProperty(
                damageInfo,
                "Knockback",
                new Vector2(3f, 4f));

            object storedCrowdControl =
                damageInfoType
                    .GetProperty("CrowdControl")
                    .GetValue(damageInfo);

            AssertProperty(
                storedCrowdControl,
                "Type",
                root);

            AssertProperty(
                storedCrowdControl,
                "Duration",
                2f);

            AssertProperty(
                storedCrowdControl,
                "ActivationDelay",
                0.35f);

            AssertProperty(
                storedCrowdControl,
                "StopMovementOnApply",
                true);
        }


        [Test]
        public void CrowdControlRules_MapSemanticTypesToCurrentLocks()
        {
            Type crowdControlKind =
                GetRuntimeType(
                    "CrowdControlType");

            MethodInfo resolve =
                GetRuntimeType(
                        "CrowdControlRules")
                    .GetMethod(
                        "ResolveLocks",
                        BindingFlags.Public |
                        BindingFlags.Static);

            Assert.That(resolve, Is.Not.Null);

            Assert.That(
                Convert.ToInt32(
                    resolve.Invoke(
                        null,
                        new[]
                        {
                            Enum.Parse(
                                crowdControlKind,
                                "Root")
                        })),
                Is.EqualTo(1));

            Assert.That(
                Convert.ToInt32(
                    resolve.Invoke(
                        null,
                        new[]
                        {
                            Enum.Parse(
                                crowdControlKind,
                                "Stun")
                        })),
                Is.EqualTo(7));
        }


        private static void AssertControlLockConsumed(
            Type commandsType,
            object commands,
            string methodName,
            float expectedDuration)
        {
            MethodInfo consume =
                commandsType.GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                consume,
                Is.Not.Null);

            object[] arguments =
            {
                0f
            };

            Assert.That(
                consume.Invoke(
                    commands,
                    arguments),
                Is.EqualTo(true));

            Assert.That(
                (float)arguments[0],
                Is.EqualTo(expectedDuration)
                    .Within(0.0001f));
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
