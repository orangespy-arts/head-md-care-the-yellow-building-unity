using UnityEngine;
using UnityEngine.EventSystems;

public class OldWomanTalk : MonoBehaviour, IPointerClickHandler, IRoomResettable, IExclusiveAudio
{
    [Header("播客音频：直接拖 podcast.mp3 进来即可")]
    public AudioClip podcastClip;

    [Header("拖角色（人物）的 Animator")]
    public Animator animator;

    private AudioSource podcastSource;
    private bool hasCompleted = false;

    // Idle 未开始 / Playing 动画+音频进行中 / Paused 冻结在当前位置
    private enum Phase { Idle, Playing, Paused }
    private Phase phase = Phase.Idle;

    private void Awake()
    {
        // 没有手动挂 AudioSource 就自动建一个；参数强制成播客需要的设置
        podcastSource = GetComponent<AudioSource>();
        if (podcastSource == null) podcastSource = gameObject.AddComponent<AudioSource>();
        if (podcastClip != null) podcastSource.clip = podcastClip;
        podcastSource.loop = false;
        podcastSource.playOnAwake = false;
    }

    private void Start()
    {
        GameManager.RegisterInteractive("B2");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;

        switch (phase)
        {
            case Phase.Idle:    StartTalking(); break;   // 第一下：开始
            case Phase.Playing: Pause();        break;   // 再点：暂停
            case Phase.Paused:  Resume();       break;   // 再点：从暂停处继续
        }
    }

    private void StartTalking()
    {
        AudioExclusivity.SetActive(this);    // 顶下正在播的电台
        phase = Phase.Playing;
        animator.SetBool("Talking", true);   // 拿起电话 → 进入 Calling
        if (podcastSource != null)
        {
            podcastSource.time = 0f;
            podcastSource.Play();
        }
    }

    // 点击暂停：放下电话 + 暂停音频，并让出占用
    private void Pause()
    {
        DoPause();
        AudioExclusivity.Clear(this);
    }

    // 被电台顶下来：同样放下电话 + 暂停（保留位置），但占用已归电台，不清
    public void PauseExternally()
    {
        if (phase != Phase.Playing) return;
        DoPause();
    }

    private void DoPause()
    {
        phase = Phase.Paused;
        animator.SetBool("Talking", false);  // 放下电话回到坐姿
        if (podcastSource != null) podcastSource.Pause();   // 音频停在当前位置
    }

    private void Resume()
    {
        AudioExclusivity.SetActive(this);    // 顶下正在播的电台
        phase = Phase.Playing;
        animator.SetBool("Talking", true);   // 重新拿起电话
        if (podcastSource != null) podcastSource.UnPause(); // 音频从暂停处续播
    }

    private void Update()
    {
        // 只有"播放中"才判断结束：暂停时 isPlaying 同样是 false，必须用 phase 排除
        if (phase == Phase.Playing && podcastSource != null &&
            podcastSource.clip != null && !podcastSource.isPlaying)
        {
            Finish();
        }
    }

    // 音频自然播完 → 动画退场，房间完成
    private void Finish()
    {
        phase = Phase.Idle;
        animator.SetBool("Talking", false);  // 放下电话 → 回到 01-Sit
        if (podcastSource != null) podcastSource.Stop();
        AudioExclusivity.Clear(this);

        if (!hasCompleted)
        {
            hasCompleted = true;
            GameManager.ReportCompletion("B2");
        }
    }

    public void ResetRoom()
    {
        StopAllCoroutines();
        phase = Phase.Idle;
        hasCompleted = false;
        if (podcastSource != null) podcastSource.Stop();
        AudioExclusivity.Clear(this);
        animator.SetBool("Talking", false);
        animator.Rebind();
        animator.Update(0f);
    }
}
