using UnityEngine;
using UnityEngine.EventSystems;

public class ToiletMan : MonoBehaviour, IPointerClickHandler, IRoomResettable
{
    
   //on click, change animator parameter, opening to true
    [Header("拖角色（人物）的 Animator")]
    public Animator animator;
    private bool hasInteracted = false;
    public Animator windowAnimator;
    public GameObject windows;

    private void Awake()
    {
        if (windowAnimator != null)
            windowAnimator.applyRootMotion = false;
    }

    private void Start()
    {
        GameManager.RegisterInteractive("A1");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;
        if (hasInteracted) return;
        hasInteracted = true;
        animator.SetBool("Closed", true);
        GameManager.ReportCompletion("A1");
    }

    //create public method called CloseWindow, which sets the animator parameter "Closed" to true
    public void CloseWindow()
    {
        Debug.Log("[ToiletMan] CloseWindow called, windowAnimator=" + (windowAnimator != null ? "OK" : "NULL"));
        if (windowAnimator != null)
            windowAnimator.SetTrigger("close");
    }

    public void ResetRoom()
    {
        StopAllCoroutines();
        hasInteracted = false;
        animator.Rebind();
        animator.Update(0f);
        if (windowAnimator != null)
        {
            windowAnimator.Rebind();
            windowAnimator.Update(0f);
            windowAnimator.SetBool("Closed", false);
        }
    }
}
