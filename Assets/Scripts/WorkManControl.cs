using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class BotherWorker : MonoBehaviour, IPointerClickHandler, IRoomResettable
{
    [Header("拖角色（人物）的 Animator")]
    public Animator animator;
    private bool hasCompleted = false;
    private bool isAngry = false;
    private int[] idleStates = { 1, 2, 3 };

    private void Start()
    {
        GameManager.RegisterInteractive("A2");
        StartCoroutine(RandomIdleLoop());
    }

  private IEnumerator RandomIdleLoop()
{
    int current = 1;
    animator.SetInteger("State", current);

    while (true)
    {
        if (isAngry)
        {
            yield return null;
            continue;
        }

        // 等 Animator 稳定进入当前 idle 状态（跳过过渡帧）
        yield return null;
        yield return new WaitUntil(() =>
            !animator.IsInTransition(0) || isAngry);

        if (isAngry) continue;

        // 现在读到的才是当前状态的正确长度
        float len = animator.GetCurrentAnimatorStateInfo(0).length;
        float t = 0f;
        while (t < len)
        {
            if (isAngry) break;
            t += Time.deltaTime;
            yield return null;
        }

        if (isAngry) continue;

        int next;
        do {
            next = idleStates[Random.Range(0, idleStates.Length)];
        } while (next == current);
        current = next;
        animator.SetInteger("State", next);
    }
}

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;
        Debug.Log("WorkMan clicked, isAngry=" + isAngry);
        if (isAngry) return;

        isAngry = true;
        animator.SetBool("Angery", true);
        StartCoroutine(AfterAngry());
    }

private IEnumerator AfterAngry()
{
    // 等进入 Angery 状态
    yield return new WaitUntil(() =>
        animator.GetCurrentAnimatorStateInfo(0).IsName("04-Angery"));

    // 按 Angery 动画的实际长度等待，不依赖 normalizedTime
    float angryLength = animator.GetCurrentAnimatorStateInfo(0).length;
    yield return new WaitForSeconds(angryLength);

    if (!hasCompleted)
    {
        hasCompleted = true;
        GameManager.ReportCompletion("A2");
    }

    // 清 bool，配合 04→01 的 Angery==false condition 跳回 idle
    animator.SetBool("Angery", false);
    isAngry = false;
}

    public void ResetRoom()
    {
        StopAllCoroutines();
        isAngry = false;
        hasCompleted = false;
        animator.Rebind();
        animator.Update(0f);
        StartCoroutine(RandomIdleLoop());
    }
}