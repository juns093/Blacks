using UnityEngine;
using UnityEngine.EventSystems;

// 마우스를 올리면 그 줄을 선택하게 해 주는 보조 컴포넌트 (RetroOptionsMenu가 자동으로 붙임)
public class MenuCursorHover : MonoBehaviour, IPointerEnterHandler
{
    private System.Action<int> onHover;
    private int index;

    public void Setup(System.Action<int> callback, int i)
    {
        onHover = callback;
        index = i;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke(index);
    }
}
