using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("발소리")]
    public AudioClip footstepClip;
    public float stepDistance = 2.9f;
    [Range(0f, 1f)] public float footstepVolume = 0.55f;

    private AudioSource footSource;
    private float stepAccum;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // 바닥에 붙어있게
        }

        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S

        // 플레이어 로컬 기준 이동 (transform.forward/right는 Y축 회전을 따라감)
        Vector3 move = transform.right * h + transform.forward * v;
        Vector3 before = transform.position;
        controller.Move(move * moveSpeed * Time.deltaTime);
        UpdateFootsteps(before);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // 실제로 움직인 거리만큼 쌓아서 일정 거리마다 발소리를 낸다.
    private void UpdateFootsteps(Vector3 before)
    {
        if (footstepClip == null) return;

        Vector3 delta = transform.position - before;
        delta.y = 0f;
        if (!isGrounded || delta.sqrMagnitude < 0.000001f)
        {
            stepAccum = Mathf.Min(stepAccum, stepDistance * 0.5f);
            return;
        }

        stepAccum += delta.magnitude;
        if (stepAccum < stepDistance) return;
        stepAccum = 0f;

        if (footSource == null)
        {
            footSource = gameObject.AddComponent<AudioSource>();
            footSource.playOnAwake = false;
            footSource.spatialBlend = 0f;
        }
        footSource.pitch = Random.Range(0.88f, 1.08f);
        footSource.PlayOneShot(footstepClip, footstepVolume * Random.Range(0.85f, 1f));
    }
}
