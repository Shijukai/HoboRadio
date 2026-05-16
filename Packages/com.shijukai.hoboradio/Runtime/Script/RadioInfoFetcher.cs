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
    // TMPを配列に変更！ここにスクロール用の2つのTMPを入れる
    [SerializeField] private TextMeshProUGUI[] displayTmps;
    [SerializeField] private UdonBehaviour radioRoot;
    [SerializeField] private float refreshInterval = 60f;

    private float lastRequestTime = -10f;
    private bool isWaitingForRetry = false;

    void Start()
    {
        SendCustomEventDelayedSeconds(nameof(RequestUpdate), 2f);
    }

    // 複数のTMPを同時に書き換えるための便利関数
    private void UpdateAllDisplays(string newText)
    {
        if (displayTmps == null) return;
        for (int i = 0; i < displayTmps.Length; i++)
        {
            if (displayTmps[i] != null)
            {
                displayTmps[i].text = newText;
            }
        }
    }

    public void RequestUpdate()
    {
        if (radioRoot == null || displayTmps == null || infoUrls == null) return;

        if (Time.timeSinceLevelLoad - lastRequestTime < 5.5f)
        {
            if (!isWaitingForRetry)
            {
                isWaitingForRetry = true;
                UpdateAllDisplays("[ON AIR ] Loading... -Thank you for listening !-");
                SendCustomEventDelayedSeconds(nameof(RequestUpdate), 5.5f - (Time.timeSinceLevelLoad - lastRequestTime));
            }
            return;
        }

        isWaitingForRetry = false;
        lastRequestTime = Time.timeSinceLevelLoad;

        int currentIndex = (int)radioRoot.GetProgramVariable("currentChannelIndex");
        if (currentIndex >= 0 && currentIndex < infoUrls.Length)
        {
            UpdateAllDisplays("[ON AIR ] Loading... -Thank you for listening !-");
            VRCStringDownloader.LoadUrl(infoUrls[currentIndex], (IUdonEventReceiver)this);
        }

        SendCustomEventDelayedSeconds(nameof(AutoRefresh), refreshInterval);
    }

    public void AutoRefresh()
    {
        RequestUpdate();
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        UpdateAllDisplays($"[ON AIR ] {result.Result} -Thank you for listening !-");
    }

    // エラー時
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        UpdateAllDisplays("[ON AIR ] Loading Error... -Thank you for listening !-");
    }
}