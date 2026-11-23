using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public float moveSpeed = 6f, jumpForce = 12f, fireCooldown = 0.4f, range = 15f;
    public int damage = 20;
    public Transform groundCheck;
    public LayerMask groundMask;
    public LineRenderer tracer;

    public NetworkVariable<int> health = new(100);
    public NetworkVariable<bool> hasWeapon = new(false);

    Rigidbody2D rb;
    float moveInput, nextFire, lastSent;
    bool jumpQueued;
    int facing = 1;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    // Owner: read input and send it to the server
    void Update()
    {
        if (!IsOwner) return;
        float m = Input.GetAxisRaw("Horizontal");
        bool jump = Input.GetButtonDown("Jump");
        bool fire = Input.GetButtonDown("Fire1");
        if (m != lastSent || jump || fire)
        {
            lastSent = m;
            SendInputServerRpc(m, jump, fire);
        }
    }

    [ServerRpc]
    void SendInputServerRpc(float move, bool jump, bool fire)
    {
        moveInput = Mathf.Clamp(move, -1f, 1f);
        if (moveInput != 0) facing = moveInput > 0 ? 1 : -1;
        if (jump) jumpQueued = true;
        if (fire) TryFire();
    }

    // Server: apply movement
    void FixedUpdate()
    {
        if (!IsServer) return;
        if (GameManager.Instance.MatchOver) { rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); return; }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, 0.15f, groundMask);
        if (jumpQueued && grounded) rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpQueued = false;
    }

    void TryFire()
    {
        if (!hasWeapon.Value || Time.time < nextFire || GameManager.Instance.MatchOver) return;
        nextFire = Time.time + fireCooldown;

        Vector2 dir = Vector2.right * facing;
        Vector2 origin = (Vector2)transform.position + dir * 0.6f;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, range);
        Vector2 end = hit ? hit.point : origin + dir * range;

        if (hit && hit.collider.TryGetComponent(out Player target)) target.TakeDamage(damage);
        ShotClientRpc(origin, end);
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer || health.Value <= 0) return;
        health.Value = Mathf.Max(0, health.Value - amount);
        if (health.Value == 0) GameManager.Instance.EndMatch(OwnerClientId);
    }

    [ClientRpc]
    void ShotClientRpc(Vector2 a, Vector2 b)
    {
        tracer.SetPosition(0, a);
        tracer.SetPosition(1, b);
        tracer.enabled = true;
        CancelInvoke(nameof(HideTracer));
        Invoke(nameof(HideTracer), 0.05f);
    }
    void HideTracer() { tracer.enabled = false; }

    void OnGUI()
    {
        if (!IsOwner) return;
        GUI.Label(new Rect(10, 10, 300, 25), $"HP: {health.Value}   Weapon: {(hasWeapon.Value ? "yes" : "no")}");
    }
}