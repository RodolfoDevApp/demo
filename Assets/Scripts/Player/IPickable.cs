using UnityEngine;

public interface IPickable
{
    // 'collector' es, t�picamente, el Player.
    void Collect(GameObject collector);
}
