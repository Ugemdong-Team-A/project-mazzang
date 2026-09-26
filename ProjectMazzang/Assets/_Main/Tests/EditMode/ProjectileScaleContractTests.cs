using NUnit.Framework;

namespace ProjectMazzang.Tests
{
    public sealed class ProjectileScaleContractTests
    {
        [Test]
        public void LaunchSettings_KeepBaseRadiusSeparateFromScale()
        {
            ProjectileBaseSettings baseSettings =
                new(
                    16f,
                    2.5f,
                    0f,
                    9.81f,
                    0f,
                    true,
                    0.2f);

            ProjectileStatSnapshot stats =
                new(
                    1f,
                    1f,
                    1f,
                    1f,
                    1.5f,
                    1f);

            ProjectileLaunchSettings launchSettings =
                baseSettings.Resolve(
                    in stats);

            Assert.That(
                launchSettings.BaseCollisionRadius,
                Is.EqualTo(0.2f));

            Assert.That(
                launchSettings.ResolveCollisionRadius(
                    stats.ScaleMultiplier),
                Is.EqualTo(0.3f).Within(0.0001f));
        }


        [Test]
        public void CollisionRadius_UsesIdentityForInvalidScale()
        {
            ProjectileLaunchSettings launchSettings =
                new(
                    16f,
                    baseCollisionRadius: 0.2f);

            Assert.That(
                launchSettings.ResolveCollisionRadius(0f),
                Is.EqualTo(0.2f));
        }
    }
}
