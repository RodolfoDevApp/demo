using System.Collections.Generic;
using UnityEngine;

public class TracerPool2D : MonoBehaviour
{
    public HitscanTracer2D tracerPrefab;
    public int prewarm = 32;

    readonly Queue<HitscanTracer2D> pool = new();

    void Awake()
    {
        for (int i = 0; i < Mathf.Max(1, prewarm); i++)
        {
            var t = Instantiate(tracerPrefab, transform);
            t.gameObject.SetActive(false);
            pool.Enqueue(t);
        }
    }

    public HitscanTracer2D Get()
    {
        if (pool.Count > 0)
        {
            var t = pool.Dequeue();
            t.gameObject.SetActive(true);
            return t;
        }
        return Instantiate(tracerPrefab, transform);
    }

    public void Return(HitscanTracer2D t)
    {
        if (!t) return;
        t.gameObject.SetActive(false);
        pool.Enqueue(t);
    }
}
