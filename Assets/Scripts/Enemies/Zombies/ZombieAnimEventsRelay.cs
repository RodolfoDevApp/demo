using UnityEngine;

[DisallowMultipleComponent]
public class ZombieAnimEventsRelay : MonoBehaviour
{
    [Header("Asigna uno. Si estan vacios, se buscan en los padres.")]
    public ZombieAxeAI axeTarget;
    public ZombieDaggerAI daggerTarget;
    public ZombieBigAI bigTarget;

    void Reset() { AutoDetectTargets(); }

    void Awake()
    {
        if (!axeTarget || !daggerTarget || !bigTarget) AutoDetectTargets();
    }

    void AutoDetectTargets()
    {
        if (!axeTarget) axeTarget = GetComponentInParent<ZombieAxeAI>();
        if (!daggerTarget) daggerTarget = GetComponentInParent<ZombieDaggerAI>();
        if (!bigTarget) bigTarget = GetComponentInParent<ZombieBigAI>();
    }

    public void OnAnimAttackAHit()
    {
        if (axeTarget) axeTarget.OnAnimAttackAHit();
        if (daggerTarget) daggerTarget.OnAnimAttackAHit();
        if (bigTarget) bigTarget.OnAnimAttackAHit();
    }

    public void OnAnimAttackAEnd()
    {
        if (axeTarget) axeTarget.OnAnimAttackAEnd();
        if (daggerTarget) daggerTarget.OnAnimAttackAEnd();
        if (bigTarget) bigTarget.OnAnimAttackAEnd();
    }

    public void OnAnimAttackBHit()
    {
        if (axeTarget) axeTarget.OnAnimAttackBHit();
        if (daggerTarget) daggerTarget.OnAnimAttackBHit();
        if (bigTarget) bigTarget.OnAnimAttackBHit();
    }

    public void OnAnimAttackBEnd()
    {
        if (axeTarget) axeTarget.OnAnimAttackBEnd();
        if (daggerTarget) daggerTarget.OnAnimAttackBEnd();
        if (bigTarget) bigTarget.OnAnimAttackBEnd();
    }

    public void OnAnimTakeAxe()
    {
        if (axeTarget) axeTarget.OnAnimTakeAxe();
    }
}
