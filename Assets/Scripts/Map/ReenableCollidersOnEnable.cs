using UnityEngine;

[DisallowMultipleComponent]
public class ReenableCollidersOnEnable : MonoBehaviour
{
    public Collider2D[] colliders;

    void OnEnable()
    {
        if (colliders == null) return;
        foreach (var c in colliders) if (c) c.enabled = true;
    }
}
