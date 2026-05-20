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
    [Header("--- Settings ---")]
    [SerializeField] bool isGlobal = true; // チェックを入れると全員同期、外すとローカル
    [SerializeField] public bool radioPowerOn = true; //電源は同期モードに関係なくローカル動作するようにしています

    [Header("--- Channels (URLs) ---")]
    public VRCUrl[] channels = new VRCUrl[3];
    [UdonSynced, SerializeField] public int currentChannelIndex = 0;
    private int loadedChannelIndex = -1;

    [Header("--- 3D Model Options (Optional) ---")]
    public Animator radioAnimator;
    [SerializeField] private float[] channelDialValues = new float[] { 0.416f, 0.43f, 0.45f };

    [Header("--- UI Options (Optional) ---")]
    public TextMeshProUGUI channelText;
    public TextMeshProUGUI statusText; // 旧TextもTMPに統一を推奨
    public GameObject debugCanvas;

    [Header("--- Audio / External ---")]
    public AudioSource powerSwitchSE;
    public AudioSource channelNoiseSE;
    public BaseVRCVideoPlayer videoPlayer;
    public UdonBehaviour infoFetcher;

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

    private void Start()
    {
        // 初期化
        if (radioPowerOn)
        {
            if (radioAnimator != null) radioAnimator.SetTrigger("PowerOn");
            UpdateVisuals();

            // Global設定かつオーナーなら初期ロード実行
            if (!isGlobal || Networking.IsOwner(gameObject))
            {
                hasSyncedInitial = true;
                SendCustomEventDelayedSeconds(nameof(ApplyChannel), 2f);
            }
        }
    }

    private void Update()
    {
        DateTime serverTime = Networking.GetNetworkDateTime();
        int currentHr = serverTime.Hour;
        float currentSec = serverTime.Minute * 60f + serverTime.Second;

        // 1時間ごとの自動更新（電源ON時のみ）
        if (radioPowerOn && lastServerHour != currentHr && (!isGlobal || hasSyncedInitial))
        {
            lastServerHour = currentHr;
            ApplyChannel();
        }

        // 再生時間の表示更新
        if (videoPlayer.IsPlaying && statusText != null)
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
            if (radioAnimator != null) radioAnimator.SetTrigger("PowerOn");
            lastDisplayedSecond = -1;
            ApplyChannel(); // ApplyChannel内でRequestUpdateが呼ばれ画面が点灯
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
            ApplyChannel(); // オーナー自身も即時適用
        }
        else
        {
            currentChannelIndex = (currentChannelIndex + 1) % channels.Length;
            ApplyChannel();
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
        SendCustomEventDelayedSeconds(nameof(UnlockInteraction), 3f);
    }

    public void UnlockInteraction() => isInteractedLocked = false;

    #endregion

    #region --- Logic & Sync ---

    public override void OnDeserialization()
    {
        if (!isGlobal) return;
        hasSyncedInitial = true;
        if (loadedChannelIndex != currentChannelIndex) ApplyChannel();
    }

    private void ApplyChannel()
    {
        loadedChannelIndex = currentChannelIndex;
        UpdateVisuals();

        if (!radioPowerOn) return;

        CancelPendingNoiseFadeOut();

        // Fetcherへの通知
        if (infoFetcher != null) infoFetcher.SendCustomEvent("RequestUpdate");

        // ビデオロード
        if (!waitingPlay)
        {
            videoPlayer.LoadURL(channels[currentChannelIndex]);
            waitingPlay = true;
            videoLoadStartTime = Time.timeSinceLevelLoad;
            SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 2f);
        }

        NoiseFadeIn();
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

    public void WaitUntilReady()
    {
        if (!videoPlayer.IsReady || videoPlayer.IsPlaying)
        {
            if (waitingPlay)
            {
                // OnVideoError不発時のための措置
                // 20秒経過してもReadyにならなければ強制タイムアウト処理
                if (Time.timeSinceLevelLoad - videoLoadStartTime > 20f)
                {
                    waitingPlay = false;
                    videoPlayer.Stop();
                    CancelPendingNoiseFadeOut();
                    NoiseFadeOut();
                    if (statusText != null) statusText.text = "LOADING TIMEOUT";
                    return; // ループを終了
                }
                SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 0.2f);
            }
            return;
        }

        waitingPlay = false;

        // 再生開始
        float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
        videoPlayer.SetTime(syncTime);
        videoPlayer.Play();

        if (statusText != null) statusText.text = "";

        StartNoiseFadeOutDelay(3f);
        SendCustomEventDelayedSeconds(nameof(ReSyncSeek), 30f); // 30秒後に微調整
    }

    public void ReSyncSeek()
    {
        if (videoPlayer.IsPlaying)
        {
            float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
            videoPlayer.SetTime(syncTime);
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

    public void NoiseFadeStep()
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
        SendCustomEventDelayedSeconds(nameof(NoiseFadeStep), 0.1f);
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

    public void NoiseFadeOutDelayStep()
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
        SendCustomEventDelayedSeconds(nameof(NoiseFadeOutDelayStep), 0.1f);
    }

    private void CancelPendingNoiseFadeOut()
    {
        isNoiseFadeOutDelayActive = false;
        noiseFadeOutDelayRemaining = 0f;
    }

    #endregion

    public override void OnVideoError(VideoError videoError)
    {
        // エラー発生時にロックを解除し、次回の操作を許可する
        waitingPlay = false;
        CancelPendingNoiseFadeOut();
        NoiseFadeOut();

        // ステータス表示にエラーを反映
        if (statusText != null)
        {
            statusText.text = "VIDEO PLAYER ERROR";
        }

    }
}
