using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CatController : MonoBehaviour
{
    [Header("窗台")]
    public Transform[] balconies;

    [Header("朝向参考点")]
    public Transform facingTarget;

    [Header("Screensaver 模式速度（屏保，相机跟猫）")]
    public float saverMinStayTime = 2f;
    public float saverMaxStayTime = 5f;
    [Tooltip("每次跳跃用多少秒（越小越快）")]
    public float saverJumpClipLength = 1f;

    [Header("Interactive 模式速度（交互）")]
    public float interactiveMinStayTime = 1.5f;
    public float interactiveMaxStayTime = 3.5f;
    [Tooltip("每次跳跃用多少秒（越小越快）")]
    public float interactiveJumpClipLength = 0.7f;

    [Header("参数")]
    public float arcHeight = 3f;
    public Vector3 positionOffset = Vector3.zero;

    private Animator animator;
    private Renderer[] renderers;
    private int currentIndex = 2;

    private int[,] coords = new int[,]
    {
        {0, 0}, // A1
        {0, 1}, // A2
        {0, 2}, // A3
        {1, 0}, // B1
        {1, 1}, // B2
        {1, 2}, // B3
        {2, 0}, // C1
        {2, 1}, // C2
        {2, 2}, // C3
    };

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false; // 否则首次进 idle 会把根位移写进 Transform，猫被顶到半空
        renderers = GetComponentsInChildren<Renderer>(true);
        currentIndex = 2;
        transform.position = balconies[currentIndex].position + positionOffset; // 第0帧就到位，避免延迟一帧造成的闪跳
        FaceTarget();
        StartCoroutine(JumpLoop());
    }

    private void FaceTarget()
    {
        if (facingTarget == null) return;
        Vector3 dir = facingTarget.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private bool IsValidJump(int from, int to)
    {
        int colDiff = Mathf.Abs(coords[from, 0] - coords[to, 0]);
        int rowDiff = Mathf.Abs(coords[from, 1] - coords[to, 1]);
        return colDiff > 0 && rowDiff <= 1;
    }

    private IEnumerator JumpLoop()
    {
        // 等一帧，让窗台/建筑先就位再放猫（否则开局读到旧坐标，猫会飘在空中）
        yield return null;
        transform.position = balconies[currentIndex].position + positionOffset;
        FaceTarget();

        while (true)
        {
            // 按当前状态选速度：屏保一套，交互一套
            bool saver = GameManager.Instance == null
                      || GameManager.Instance.State == GameState.Screensaver;
            float minStay   = saver ? saverMinStayTime   : interactiveMinStayTime;
            float maxStay   = saver ? saverMaxStayTime   : interactiveMaxStayTime;
            float jumpLen   = saver ? saverJumpClipLength : interactiveJumpClipLength;

            float stayTime = Random.Range(minStay, maxStay);
            yield return new WaitForSeconds(stayTime);

            List<int> validTargets = new List<int>();
            for (int i = 0; i < balconies.Length; i++)
            {
                if (i != currentIndex && IsValidJump(currentIndex, i))
                    validTargets.Add(i);
            }

            if (validTargets.Count == 0) continue;

            int nextIndex = validTargets[Random.Range(0, validTargets.Count)];

            // 跳跃前转向目标窗台
            Vector3 jumpDir = balconies[nextIndex].position - transform.position;
            jumpDir.y = 0;
            if (jumpDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(jumpDir);

            animator.SetTrigger("DoJump");

            // 抛物线位移
            Vector3 startPos = transform.position;
            Vector3 endPos = balconies[nextIndex].position + positionOffset;
            float elapsed = 0f;

            while (elapsed < jumpLen)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpLen);
                Vector3 flatPos = Vector3.Lerp(startPos, endPos, t);
                flatPos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
                transform.position = flatPos;
                yield return null;
            }

            // 落地
            currentIndex = nextIndex;
            transform.position = endPos;
            FaceTarget();
        }
    }

    // ---- 以下由 GameManager 在 State3 / 循环重置时调用 ----

    // State3 末尾：猫消失（停跳 + 隐藏渲染，不 SetActive 以便协程控制权保留在这里）
    public void Hide()
    {
        StopAllCoroutines();
        SetVisible(false);
    }

    // State4：猫出现在结尾窗台，不重启跳跃循环（由 GameManager 控制时序）
    // offset：结尾落点单独的偏移（不用窗台那套 positionOffset）
    public void AppearAt(Transform perch, Vector3 offset = default)
    {
        StopAllCoroutines();
        if (perch != null) transform.position = perch.position + offset;
        FaceTarget();
        SetVisible(true);
    }

    // 循环重置：回起始窗台（A3）重新开始跳
    public void ResetCat()
    {
        StopAllCoroutines();
        currentIndex = 2;
        transform.position = balconies[currentIndex].position + positionOffset;
        FaceTarget();
        SetVisible(true);
        StartCoroutine(JumpLoop());
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers)
            r.enabled = visible;
    }
}