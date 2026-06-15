using UnityEngine;
using UnityEngine.EventSystems;

// A3 房间：点击电灯泡 → 灯亮并播放电台；再点 → 灯灭并暂停。
// 暂停/续播保留播放位置（不从头开始），和 OldWoman 一致。电台循环播放。
public class RadioStation : MonoBehaviour, IPointerClickHandler, IRoomResettable, IExclusiveAudio
{
    [Header("电台音频：直接拖 radio-station.mp3 进来")]
    public AudioClip radioClip;

    [Header("灯泡的 Light（点击控制亮/灭）")]
    public Light bulbLight;

    [Header("灯泡发光（可选：拖灯泡 mesh 的 Renderer，亮时开 emission）")]
    public Renderer bulbRenderer;
    [ColorUsage(true, true)] public Color emissionColor = Color.white;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private AudioSource radioSource;
    private bool isOn = false;
    private bool hasStarted = false;   // 区分首次 Play（从头）和之后的 UnPause（续播）

    private void Awake()
    {
        radioSource = GetComponent<AudioSource>();
        if (radioSource == null) radioSource = gameObject.AddComponent<AudioSource>();
        if (radioClip != null) radioSource.clip = radioClip;
        radioSource.loop = true;        // 电台：亮着就一直循环放
        radioSource.playOnAwake = false;

        SetBulb(false);                 // 初始：灯灭、不放
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameManager.InteractionEnabled) return;

        if (isOn) TurnOff();
        else      TurnOn();
    }

    private void TurnOn()
    {
        AudioExclusivity.SetActive(this);   // 顶下正在播的 oldwoman
        isOn = true;
        SetBulb(true);
        if (radioSource == null) return;

        if (!hasStarted) { radioSource.Play(); hasStarted = true; } // 首次从头放
        else             { radioSource.UnPause(); }                 // 之后从暂停处续
    }

    // 点击熄灭：灯灭 + 暂停，并让出占用
    private void TurnOff()
    {
        DoTurnOff();
        AudioExclusivity.Clear(this);
    }

    // 被 oldwoman 顶下来：同样灯灭 + 暂停（保留位置），占用已归对方，不清
    public void PauseExternally()
    {
        if (!isOn) return;
        DoTurnOff();
    }

    private void DoTurnOff()
    {
        isOn = false;
        SetBulb(false);
        if (radioSource != null) radioSource.Pause();   // 暂停，保留当前位置
    }

    private void SetBulb(bool on)
    {
        if (bulbLight != null) bulbLight.enabled = on;

        if (bulbRenderer != null)
        {
            Material mat = bulbRenderer.material;   // 取实例，避免改到共享材质
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorID, on ? emissionColor : Color.black);
        }
    }

    // 循环重置 / 进入 Interactive 时：灯灭、电台停、下次从头放
    public void ResetRoom()
    {
        isOn = false;
        hasStarted = false;
        if (radioSource != null) radioSource.Stop();
        AudioExclusivity.Clear(this);
        SetBulb(false);
    }
}
