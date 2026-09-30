using UnityEngine;
using System;

// World Space 선택지 오브젝트 (DEALER 또는 YOU 텍스트 메시)
// 이 컴포넌트를 DEALER용, YOU용 각각의 3D 오브젝트에 붙입니다.
// MeshRenderer는 꺼두고 BoxCollider만 활성화하여 클릭을 감지합니다.
// InteractableObject를 상속하므로 CamMove의 레이캐스트 호버/클릭이 자동으로 동작합니다.
public class ShootTargetOption : InteractableObject
{
    [Header("Target Option")]
    [SerializeField] private bool isDealer; // true = DEALER, false = YOU

    [Header("Hover Visual (선택지 텍스트 오브젝트)")]
    [SerializeField] private GameObject textObject; // 자식으로 붙어 있는 Text/TMP 오브젝트

    // 클릭 결과를 받을 콜백 (GunObject가 등록)
    // 각 선택지에 목표 Transform을 직접 연결하여 호출합니다.
    [Header("Target Transform")]
    [SerializeField] private Transform targetTransform;
    internal System.Action<Transform> onSelected;

    protected override void Start()
    {
        // InteractableObject의 originalPosition 초기화만 수행
        base.Start();

        // 텍스트는 처음부터 보임 (게임오브젝트 자체가 SetActive로 표시/숨김 제어됨)
        if (textObject != null)
            textObject.SetActive(true);
    }

    // 호버 시 텍스트 강조 (위로 들리는 애니메이션 대신 텍스트 색상 등 원하는 연출로 교체 가능)
    public override void ShowInteractionUI()
    {
        isHovered = true;
        // 살짝 위로 떠오르는 효과 (필요 없으면 제거)
        targetPosition = originalPosition + Vector3.up * 0.05f;
    }

    public override void HideInteractionUI()
    {
        isHovered = false;
        targetPosition = originalPosition;
    }

    public override void Interact()
    {
        onSelected?.Invoke(targetTransform);
    }
}
