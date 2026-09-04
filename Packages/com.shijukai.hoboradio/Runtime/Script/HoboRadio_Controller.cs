using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HoboRadio_Controller : UdonSharpBehaviour
{
    [Header("--- 同期設定 ---")]
    [Tooltip("チェックを入れるとチャンネル切り替えがグローバルになります(電源、音量は同期しません)")]
    [SerializeField] bool isGlobal = true;

    [Header("--- 自動起動設定 ---")]
    [Tooltip("チェックを入れるとワールドに入った時に電源が自動でONになります")]
    [SerializeField] public bool radioPowerOn = true;

    private const int ChannelCount = 4;

    [Header("--- デフォルトチャンネル設定 ---")]
    [Tooltip("電源を入れた時に最初に流れるチャンネルを設定できます")]
    [Range(0, ChannelCount - 1)]
    [UdonSynced, SerializeField] public int currentChannelIndex = 0;


    [Header("--- 開発用（設定不要） ---")]
    [Tooltip("開発の際に使用する設定欄です。不具合の原因になりますのでお手を触れないようにお願いします")]
    [SerializeField] public Animator radioAnimator;

    //ChannelSettings
    [HideInInspector] public VRCUrl[] channels = new VRCUrl[ChannelCount];
    [HideInInspector] private int loadedChannelIndex = -1;

    //AnimationSettings
    
    [SerializeField, HideInInspector] private float[] channelDialValues = new float[] { 0.416f, 0.43f, 0.45f, 0.47f };

    //UISettings
    [HideInInspector] public TextMeshProUGUI channelText;
    [HideInInspector] public TextMeshProUGUI statusText;
    [HideInInspector] public GameObject debugCanvas;

    //AudioSettings
    [HideInInspector] public AudioSource powerSwitchSE;
    [HideInInspector] public AudioSource channelNoiseSE;
    [HideInInspector] public BaseVRCVideoPlayer videoPlayer;
    [HideInInspector] public UdonBehaviour infoFetcher;

    // Internal State
    private const int NoiseFadeNone = 0;
    private const int NoiseFadeInMode = 1;
    private const int NoiseFadeOutMode = 2;
    private int noiseFadeMode = NoiseFadeNone;
    private int noiseFadeStep;
    private bool isNoiseFadeStepScheduled = false;
    private float noiseFadeOutDelayRemaining;
    private bool isNoiseFadeOutDelayActive = false;
    private bool isNoiseFadeOutDelayStepScheduled = false;
    private int lastServerHour = -1;
    private int lastDisplayedSecond = -1;
    private bool waitingPlay = false;
    private bool isInteractedLocked = false;
    private bool hasSyncedInitial = false;
    private float videoLoadStartTime;
    private int retryCount = 0;
    private bool isRetryScheduled = false;
    private const int MaxRetryCount = 3;
    private const float RetryDelay = 5f;
    private const float LoadingTimeout = 45f;

    private void Start()
    {
        Debug.Log("[HoboRadio] Controller Started");

        if (!isGlobal || Networking.IsOwner(gameObject))
        {
            hasSyncedInitial = true;
        }

        // 初期化
        if (radioPowerOn)
        {
            if (radioAnimator != null) radioAnimator.SetTrigger("PowerOn");
            UpdateVisuals();

            // Global設定かつオーナーなら初期ロード実行
            if (!isGlobal || Networking.IsOwner(gameObject))
            {
                RequestSerialization();
                SendCustomEventDelayedSeconds(nameof(_ApplyChannel), 2f);
            }
        }
    }

    private void Update()
    {
        DateTime serverTime = Networking.GetNetworkDateTime();
        int currentHr = serverTime.Hour;
        float currentSec = serverTime.Minute * 60f + serverTime.Second;

        if (lastServerHour == -1)
        {
            lastServerHour = currentHr;
        }

        // 1時間ごとの自動更新（電源ON時のみ）
        if (radioPowerOn && lastServerHour != currentHr && (!isGlobal || hasSyncedInitial))
        {
            lastServerHour = currentHr;
            float jitterDelay = UnityEngine.Random.Range(0f, 5f);
            Debug.Log($"[HoboRadio] Periodic Update Triggered: currentHr/Min={currentHr}");
            SendCustomEventDelayedSeconds(nameof(_ApplyChannel), jitterDelay);
        }

        // 再生時間の表示更新
        if (videoPlayer != null && videoPlayer.IsPlaying && statusText != null)
        {
            int totalSec = (int)videoPlayer.GetTime();
            if (totalSec != lastDisplayedSecond)
            {
                lastDisplayedSecond = totalSec;
                statusText.text = $"{totalSec / 60:00}:{totalSec % 60:00}";
            }
        }
    }

    #region --- Interaction ---

    public void InteractButtonPower()
    {
        if (isInteractedLocked) return;
        LockInteraction();

        if (powerSwitchSE != null) powerSwitchSE.Play();

        if (radioPowerOn) // OFFにする処理
        {
            videoPlayer.Stop();
            CancelPendingNoiseFadeOut();
            StopChannelNoise();
            if (radioAnimator != null) radioAnimator.SetTrigger("PowerOff");
            if (channelText != null) channelText.text = "";
            radioPowerOn = false;
            waitingPlay = false;

            // Fetcherに表示クリアを通知
            if (infoFetcher != null) infoFetcher.SendCustomEvent("ClearDisplay");
        }
        else // ONにする処理
        {
            radioPowerOn = true;
            hasSyncedInitial = true;
            if (radioAnimator != null) radioAnimator.SetTrigger("PowerOn");
            lastDisplayedSecond = -1;
            _ApplyChannel(); // ApplyChannel内でRequestUpdateが呼ばれ画面が点灯
        }
    }

    public void InteractSwitchChannel()
    {
        if (!radioPowerOn || isInteractedLocked || waitingPlay) return;
        LockInteraction();

        if (powerSwitchSE != null) powerSwitchSE.Play();

        if (isGlobal)
        {
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            currentChannelIndex = (currentChannelIndex + 1) % channels.Length;
            RequestSerialization();
            _ApplyChannel(); // オーナー自身も即時適用
        }
        else
        {
            currentChannelIndex = (currentChannelIndex + 1) % channels.Length;
            _ApplyChannel();
        }
    }

    public void InteractButtonDebug()
    {
        if (debugCanvas != null) debugCanvas.SetActive(!debugCanvas.activeSelf);
    }

    private void LockInteraction()
    {
        if (!isGlobal) return;
        isInteractedLocked = true;
        SendCustomEventDelayedSeconds(nameof(_UnlockInteraction), 3f);
    }

    public void _UnlockInteraction() => isInteractedLocked = false;

    #endregion

    #region --- Logic & Sync ---

    public override void OnDeserialization()
    {
        if (!isGlobal) return;
        bool isFirstSync = !hasSyncedInitial;
        hasSyncedInitial = true;

        if (isFirstSync || loadedChannelIndex != currentChannelIndex)
        {
            _ApplyChannel();
        }
    }

    public void _ApplyChannel()
    {
        loadedChannelIndex = currentChannelIndex;
        UpdateVisuals();

        Debug.Log($"[HoboRadio] ApplyChannel: powerOn={radioPowerOn}, waitingPlay={waitingPlay}, currentCh={currentChannelIndex}, isOwner={Networking.IsOwner(gameObject)}");

        if (!radioPowerOn) return;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        waitingPlay = false;

        CancelPendingNoiseFadeOut();

        // Fetcherへの通知
        if (infoFetcher != null) infoFetcher.SendCustomEvent("RequestUpdate");

        // ビデオロード
        if (!waitingPlay)
        {
            retryCount = 0;
            isRetryScheduled = false;
            _ExecuteLoad();
        }

        NoiseFadeIn();
    }

    public void _ExecuteLoad()
    {
        if (!radioPowerOn) return;

        Debug.Log($"[HoboRadio] LoadURL Executed (Attempt {retryCount + 1}): {channels[currentChannelIndex]}");
        videoPlayer.LoadURL(channels[currentChannelIndex]);
        waitingPlay = true;
        isRetryScheduled = false;
        videoLoadStartTime = Time.timeSinceLevelLoad;
        SendCustomEventDelayedSeconds(nameof(_CheckLoadingTimeout), LoadingTimeout);
    }

    private void UpdateVisuals()
    {
        // 3Dモデル：針の移動
        if (radioAnimator != null && currentChannelIndex < channelDialValues.Length)
        {
            radioAnimator.SetFloat("Float_Needle_Position", channelDialValues[currentChannelIndex]);
        }

        // UI：チャンネル番号表示
        if (channelText != null)
        {
            channelText.text = $"CH{(currentChannelIndex + 1):00}";
        }
    }

    public override void OnVideoReady()
    {
        if (!waitingPlay) return;
        waitingPlay = false;
        isRetryScheduled = false;

        Debug.Log($"[HoboRadio] OnVideoReady: ready={videoPlayer.IsReady} dur={videoPlayer.GetDuration()}");

        // 再生開始
        float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
        videoPlayer.SetTime(syncTime);
        videoPlayer.Play();

        if (statusText != null) statusText.text = "";

        StartNoiseFadeOutDelay(3f);
        SendCustomEventDelayedSeconds(nameof(_ReSyncSeek), 30f); // 30秒後に微調整
    }

    public void _ReSyncSeek()
    {
        if (videoPlayer.IsPlaying)
        {
            float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
            videoPlayer.SetTime(syncTime);
        }
    }

    public void _CheckLoadingTimeout()
    {
        if (!waitingPlay) return;

        if (Time.timeSinceLevelLoad - videoLoadStartTime < LoadingTimeout - 0.5f) return;

        Debug.LogWarning($"[HoboRadio] Loading Timeout Detected (Attempt {retryCount + 1})");
        HandleRetry();
    }

    private void HandleRetry()
    {
        if (!waitingPlay || isRetryScheduled) return;

        if (retryCount < MaxRetryCount)
        {
            retryCount++;
            isRetryScheduled = true;
            Debug.Log($"[HoboRadio] Retrying load in {RetryDelay}s ({retryCount}/{MaxRetryCount})...");
            if (statusText != null) statusText.text = $"RETRY {retryCount}/{MaxRetryCount}";

            videoPlayer.Stop();
            SendCustomEventDelayedSeconds(nameof(_ExecuteLoad), RetryDelay);
        }
        else
        {
            Debug.LogError("[HoboRadio] Load Failed: Max retry limit reached.");
            waitingPlay = false;
            isRetryScheduled = false;
            videoPlayer.Stop();
            CancelPendingNoiseFadeOut();
            NoiseFadeOut();
            if (statusText != null) statusText.text = "LOAD ERROR";
        }
    }

    #endregion

    #region --- Audio Effects ---

    public void NoiseFadeIn()
    {
        if (channelNoiseSE == null) return;
        CancelPendingNoiseFadeOut();
        noiseFadeMode = NoiseFadeInMode;
        noiseFadeStep = 0;
        channelNoiseSE.volume = 0f;
        if (!channelNoiseSE.isPlaying) channelNoiseSE.Play();
        ScheduleNoiseFadeStep();
    }

    public void NoiseFadeOut()
    {
        if (channelNoiseSE == null) return;
        CancelPendingNoiseFadeOut();
        noiseFadeMode = NoiseFadeOutMode;
        noiseFadeStep = 0;
        ScheduleNoiseFadeStep();
    }

    public void _NoiseFadeStep()
    {
        isNoiseFadeStepScheduled = false;

        if (channelNoiseSE == null || noiseFadeMode == NoiseFadeNone) return;

        noiseFadeStep++;
        float fadeProgress = noiseFadeStep / 10f;

        if (noiseFadeMode == NoiseFadeInMode)
        {
            channelNoiseSE.volume = Mathf.Lerp(0f, 1f, fadeProgress);

            if (noiseFadeStep < 10)
            {
                ScheduleNoiseFadeStep();
            }
            else
            {
                noiseFadeMode = NoiseFadeNone;
            }

            return;
        }

        if (noiseFadeMode == NoiseFadeOutMode)
        {
            channelNoiseSE.volume = Mathf.Lerp(1f, 0f, fadeProgress);

            if (noiseFadeStep < 10)
            {
                ScheduleNoiseFadeStep();
            }
            else
            {
                StopChannelNoise();
            }
        }
    }

    private void ScheduleNoiseFadeStep()
    {
        if (isNoiseFadeStepScheduled) return;
        isNoiseFadeStepScheduled = true;
        SendCustomEventDelayedSeconds(nameof(_NoiseFadeStep), 0.1f);
    }

    private void StopChannelNoise()
    {
        noiseFadeMode = NoiseFadeNone;
        noiseFadeStep = 0;
        if (channelNoiseSE != null) channelNoiseSE.Stop();
    }

    private void StartNoiseFadeOutDelay(float delaySeconds)
    {
        noiseFadeOutDelayRemaining = delaySeconds;
        isNoiseFadeOutDelayActive = true;
        ScheduleNoiseFadeOutDelayStep();
    }

    public void _NoiseFadeOutDelayStep()
    {
        isNoiseFadeOutDelayStepScheduled = false;

        if (!isNoiseFadeOutDelayActive) return;

        noiseFadeOutDelayRemaining -= 0.1f;
        if (noiseFadeOutDelayRemaining > 0f)
        {
            ScheduleNoiseFadeOutDelayStep();
            return;
        }

        isNoiseFadeOutDelayActive = false;
        NoiseFadeOut();
    }

    private void ScheduleNoiseFadeOutDelayStep()
    {
        if (isNoiseFadeOutDelayStepScheduled) return;
        isNoiseFadeOutDelayStepScheduled = true;
        SendCustomEventDelayedSeconds(nameof(_NoiseFadeOutDelayStep), 0.1f);
    }

    private void CancelPendingNoiseFadeOut()
    {
        isNoiseFadeOutDelayActive = false;
        noiseFadeOutDelayRemaining = 0f;
    }

    #endregion

    public override void OnVideoError(VideoError videoError)
    {
        if (!waitingPlay) return;

        Debug.LogWarning($"[HoboRadio] OnVideoError Received: {videoError}");
        HandleRetry();

    }
}
