using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerClickHandler
{
    [Header("UI")]
    public Image icon;
    public TMP_Text amountText;
    public Image highlight;

    InventoryUI owner;
    InventoryUI.GridKind kind;
    int index;

    static Canvas rootCanvas;
    static Image dragGhost;
    static SlotUI dragSource;

    public void Setup(InventoryUI owner, InventoryUI.GridKind kind, int index)
    {
        this.owner = owner;
        this.kind = kind;
        this.index = index;

        if (!rootCanvas) rootCanvas = owner.rootCanvas ? owner.rootCanvas : GetComponentInParent<Canvas>();
        Bind(owner.GetStack(kind, index));
    }

    public void Bind(InventoryRuntime.Stack s)
    {
        bool has = !s.IsEmpty && s.item && s.item.icon;
        if (icon) { icon.enabled = has; icon.sprite = has ? s.item.icon : null; }

        if (amountText)
        {
            bool showCount = has && s.item.maxStack > 1 && s.amount > 1;
            amountText.gameObject.SetActive(showCount);
            if (showCount) amountText.text = s.amount.ToString();
        }
    }

    public void SetHighlight(bool on, Color c)
    {
        if (!highlight) return;
        highlight.enabled = on;
        highlight.color = c;
    }

    // -------- Double Click --------
    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount >= 2 && owner != null)
            owner.OnSlotDoubleClick(kind, index);
    }

    // -------- Drag & Drop --------
    public void OnBeginDrag(PointerEventData e)
    {
        var s = owner.GetStack(kind, index);
        if (s.IsEmpty) return;

        dragSource = this;

        if (!dragGhost)
        {
            var go = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(rootCanvas.transform, false);
            dragGhost = go.GetComponent<Image>();
            dragGhost.raycastTarget = false;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        dragGhost.sprite = s.item.icon;
        dragGhost.enabled = true;
        dragGhost.rectTransform.sizeDelta = icon ? icon.rectTransform.sizeDelta : new Vector2(32, 32);
        dragGhost.rectTransform.position = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (dragGhost) dragGhost.rectTransform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (dragGhost) dragGhost.enabled = false;
        dragSource = null;
    }

    public void OnDrop(PointerEventData e)
    {
        if (dragSource == null || dragSource == this) return;
        owner.SwapOrMerge(dragSource.kind, dragSource.index, kind, index);
    }
}
