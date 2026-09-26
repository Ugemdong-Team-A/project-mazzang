using NUnit.Framework;
using UnityEngine;

namespace ProjectMazzang.Tests
{
    public sealed class ParryProjectileModifiersTests
    {
        [Test]
        public void Identity_DoesNotChangeProjectileValues()
        {
            ParryProjectileModifiers modifiers =
                ParryProjectileModifiers.Identity;

            Assert.That(modifiers.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.KnockbackMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.ScaleMultiplier, Is.EqualTo(1f));
        }


        [Test]
        public void Constructor_ClampsInvalidMultipliers()
        {
            ParryProjectileModifiers modifiers =
                new(-1f, -2f, -3f, 0f);

            Assert.That(modifiers.DamageMultiplier, Is.Zero);
            Assert.That(modifiers.SpeedMultiplier, Is.Zero);
            Assert.That(modifiers.KnockbackMultiplier, Is.Zero);
            Assert.That(modifiers.ScaleMultiplier, Is.EqualTo(0.01f));
        }


        [Test]
        public void ParryData_ProvidesConfiguredDefaults()
        {
            ParryData data =
                ScriptableObject.CreateInstance<ParryData>();

            try
            {
                ParryProjectileModifiers modifiers =
                    data.ProjectileModifiers;

                Assert.That(modifiers.DamageMultiplier, Is.EqualTo(1f));
                Assert.That(modifiers.SpeedMultiplier, Is.EqualTo(1.15f));
                Assert.That(modifiers.KnockbackMultiplier, Is.EqualTo(1f));
                Assert.That(modifiers.ScaleMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
