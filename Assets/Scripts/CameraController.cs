using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Screensaver 相机推进")]
    public Vector3 screensaverOffset = new Vector3(-6.46f, 0, 0);
    public float enterTransitionSpeed = 2f;

    [Header("猫跟随")]
    public Transform catTarget;
    public float followSpeed = 3f;
    [Tooltip("屏保跟随时，相机在猫 Y 之上的偏移（正=相机更高）")]
    public float screensaverFollowYOffset = 0f;

    [Header("Ending zoom out（朝向不变，沿后退方向拉远）")]
    [Tooltip("相机往后退的距离（沿默认朝向的反方向）")]
    public float endingPullbackDistance = 5f;
    [Tooltip("zoom out 后相机 Y 的微调（正=抬高，负=降低）")]
    public float endingHeightOffset = 0f;
    [Tooltip("zoom out 用多少秒完成（建议 ≤ GameManager 的 Ending Camera Hold）")]
    public float endingZoomDuration = 3f;

    private Vector3    defaultPosition;
    private Quaternion defaultRotation;

    // Ending zoom out 按时长插值用
    private GameState  lastState = GameState.Screensaver;
    private float      endingElapsed;
    private Vector3    endingStartPos;
    private Quaternion endingStartRot;

    void Start()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        GameState state = GameManager.Instance.State;

        bool justEnteredEnding = (state == GameState.Ending && lastState != GameState.Ending);
        lastState = state;

        if (state == GameState.Ending)
        {
            // 进入 Ending 当帧记录起点，之后按时长插值 → 精确 endingZoomDuration 秒完成
            if (justEnteredEnding)
            {
                endingElapsed  = 0f;
                endingStartPos = transform.position;
                endingStartRot = transform.rotation;
            }
            endingElapsed += Time.deltaTime;
            float t = endingZoomDuration > 0f ? Mathf.Clamp01(endingElapsed / endingZoomDuration) : 1f;
            t = Mathf.SmoothStep(0f, 1f, t);   // 缓入缓出

            // 沿默认朝向的反方向后退 → 纯 zoom out，朝向保持默认
            Vector3 back = defaultRotation * Vector3.back;
            Vector3 endingPos = defaultPosition + back * endingPullbackDistance;
            endingPos.y += endingHeightOffset;   // 单独微调结尾相机高度

            transform.position = Vector3.Lerp(endingStartPos, endingPos, t);
            transform.rotation = Quaternion.Slerp(endingStartRot, defaultRotation, t);
            return;
        }

        Vector3 targetPos;
        if (state == GameState.Screensaver)
        {
            targetPos = defaultPosition + screensaverOffset;

            // Y 和 Z 跟猫，X 只推进不跟猫
            if (catTarget != null)
            {
                targetPos.y = catTarget.position.y + screensaverFollowYOffset;
                targetPos.z = catTarget.position.z;
            }
        }
        else
        {
            targetPos = defaultPosition;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        // Ending 结束后平滑转回默认朝向
        transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, Time.deltaTime * followSpeed);
    }
}