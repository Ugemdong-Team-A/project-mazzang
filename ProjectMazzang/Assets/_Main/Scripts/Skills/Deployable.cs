using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public abstract class Deployable : NetworkBehaviour
{
    [Networked]
    public NetworkObject Owner { get; protected set; }

    [Networked]
    protected TickTimer Lifetime { get; set; }

    public virtual void Initialize(
        NetworkObject owner,
        float lifetime)
    {
        Owner = owner;
        Lifetime =
            lifetime > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    lifetime)
                : TickTimer.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!Lifetime.IsRunning ||
            !Lifetime.Expired(Runner))
        {
            return;
        }

        OnLifetimeExpired();
        Runner.Despawn(Object);
    }

    protected virtual void OnLifetimeExpired()
    {
    }
}
