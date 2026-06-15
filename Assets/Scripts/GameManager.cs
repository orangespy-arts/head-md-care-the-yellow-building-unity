using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

// 四状态体验循环（阶段3：Dissolving 已实装，Ending 仍为占位）
//   Screensaver  相机跟猫，等观众点击（开机默认）
//   Interactive  九宫格固定机位，房间可交互
//   Dissolving   房间内容依次消失 → 猫消失
//   Ending       zoom out 结尾（占位）→ 重置回 Screensaver
public enum GameState { Screensaver, Interactive, Dissolving, Ending }

public interface IRoomResettable
{
    void ResetRoom();
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State2 闲置分流（按有没有玩过区分）")]
    [Tooltip("没完成任何房间时，闲置这么久 → 直接回屏保（跳过结局）")]
    public float idleToScreensaverThreshold = 20f;
    [Tooltip("完成过房间后，闲置这么久 → 走结局（消失 → Ending）")]
    public float idleDissolveThreshold = 45f;
    [Tooltip("全部房间交互完成后，停这么久再开始")]
    public float allCompleteDelay = 3f;
    public bool mouseMoveCountsAsActivity = true;

    [Header("九个房间（拖 empty 父物体，下面放模型；消失顺序 = 数组顺序）")]
    public GameObject[] rooms = new GameObject[9];

    [Header("State3 消失节奏")]
    [Tooltip("房间与房间之间的间隔（秒）")]
    public float roomDissolveInterval = 0.8f;
    [Tooltip("房间内子物体逐个消失的间隔（秒）")]
    public float objectInterval = 0.25f;
    [Tooltip("最后一个房间清空后，到猫消失的停顿")]
    public float catDisappearDelay = 1f;
    [Tooltip("猫消失后，到进入 State4 的停顿")]
    public float afterDissolveHold = 1.5f;

    [Header("引用")]
    public CatController cat;

    [Header("State4 结尾")]
    public Transform endingCatPerch;
    [Tooltip("结局猫咪出现的桌子（另一栋房间，后期拖进来；为空则用上面的 endingCatPerch）")]
    public Transform endingCatTable;
    [Tooltip("结局猫咪相对落点的偏移（单独调，和窗台跳跃的 offset 无关）")]
    public Vector3 endingCatOffset;
    public float fadeToBlackDuration = 3f;
    [Tooltip("结尾要熄灭的『黄房子』灯光：拖照亮黄房子的那一/几盏灯（卧室的灯不要拖）")]
    public Light[] buildingLights;
    [Tooltip("黄房子灯光淡到的最终强度（0=熄灭）")]
    public float endingBuildingLightIntensity = 0f;
    [Tooltip("（可选）结尾全屏黑覆盖层；留空则不用")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("（推荐）结尾用的 3D 黑板：放窗外挡住黄房子，材质用 URP/Unlit + Surface=Transparent；alpha 0→endingBlackoutAlpha")]
    public Renderer blackoutQuad;
    [Range(0f, 1f)]
    [Tooltip("黑板最终透明度（1=纯黑挡死，调小可留一点黄房子透出来）")]
    public float endingBlackoutAlpha = 1f;
    public float holdBlackDuration   = 2f;
    public float endingCameraHold    = 3f;

    [Header("测试开关（上展前必须关掉）")]
    [Tooltip("勾上：跳过屏保，Play 直接进 Interactive")]
    public bool debugSkipScreensaver = false;
    [Tooltip("勾上：Play 直接跳到结局（跳过消失，用来调结尾相机）")]
    public bool debugSkipToEnding = false;

    public GameState State { get; private set; } = GameState.Screensaver;

    private float idleTimer = 0f;
    private bool hasEngaged = false;   // 本次交互态点过至少一次 = 真正玩过
    private int enterFrame = -1;
    private Vector2 lastMousePos;
    private float[] initialBuildingLightIntensities;
    private MaterialPropertyBlock blackoutMPB;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    // 房间注册/完成（阶段2接入）
    private readonly HashSet<string> registeredRooms = new HashSet<string>();
    private readonly HashSet<string> completedRooms = new HashSet<string>();

    // 从 Screensaver 点进来的那一下不传给房间
    public static bool InteractionEnabled =>
        Instance != null
        && Instance.State == GameState.Interactive
        && Time.frameCount != Instance.enterFrame;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 记录每个子物体的初始 active 状态，循环重置时按原样复原
    // （比如某些道具本来就是关着的，不能复原成全开）
    private readonly Dictionary<GameObject, bool> initialActive = new Dictionary<GameObject, bool>();

