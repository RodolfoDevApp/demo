using UnityEngine;

[DisallowMultipleComponent]
public class HitscanTracer2D : MonoBehaviour
{
    public LineRenderer lr;
    public float life = 0.08f;
    public float startWidth = 0.04f;
    public float endWidth = 0.0f;

    float t, lifeInv;

    void Reset()
    {
        lr = GetComponent<LineRenderer>();
        if (!lr) lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = 1f;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingLayerName = "FX";
        lr.useWorldSpace = true;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.alignment = LineAlignment.View;
    }

    public void Show(Vector3 a, Vector3 b, Color c, float dur)
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        a.z = 0f; b.z = 0f;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, 0f);
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;

        life = Mathf.Max(0.01f, dur);
        lifeInv = 1f / life;
        t = life;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (t <= 0f) { gameObject.SetActive(false); return; }
        t -= Time.deltaTime;
        float a = Mathf.Clamp01(t * lifeInv);
        var sc = lr.startColor; sc.a = a;
        var ec = lr.endColor; ec.a = 0f;
        lr.startColor = sc;
        lr.endColor = ec;
        if (t <= 0f) gameObject.SetActive(false);
    }
}
