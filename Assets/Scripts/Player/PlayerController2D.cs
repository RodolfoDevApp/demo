using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;

    Rigidbody2D rb;
    Animator bodyAnim;   // Animator del hijo "Body"
    Vector2 input;
    // Mapping: 0=down, 1=right, 2=left, 3=up
    int lastDir = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        if (input.sqrMagnitude > 0.0001f) lastDir = ToDir(input);

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
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return (v.x >= 0f) ? 1 : 2; // right/left
        return (v.y >= 0f) ? 3 : 0;                                      // up/down
    }
}