    void Start()
    {
        lastMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        foreach (var room in rooms)
        {
            if (room == null) continue;
            foreach (Transform child in room.transform)
                initialActive[child.gameObject] = child.gameObject.activeSelf;
        }

        if (buildingLights != null)
        {
            initialBuildingLightIntensities = new float[buildingLights.Length];
            for (int i = 0; i < buildingLights.Length; i++)
                if (buildingLights[i] != null)
                    initialBuildingLightIntensities[i] = buildingLights[i].intensity;
        }

        SetBlackoutAlpha(0f); // 开局强制透明，淡出之前一直透明（不依赖材质自带 alpha）

        if (debugSkipToEnding) StartCoroutine(EndingSequence()); // 调结尾相机用，优先于上面那个
        else if (debugSkipScreensaver) EnterInteractive();
    }

    void Update()
    {
        // 任何指针按下（鼠标/触控板 tap/触摸/笔）
        bool pressed = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;

        switch (State)
        {
            case GameState.Screensaver:
                if (pressed) EnterInteractive();
                break;

            case GameState.Interactive:
                bool active = pressed;
                if (pressed) hasEngaged = true;   // 点过任何一下就算玩过
                if (Mouse.current != null)
                {
                    Vector2 currentMousePos = Mouse.current.position.ReadValue();
                    if (mouseMoveCountsAsActivity && (currentMousePos - lastMousePos).sqrMagnitude > 1f)
                        active = true;
                    lastMousePos = currentMousePos;
                }

                if (active) idleTimer = 0f;
                else idleTimer += Time.deltaTime;

                // 按"有没有真正玩过"分流：点过→走结局；没点过→直接回屏保
                if (hasEngaged)
                {
                    if (idleTimer >= idleDissolveThreshold)
                        BeginDissolve(0f, "闲置超时（玩过 → 走结局）");
                }
                else
                {
                    if (idleTimer >= idleToScreensaverThreshold)
                    {
                        Debug.Log("[GameManager] Interactive 闲置且没玩过 → 直接回屏保");
                        ResetAll();
                    }
                }
                break;

            // Dissolving / Ending 由协程自动推进，忽略输入
        }
    }

    private void EnterInteractive()
    {
        State = GameState.Interactive;
        idleTimer = 0f;
        hasEngaged = false;
        enterFrame = Time.frameCount;
        ResetAllRooms(); // 等同旧版退出 screensaver 时的重置
        Debug.Log("[GameManager] → Interactive");
    }

    // ---- 房间注册 / 完成上报（阶段2：各房间 Start 注册、完成时上报）----

    public static void RegisterInteractive(string roomId)
    {
        if (Instance != null) Instance.registeredRooms.Add(roomId);
    }

    public static void ReportCompletion(string roomId)
    {
        if (Instance == null || Instance.State != GameState.Interactive) return;
        if (!Instance.completedRooms.Add(roomId)) return; // 重复上报忽略（如 ToiletMan 反复点击）
        Debug.Log($"[GameManager] 房间完成 {roomId}（{Instance.completedRooms.Count}/{Instance.registeredRooms.Count}）");

        if (Instance.registeredRooms.Count > 0 &&
            Instance.completedRooms.Count >= Instance.registeredRooms.Count)
            Instance.BeginDissolve(Instance.allCompleteDelay, "全部房间完成");
    }

    // ---- State3 / State4（阶段1：占位循环，把四个状态真实跑通）----

    private void BeginDissolve(float delay, string reason)
    {
        if (State != GameState.Interactive) return; // 防双触发
        State = GameState.Dissolving;
        Debug.Log($"[GameManager] → Dissolving（{reason}）");
        StartCoroutine(DissolveSequence(delay));
    }

    private IEnumerator DissolveSequence(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 逐房间消失（顺序 = Inspector 里 rooms 数组顺序，空槽位跳过）
        // 每个房间自己开协程，房间之间可以重叠消失
        foreach (var room in rooms)
        {
            if (room == null) continue;
            StartCoroutine(DissolveRoom(room));
            yield return new WaitForSeconds(roomDissolveInterval);
        }

        yield return new WaitForSeconds(catDisappearDelay);
        if (cat != null) cat.Hide();
        yield return new WaitForSeconds(afterDissolveHold);

        yield return StartCoroutine(EndingSequence());
    }

