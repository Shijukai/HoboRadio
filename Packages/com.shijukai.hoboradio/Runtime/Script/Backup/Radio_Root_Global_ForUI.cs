
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDKBase;
using TMPro;

public class Radio_Root_Global_ForUI : UdonSharpBehaviour
{
    [SerializeField] public VRCUrl Ch01 = new VRCUrl("https://media.shijukairadio.com/stream/ch1");

    [SerializeField] public VRCUrl Ch02 = new VRCUrl("https://media.shijukairadio.com/stream/ch2");

    [SerializeField] public VRCUrl Ch03 = new VRCUrl("https://media.shijukairadio.com/stream/ch3");

    [SerializeField] private TextMeshProUGUI statusText;

    [SerializeField] private TextMeshProUGUI channelText;

    [SerializeField] bool RadioPowerOn = true;

    [SerializeField] private GameObject DebugCanvas;

    [SerializeField] private AudioSource PowerSwitchSE;

    [SerializeField] private AudioSource ChannelNoiseSE;

    [UdonSynced] [SerializeField] private int currentChannelIndex = 0;

    [Header("External Modules")]
    [SerializeField] private VRC.Udon.UdonBehaviour infoFetcher; // 外部のFetcherを指定

    public VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer videoPlayer;

    public int noiseStep;

    private DateTime ServerTime;

    int iServarHr = 0;

    int lastServerHour = -1; // 起動直後に必ず初回同期させるためのダミー値

    float iServarSec = 0f;

    bool WaitingPlay = false;

    private VRCUrl[] channels;

    private int lastDisplayedSecond = -1;

    private int loadedChannelIndex = -1;

    private bool hasSynced = false;

    private bool InteracLock = false;

    private void Start()
    {
        channels = new VRCUrl[] { Ch01, Ch02, Ch03 };

        if (RadioPowerOn == true)
        {
            UpdateChannelText();

            if (Networking.IsOwner(gameObject))
            {
                hasSynced = true;
                SendCustomEventDelayedSeconds(nameof(ApplyChannel), 2f);
            }
        }
    }

    //常に動き続ける処理
    private void Update()
    {
        //サーバー時間を取得
        ServerTime = Networking.GetNetworkDateTime();

        //サーバー時間HHをint型に変換
        iServarHr = ServerTime.Hour;

        // サーバー時間mmssをint型の秒数に変換
        iServarSec = ServerTime.Minute * 60f + ServerTime.Second;

        //時間の値の変化の監視
        if (RadioPowerOn && lastServerHour != iServarHr && hasSynced)
        {
            lastServerHour = iServarHr; //変化後の値の記録
            ApplyChannel(); //値が変化した際の処理の呼び出し
        }

        //Testcord
        if (!videoPlayer.IsPlaying) return;

        double time = videoPlayer.GetTime();
        int totalSec = (int)time;

        if (totalSec == lastDisplayedSecond) return; // 1秒未満の変化は無視

        lastDisplayedSecond = totalSec;

        int min = totalSec / 60;
        int sec = totalSec % 60;

        statusText.text = min.ToString("00") + ":" + sec.ToString("00");


    }
    //電源ボタンに触れた時の動作
    public void InteractButtonPower()
    {
        if (InteracLock) return;

        InteracLock = true;

        SendCustomEventDelayedSeconds(nameof(InteracUnlock), 5f);

        PowerSwitchSE.Play();
        //再生状態の検出
        if (RadioPowerOn == true)
        {
            videoPlayer.Stop();

            if (channelText != null)
            {
                channelText.text = "";
            }

            WaitingPlay = false;

            ChannelNoiseSE.Stop();
            //再生中の場合の処理
            RadioPowerOn = false;

            InteracUnlock();

            return;
        }
        RadioPowerOn = true;

        UpdateChannelText();

        //停止中の処理の呼び出し
        lastDisplayedSecond = -1;

        ApplyChannel();

    }
    public void InteractButtonStop()
    { }
    public void InteractButtonPause()
    { }
    public void InteractButtonForward()
    { }
    public void InteractButtonRewind()
    { }
    public void InteractButtonPlay()
    { }
    public void InteractButtonDebug()
    {
        if (DebugCanvas != null)
            DebugCanvas.SetActive(!DebugCanvas.activeSelf);
    }
    public void InteractSwitchChannel()
    {
        if (!RadioPowerOn) return; // 電源OFFなら無効

        if (InteracLock) return;

        InteracLock = true;

        SendCustomEventDelayedSeconds(nameof(InteracUnlock), 5f);

        if (WaitingPlay)
            return;

        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        PowerSwitchSE.Play();

        ChangeChannel();

    }

