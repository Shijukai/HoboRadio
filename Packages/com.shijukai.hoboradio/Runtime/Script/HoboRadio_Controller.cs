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
    [SerializeField] bool radioPowerOn = true;

    [Header("--- Channels (URLs) ---")]
    public VRCUrl[] channels = new VRCUrl[3];
    [UdonSynced, SerializeField] private int currentChannelIndex = 0;
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
    private int noiseFadeInStep;
    private int noiseFadeOutStep;
    private int lastServerHour = -1;
    private int lastDisplayedSecond = -1;
    private bool waitingPlay = false;
    private bool isInteractedLocked = false;
    private bool hasSyncedInitial = false;

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
            if (channelNoiseSE != null) channelNoiseSE.Stop();
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

        // Fetcherへの通知
        if (infoFetcher != null) infoFetcher.SendCustomEvent("RequestUpdate");

        // ビデオロード
        if (!waitingPlay)
        {
            videoPlayer.LoadURL(channels[currentChannelIndex]);
            waitingPlay = true;
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
            if (waitingPlay) SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 0.2f);
            return;
        }

        waitingPlay = false;

        // 再生開始
        float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
        videoPlayer.SetTime(syncTime);
        videoPlayer.Play();

        if (statusText != null) statusText.text = "";

        SendCustomEventDelayedSeconds(nameof(NoiseFadeOut), 3f);
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
        noiseFadeInStep = 0;
        channelNoiseSE.volume = 0f;
        if (!channelNoiseSE.isPlaying) channelNoiseSE.Play();
        NoiseFadeInStep();
    }

    public void NoiseFadeInStep()
    {
        if (channelNoiseSE == null) return;
        channelNoiseSE.volume = Mathf.Lerp(0f, 1f, ++noiseFadeInStep / 10f);
        if (noiseFadeInStep < 10) SendCustomEventDelayedSeconds(nameof(NoiseFadeInStep), 0.1f);
    }

    public void NoiseFadeOut()
    {
        if (channelNoiseSE == null) return;
        noiseFadeOutStep = 0;
        NoiseFadeOutStep();
    }

    public void NoiseFadeOutStep()
    {
        if (channelNoiseSE == null) return;
        channelNoiseSE.volume = Mathf.Lerp(1f, 0f, ++noiseFadeOutStep / 10f);
        if (noiseFadeOutStep < 10) SendCustomEventDelayedSeconds(nameof(NoiseFadeOutStep), 0.1f);
        else channelNoiseSE.Stop();
    }

    #endregion

    public override void OnVideoError(VideoError videoError)
    {
        // エラー発生時にロックを解除し、次回の操作を許可する
        waitingPlay = false;

        // ステータス表示にエラーを反映
        if (statusText != null)
        {
            statusText.text = "VIDEO PLAYER ERROR";
        }

    }
}
