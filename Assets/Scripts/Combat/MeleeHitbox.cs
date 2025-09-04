using UnityEngine;
using System.Collections.Generic;

public class MeleeHitbox : MonoBehaviour
{
    [SerializeField] Collider2D hitbox;   // Trigger
    [SerializeField] float damage = 12f;
    [SerializeField] float knockback = 6f;
    [SerializeField] LayerMask enemyMask;

    HashSet<Collider2D> hitThisSwing = new();

    void Reset()
    {
        if (!hitbox) hitbox = GetComponent<Collider2D>();
        if (enemyMask.value == 0) enemyMask = LayerMask.GetMask("Enemy");
        if (hitbox) hitbox.isTrigger = true;
        if (hitbox) hitbox.enabled = false;
    }

    // Llama estos dos desde Animation Events del swing (o con teclas para test)
    public void AE_MeleeStart() { hitThisSwing.Clear(); if (hitbox) hitbox.enabled = true; }
    public void AE_MeleeEnd() { if (hitbox) hitbox.enabled = false; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!hitbox || !hitbox.enabled) return;
        if (((1 << other.gameObject.layer) & enemyMask.value) == 0) return;
        if (!hitThisSwing.Add(other)) return;

        if (other.TryGetComponent<IDamageable>(out var dmg))
        {
            Vector2 from = transform.position;
            Vector2 to = other.bounds.ClosestPoint(from);
            var info = new DamageInfo(damage, (to - from).normalized, to, DamageKind.Melee, knockback, gameObject, transform.root.gameObject);
            dmg.ApplyDamage(info);
        }
    }
}
