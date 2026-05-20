using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RadioInfoFetcher : UdonSharpBehaviour
{
    [Header("Settings")]
    [SerializeField] private VRCUrl[] infoUrls;
    [SerializeField] private TextMeshProUGUI masterTmp;
    [SerializeField] private UdonBehaviour radioRoot;
    [SerializeField] private float refreshInterval = 60f;

    [Header("Display Control")]
    [SerializeField] private GameObject displayRoot;
    [SerializeField] private GameObject scrollingRoot;
    [SerializeField] private UdonBehaviour scrollUdon;

    [Header("Options")]
    [SerializeField] private bool showClockWhenOff = false;

    private float lastRequestTime = -10f;
    private bool isWaitingForRetry = false;
    private int lastClockSecond = -1;

    void Start()
    {
        bool isPowerOn = (bool)radioRoot.GetProgramVariable("radioPowerOn");
        if (isPowerOn) SendCustomEventDelayedSeconds(nameof(RequestUpdate), 2f);
        else ClearDisplay();
    }

    void Update()
    {
        if (radioRoot == null) return;

        bool isPowerOn = (bool)radioRoot.GetProgramVariable("radioPowerOn");
        if (!isPowerOn && showClockWhenOff && masterTmp != null)
        {
            DateTime jstTime = Networking.GetNetworkDateTime().AddHours(9);
            if (jstTime.Second != lastClockSecond)
            {
                lastClockSecond = jstTime.Second;
                masterTmp.text = $"{jstTime:HH:mm:ss}";
            }
        }
    }

    private void SetMasterAlpha(float alpha)
    {
        if (masterTmp != null)
        {
            Color c = masterTmp.color;
            c.a = alpha;
            masterTmp.color = c;
        }
    }

    public void ClearDisplay()
    {
        if (showClockWhenOff)
        {
            if (displayRoot != null) displayRoot.SetActive(true);
            if (scrollingRoot != null) scrollingRoot.SetActive(false);

            if (masterTmp != null) masterTmp.gameObject.SetActive(true);
            SetMasterAlpha(1.0f);
        }
        else
        {
            if (displayRoot != null) displayRoot.SetActive(false);
        }
    }

    public void RequestUpdate()
    {
        if (radioRoot == null || masterTmp == null) return;

        bool isPowerOn = (bool)radioRoot.GetProgramVariable("radioPowerOn");
        if (!isPowerOn)
        {
            ClearDisplay();
            return;
        }

        // 1. まずMasterを透明化（時計表示からの残像を防ぐ）
        SetMasterAlpha(0.0f);

        bool isWakingUp = (scrollingRoot != null && !scrollingRoot.activeSelf);

        // 2. 透明にした後に文字を更新
        masterTmp.text = " CONNECTING ... ";

        if (isWakingUp)
        {
            // 3. 表示ルートを有効化
            if (displayRoot != null) displayRoot.SetActive(true);
            if (scrollingRoot != null) scrollingRoot.SetActive(true);

            // 4. スクロール側にリセット命令（この中で A/B に文字がコピーされる）
            if (scrollUdon != null) scrollUdon.SendCustomEvent("ResetScroll");
        }

        if (infoUrls == null) return;

        if (Time.timeSinceLevelLoad - lastRequestTime < 5.5f)
        {
            if (!isWaitingForRetry)
            {
                isWaitingForRetry = true;
                SendCustomEventDelayedSeconds(nameof(RequestUpdate), 6f);
            }
            return;
        }

        isWaitingForRetry = false;
        lastRequestTime = Time.timeSinceLevelLoad;

        int currentIndex = (int)radioRoot.GetProgramVariable("currentChannelIndex");
        if (currentIndex >= 0 && currentIndex < infoUrls.Length)
        {
            VRCStringDownloader.LoadUrl(infoUrls[currentIndex], (IUdonEventReceiver)this);
        }

        SendCustomEventDelayedSeconds(nameof(AutoRefresh), refreshInterval);
    }

    public void AutoRefresh() => RequestUpdate();

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if ((bool)radioRoot.GetProgramVariable("radioPowerOn"))
        {
            if (masterTmp != null) masterTmp.text = $"[ON AIR ] {result.Result} - Thank you for listening ! -";
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if ((bool)radioRoot.GetProgramVariable("radioPowerOn"))
        {
            if (masterTmp != null) masterTmp.text = " LINK ERROR ... ";
        }
    }
}
