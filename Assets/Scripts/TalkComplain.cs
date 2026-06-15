using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class CoupleRoom : MonoBehaviour, IPointerClickHandler, IRoomResettable
{
    public Animator couple1Animator;
    public Animator couple2Animator;

    public GameObject[] batch1;
    public GameObject[] batch2;
    public float disappearInterval = 0.3f;
    [Range(0f, 1f)]
    public float disappearAt = 0.67f;

    private int clickCount = 0;
    private bool isPlaying = false;

    void Start()
    {
        GameManager.RegisterInteractive("C3");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;
        Debug.Log("点击触发，clickCount: " + clickCount + " isPlaying: " + isPlaying);
        if (isPlaying) return;
        if (clickCount >= 2) return;

        clickCount++;

        if (clickCount == 1)
        {
            couple1Animator.SetTrigger("Trigger1");
            couple2Animator.SetTrigger("Trigger1");
            StartCoroutine(WaitForAnimation(couple1Animator, batch1));
        }
        else if (clickCount == 2)
        {
            couple1Animator.SetTrigger("Trigger2");
            couple2Animator.SetTrigger("Trigger2");
            StartCoroutine(WaitForAnimation(couple1Animator, batch2));
        }
    }

    private IEnumerator WaitForAnimation(Animator animator, GameObject[] batchToHide)
    {
        isPlaying = true;

        yield return new WaitUntil(() => animator.IsInTransition(0));
        yield return new WaitUntil(() => !animator.IsInTransition(0));

        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
        Debug.Log("clip 长度: " + clipLength + " 状态: " + animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

        yield return new WaitForSeconds(clipLength * disappearAt);

        foreach (GameObject person in batchToHide)
        {
            person.SetActive(false);
            yield return new WaitForSeconds(disappearInterval);
        }

        float remaining = clipLength * (1f - disappearAt);
        yield return new WaitForSeconds(remaining);

        Debug.Log("isPlaying 解锁");
        isPlaying = false;

        // 两次点击的序列都走完，这个房间才算完成
        if (clickCount >= 2)
            GameManager.ReportCompletion("C3");
    }

    public void ResetRoom()
    {
        StopAllCoroutines();
        isPlaying = false;
        clickCount = 0;

        foreach (GameObject person in batch1)
            person.SetActive(true);
        foreach (GameObject person in batch2)
            person.SetActive(true);

        couple1Animator.Rebind();
        couple1Animator.Update(0f);
        couple2Animator.Rebind();
        couple2Animator.Update(0f);
    }
}