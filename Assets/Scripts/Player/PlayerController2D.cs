using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;

    Rigidbody2D rb;
    Animator bodyAnim;   // Animator del hijo "Body"
    Vector2 input;
    int lastDir = 0;     // 0=down,1=left,2=right,3=up

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Busca el Animator del Body
        var body = transform.Find("Body");
        if (body) bodyAnim = body.GetComponent<Animator>();
        if (!bodyAnim) bodyAnim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        // Dirección solo si nos movemos, si no, conserva la última
        if (input.sqrMagnitude > 0.0001f) lastDir = ToDir(input);

        // Anims del Body
        if (bodyAnim)
        {
            bodyAnim.SetInteger("Dir", lastDir);
            bodyAnim.SetFloat("Speed", rb ? rb.linearVelocity.magnitude : 0f);
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = input * moveSpeed;
    }

    int ToDir(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return (v.x >= 0f) ? 1 : 2;
        return (v.y >= 0f) ? 3 : 0;
    }
}
