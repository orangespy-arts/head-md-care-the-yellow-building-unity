using UnityEngine;

// 挂在 ToiletMan 角色上（与播放 02-CloseWindow 的 Animator 同一个物体）。
// 动画事件 CloseWindow 在角色上触发，这里接住并转发给窗户上的 ToiletMan 脚本。
public class ToiletManAnimRelay : MonoBehaviour
{
    [Tooltip("拖窗户上的 ToiletMan 脚本")]
    public ToiletMan toiletMan;

    // 方法名必须叫 CloseWindow，和动画事件一致
    public void CloseWindow()
    {
        if (toiletMan != null) toiletMan.CloseWindow();
    }
}
