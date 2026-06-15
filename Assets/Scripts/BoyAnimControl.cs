using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class KidBehaviour : MonoBehaviour, IPointerClickHandler, IRoomResettable
{
    [Header("拖角色（人物）的 Animator")]
    public Animator animator;
    private bool hasCompleted = false;
    private bool isPlaying = false;

    private void Start()
    {
        GameManager.RegisterInteractive("B1");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;
        if (isPlaying) return;

        isPlaying = true;
        int waveIndex = Random.Range(3, 6);
        animator.SetInteger("WaveIndex", waveIndex);
        animator.SetTrigger("Trigger");
        StartCoroutine(WaitForSequence());
    }

    private IEnumerator WaitForSequence()
    {
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("01-Up"));

        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("00-idle"));

        if (!hasCompleted)
        {
            hasCompleted = true;
            GameManager.ReportCompletion("B1");
        }

        isPlaying = false;
    }

    public void ResetRoom()
    {
        StopAllCoroutines();
        isPlaying = false;
        hasCompleted = false;
        animator.Rebind();
        animator.Update(0f);
    }
}