    public void InteracUnlock()
    {
        InteracLock = false;
    }

    private void ChangeChannel()
    {
        if (!Networking.IsOwner(gameObject)) return;


        WaitingPlay = false;

        currentChannelIndex = (currentChannelIndex + 1) % channels.Length;

        RequestSerialization(); //同期送信

        ReceiveSync(); //自分にも適用

    }

    public override void OnDeserialization()
    {
        hasSynced = true;
        ReceiveSync();
    }

    private void ReceiveSync()
    {

        if (loadedChannelIndex == currentChannelIndex)
            return;

        ApplyChannel();
    }

    private void ApplyChannel()
    {

        loadedChannelIndex = currentChannelIndex;

        if (!RadioPowerOn) return;

        // URLロード
        LoadAndSync();

        // チャンネルを表示
        UpdateChannelText();

        if (infoFetcher != null)
        {
            infoFetcher.SendCustomEvent("RequestUpdate");
        }

        NoiseFadeIn();
    }

    //読み込み処理
    private void LoadAndSync()
    {
        if (WaitingPlay) return;

        videoPlayer.LoadURL(channels[currentChannelIndex]);
        WaitingPlay = true;

        SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 2f);
    }
    //再生準備完了時にシーク・再生を行う処理
    public void WaitUntilReady()
    {
        //読み込み中だった場合に待機させる
        if (!videoPlayer.IsReady)
        {
            SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 0.2f);
            return;
        }
        if (videoPlayer.IsPlaying)
        {
            SendCustomEventDelayedSeconds(nameof(WaitUntilReady), 0.2f);
            return;
        }
        if (!WaitingPlay) return;

        WaitingPlay = false;

        SendCustomEventDelayedSeconds(nameof(PlayOnly), 0.5f);

        SendCustomEventDelayedSeconds(nameof(NoiseFadeOut), 4f);

        //エラー解消時のテキスト消去
        if (statusText != null)
        {
            statusText.text = "";
        }
    }
    public void PlayOnly()
    {
        if (!videoPlayer.IsPlaying)
        {
            videoPlayer.SetTime(iServarSec);

            videoPlayer.Play();

            SendCustomEventDelayedSeconds(nameof(ReSyncSeek), 30f);
        }
    }

    public void ReSyncSeek()
    {
        if (videoPlayer.IsPlaying)
        {
            videoPlayer.SetTime(iServarSec);
        }
    }

    public void NoiseFadeIn()
    {
        noiseStep = 0;
        ChannelNoiseSE.volume = 0f;
        if (!ChannelNoiseSE.isPlaying)
            ChannelNoiseSE.Play();
        NoiseFadeInStep();
    }

    public void NoiseFadeInStep()
    {
        ChannelNoiseSE.volume = Mathf.Lerp(0f, 1f, ++noiseStep / 10f);
        if (noiseStep < 10)
            SendCustomEventDelayedSeconds(nameof(NoiseFadeInStep), 0.1f);
    }

    public void NoiseFadeOut()
    {
        noiseStep = 0;
        NoiseFadeOutStep();
    }

    public void NoiseFadeOutStep()
    {
        ChannelNoiseSE.volume = Mathf.Lerp(1f, 0f, ++noiseStep / 10f);
        if (noiseStep < 10)
            SendCustomEventDelayedSeconds(nameof(NoiseFadeOutStep), 0.1f);
        else
            ChannelNoiseSE.Stop();
    }

    private void UpdateChannelText()
    {
        if (channelText != null)
        {
            channelText.text = "CH" + (currentChannelIndex + 1).ToString("00");
        }
    }
}