    // State4 结尾：猫落窗台 → 相机 zoom out → 淡黑 → 循环重置
    // （debug 也能直接跳进来，不经过消失阶段）
    private IEnumerator EndingSequence()
    {
        State = GameState.Ending;
        Debug.Log("[GameManager] → Ending");

        // 有桌子就落桌子（另一栋房间），没拖就回退到窗台
        Transform catSpot = endingCatTable != null ? endingCatTable : endingCatPerch;
        if (cat != null && catSpot != null) cat.AppearAt(catSpot, endingCatOffset);
        yield return new WaitForSeconds(endingCameraHold);

        yield return StartCoroutine(FadeToBlack());
        yield return new WaitForSeconds(holdBlackDuration);

        ResetAll();
    }

    private IEnumerator FadeToBlack()
    {
        float elapsed    = 0f;
        float[] startI   = CaptureBuildingIntensities();
        float startAlpha = fadeCanvasGroup != null ? fadeCanvasGroup.alpha : 0f;

        while (elapsed < fadeToBlackDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / fadeToBlackDuration);
            if (buildingLights != null)
                for (int i = 0; i < buildingLights.Length; i++)
                    if (buildingLights[i] != null)
                        buildingLights[i].intensity = Mathf.Lerp(startI[i], endingBuildingLightIntensity, t);
            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t); // 可选：连了才会全屏黑
            SetBlackoutAlpha(Mathf.Lerp(0f, endingBlackoutAlpha, t)); // 3D 黑板淡入到目标透明度
            yield return null;
        }
        if (buildingLights != null)
            for (int i = 0; i < buildingLights.Length; i++)
                if (buildingLights[i] != null) buildingLights[i].intensity = endingBuildingLightIntensity;
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;
        SetBlackoutAlpha(endingBlackoutAlpha);
    }

    // 3D 黑板透明度：a=0 全透明，a=1 纯黑（用 MaterialPropertyBlock，不动共享材质）
    private void SetBlackoutAlpha(float a)
    {
        if (blackoutQuad == null) return;
        if (blackoutMPB == null) blackoutMPB = new MaterialPropertyBlock();
        blackoutQuad.GetPropertyBlock(blackoutMPB);
        blackoutMPB.SetColor(BaseColorID, new Color(0f, 0f, 0f, a));
        blackoutQuad.SetPropertyBlock(blackoutMPB);
    }

    private float[] CaptureBuildingIntensities()
    {
        if (buildingLights == null) return null;
        var arr = new float[buildingLights.Length];
        for (int i = 0; i < buildingLights.Length; i++)
            if (buildingLights[i] != null) arr[i] = buildingLights[i].intensity;
        return arr;
    }

    // 房间内子物体按层级顺序逐个消失（只动直接子物体；要整组一起消失就把它们包在一个子 empty 里）
    private IEnumerator DissolveRoom(GameObject room)
    {
        if (room.transform.childCount == 0)
        {
            Debug.LogWarning($"[GameManager] {room.name} 没有任何子物体！要消失的模型必须放在它下面一层。" +
                "另外确认拖进 Rooms 的是 Hierarchy 里的场景物体，不是 Project 里的 prefab。");
            yield break;
        }

        int hidden = 0;
        foreach (Transform child in room.transform)
        {
            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                hidden++;
                yield return new WaitForSeconds(objectInterval);
            }
        }
        Debug.Log($"[GameManager] {room.name} 已清空（隐藏了 {hidden} 个子物体）");
    }

    private void ResetAll()
    {
        StopAllCoroutines(); // 终止可能还在跑的 DissolveRoom

        completedRooms.Clear();
        idleTimer = 0f;

        // 子物体按初始 active 状态复原
        foreach (var room in rooms)
        {
            if (room == null) continue;
            foreach (Transform child in room.transform)
            {
                bool wasActive;
                child.gameObject.SetActive(
                    initialActive.TryGetValue(child.gameObject, out wasActive) ? wasActive : true);
            }
        }

        ResetAllRooms();
        if (cat != null) cat.ResetCat();
        if (buildingLights != null && initialBuildingLightIntensities != null)
            for (int i = 0; i < buildingLights.Length; i++)
                if (buildingLights[i] != null)
                    buildingLights[i].intensity = initialBuildingLightIntensities[i];
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f; // 收回全屏黑（若用了）
        SetBlackoutAlpha(0f);                                    // 黑板收回透明

        State = GameState.Screensaver;
        Debug.Log("[GameManager] → Screensaver（循环重置）");
    }

    private void ResetAllRooms()
    {
        var all = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in all)
            if (mb is IRoomResettable r) r.ResetRoom();
    }
}
