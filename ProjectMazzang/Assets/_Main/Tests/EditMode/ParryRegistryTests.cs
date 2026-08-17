using Fusion;
using NUnit.Framework;
using UnityEngine;

public sealed class ParryRegistryTests
{
    [Test]
    public void IncomingProjectileInsideArc_IsReflectedTowardAim()
    {
        GameObject ownerObject = new("Parry Owner");
        NetworkObject owner = ownerObject.AddComponent<NetworkObject>();
        TestVolume volume = new(owner);
        TestParryable projectile = new(
            Vector2.left,
            null);

        ParryRegistry.Register(volume);

        try
        {
            bool parried = ParryRegistry.TryParry(
                projectile,
                new Vector2(1.5f, 0f),
                new Vector2(0.5f, 0f));

            Assert.That(parried, Is.True);
            Assert.That(projectile.LastHit.Owner, Is.SameAs(owner));
            Assert.That(projectile.LastHit.Direction.x, Is.GreaterThan(0.99f));
            Assert.That(volume.SuccessCount, Is.EqualTo(1));
        }
        finally
        {
            ParryRegistry.Unregister(volume);
            Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void ProjectileOutsideArc_IsIgnored()
    {
        GameObject ownerObject = new("Parry Owner");
        NetworkObject owner = ownerObject.AddComponent<NetworkObject>();
        TestVolume volume = new(owner);
        TestParryable projectile = new(
            Vector2.down,
            null);

        ParryRegistry.Register(volume);

        try
        {
            bool parried = ParryRegistry.TryParry(
                projectile,
                new Vector2(0f, 1.5f),
                new Vector2(0f, 0.5f));

            Assert.That(parried, Is.False);
            Assert.That(volume.SuccessCount, Is.Zero);
        }
        finally
        {
            ParryRegistry.Unregister(volume);
            Object.DestroyImmediate(ownerObject);
        }
    }

    private sealed class TestParryable : IParryable
    {
        public TestParryable(Vector2 velocity, NetworkObject source)
        {
            ParryVelocity = velocity;
            ParrySource = source;
        }

        public Vector2 ParryVelocity { get; }
        public NetworkObject ParrySource { get; }
        public ParryHit LastHit { get; private set; }

        public bool TryParry(in ParryHit hit)
        {
            LastHit = hit;
            return true;
        }
    }

    private sealed class TestVolume : IParryVolume
    {
        public TestVolume(NetworkObject owner)
        {
            ParryOwner = owner;
        }

        public bool IsParryActive => true;
        public NetworkObject ParryOwner { get; }
        public Vector2 ParryOrigin => Vector2.zero;
        public Vector2 ParryDirection => Vector2.right;
        public float ParryRadius => 2f;
        public float ParryHalfAngle => 55f;
        public float ParryAimInfluence => 1f;
        public float ParrySpeedMultiplier => 1f;
        public int SuccessCount { get; private set; }

        public void OnParrySuccess(Vector2 point)
        {
            SuccessCount++;
        }
    }
}
