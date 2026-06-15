using UnityEngine;

// 互斥音频：同一时间只允许一个在播。
// 谁开始/恢复播放就调 SetActive，把上一个正在播的"顶下来"（暂停，保留播放位置）。
public interface IExclusiveAudio
{
    void PauseExternally(); // 被别的音频顶下来：暂停自己（动画/灯随之，保留播放位置）
}

public static class AudioExclusivity
{
    private static IExclusiveAudio current;

    // 进入 Play 时重置（防止关掉 Domain Reload 时残留上一次的引用）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    // 开始/恢复播放时调用：暂停上一个正在播的
    public static void SetActive(IExclusiveAudio who)
    {
        if (current != null && current != who)
            current.PauseExternally();
        current = who;
    }

    // 自己暂停/停止时调用：若登记的是自己则清空
    public static void Clear(IExclusiveAudio who)
    {
        if (current == who) current = null;
    }
}
