using UnityEngine;

public interface IPickable
{
    // 'collector' es, típicamente, el Player.
    void Collect(GameObject collector);
}
