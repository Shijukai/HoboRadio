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
    [HideInInspector] public AudioSource channelNoiseSE;
    [HideInInspector] public BaseVRCVideoPlayer videoPlayer;
    [Tooltip("動画の音声を出力するAudioSource（初期化ノイズ防止用）")]
    public AudioSource videoAudioSource;
    [Range(0f, 1f)]
    public float masterVolume = 0.5f;
    [HideInInspector] public UdonBehaviour infoFetcher;

    [Header("--- テープ再生設定 ---")]
    [UdonSynced] public int currentMode = 0; // 0: Radio, 1: Tape
    [UdonSynced] public bool isTapeInserted = false;
    [UdonSynced] public bool isTapePlaying = false;
    [UdonSynced] public VRCUrl currentTapeUrl;
    [UdonSynced] public double tapeStartTime = 0;

    [Header("--- テープ機構設定 ---")]
    public Transform tapeSlot;
    [Tooltip("スロットが開いてからスナップされるまでの待機時間（秒）")]
    public float slotOpenDelay = 0.5f;
    public AudioSource tapeMechanicsAudioSource;
    public AudioClip powerSwitchOnSE;
    public AudioClip powerSwitchOffSE;
    public AudioClip tapeInsertSE;
    public AudioClip tapeEjectSE;
    [Tooltip("テープ読み込み中の駆動音（ノイズ用AudioSourceで再生）")]
    public AudioClip tapeLoadingSE;

    [HideInInspector] public HoboTape insertedTape;
    private HoboTape pendingInsertTape;

    [UdonSynced] public bool isEjecting = false;
    [UdonSynced] public bool isTapeStopped = false;
    [UdonSynced] public bool isSlotOpen = false;

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

    private bool isEjectAnimating = false;
    private float ejectAnimTime = 0f;
    private Vector3 ejectStartPos;
    private Vector3 ejectEndPos;

    [Header("--- アニメーション設定（リール） ---")]
    public Transform radioReelLeft;
    public Transform radioReelRight;
    [Tooltip("リールの回転速度と軸（ローカル空間）")]
    public Vector3 reelRotationSpeed = new Vector3(-180f, 0f, 0f);

    // Animation Trackers
    private bool _animPlayDown = false;
    private bool _animPauseDown = false;
    private bool _animSlotOpen = false;

    private AudioClip defaultRadioNoiseSE;

    private void Start()
    {
        Debug.Log("[HoboRadio] Controller Started");

        if (channelNoiseSE != null)
        {
            defaultRadioNoiseSE = channelNoiseSE.clip;
        }

        if (!isGlobal || Networking.IsOwner(gameObject))
        {
            hasSyncedInitial = true;
        }

        // 初期化
        if (radioPowerOn)
        {
            if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_PowerOn");
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
        int currentMin = serverTime.Minute;

        if (lastServerHour == -1)
        {
            lastServerHour = currentHr;
        }

        // 1時間ごとの自動更新（電源ON時かつラジオモード時のみ）
        if (radioPowerOn && currentMode == 0 && currentMin == 0 && lastServerHour != currentHr && (!isGlobal || hasSyncedInitial))
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

        // テープせり出しアニメーション
        if (isEjectAnimating && insertedTape != null && tapeSlot != null)
        {
            ejectAnimTime += Time.deltaTime;
            float t = Mathf.Clamp01(ejectAnimTime / 0.5f); // 0.5秒かけて移動
            Transform target = insertedTape.targetTransform != null ? insertedTape.targetTransform : insertedTape.transform;
            target.localPosition = Vector3.Lerp(ejectStartPos, ejectEndPos, t);

            if (t >= 1.0f)
            {
                isEjectAnimating = false;
                if (insertedTape.pickup != null)
                {
                    insertedTape.pickup.pickupable = true;
                }
            }
        }

        // テープとリール・ハブの再生アニメーション
        if (currentMode == 1 && isTapeInserted && insertedTape != null && (isTapePlaying || waitingPlay))
        {
            Quaternion rotDelta = Quaternion.Euler(reelRotationSpeed * Time.deltaTime);

            if (radioReelLeft != null) radioReelLeft.localRotation = rotDelta * radioReelLeft.localRotation;
            if (radioReelRight != null) radioReelRight.localRotation = rotDelta * radioReelRight.localRotation;

            if (insertedTape.hubLeft != null) insertedTape.hubLeft.localRotation = rotDelta * insertedTape.hubLeft.localRotation;
            if (insertedTape.hubRight != null) insertedTape.hubRight.localRotation = rotDelta * insertedTape.hubRight.localRotation;

            if (isTapePlaying && videoPlayer != null && videoPlayer.IsPlaying)
            {
                float duration = videoPlayer.GetDuration();
                if (duration > 0f && !float.IsInfinity(duration))
                {
                    float progress = Mathf.Clamp01(videoPlayer.GetTime() / duration);
                    insertedTape.UpdateTapeProgress(progress);
                }
            }
        }
    }

    #region --- Interaction ---

    public void InteractButtonPower()
    {
        if (isInteractedLocked) return;
        LockInteraction();

        if (radioPowerOn) // OFFにする処理
        {
            if (tapeMechanicsAudioSource != null && powerSwitchOffSE != null) tapeMechanicsAudioSource.PlayOneShot(powerSwitchOffSE);
            if (videoPlayer != null) videoPlayer.Stop();
            CancelPendingNoiseFadeOut();
            StopChannelNoise();
            if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_PowerOff");
            if (channelText != null) channelText.text = "";
            radioPowerOn = false;
            waitingPlay = false;

            if (isTapeInserted)
            {
                isTapePlaying = false;
                isTapeStopped = true;
            }

            // Fetcherに表示クリアを通知
            if (infoFetcher != null) infoFetcher.SendCustomEvent("ClearDisplay");
        }
        else // ONにする処理
        {
            if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null) tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
            radioPowerOn = true;
            hasSyncedInitial = true;
            isRetryScheduled = false;
            if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_PowerOn");
            lastDisplayedSecond = -1;
            _ApplyChannel(); // ApplyChannel内でRequestUpdateが呼ばれ画面が点灯
        }
    }

    public void InteractSwitchChannel()
    {
        if (!radioPowerOn || isInteractedLocked || waitingPlay || isTapeInserted) return;
        LockInteraction();

        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null) tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);

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

    public void InteractButtonStop()
    {
        if (isInteractedLocked || !isTapeInserted || !radioPowerOn) return;

        if (videoAudioSource != null) videoAudioSource.mute = false;

        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
        }

        if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_Stop");

        TakeOwnership();
        if (!isTapeStopped)
        {
            if (videoPlayer != null) videoPlayer.Stop();
            isTapePlaying = false;
            isTapeStopped = true;
            waitingPlay = false;

            CancelPendingNoiseFadeOut();
            StopChannelNoise();

            RequestSerialization();
            UpdateVisuals();
        }
        else
        {
            EjectTape(insertedTape);
        }
    }

    public void InteractButtonPlay()
    {
        if (isInteractedLocked || !isTapeInserted || !radioPowerOn) return;

        TakeOwnership();
        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
        }

        if (isTapeStopped)
        {
            isTapeStopped = false;
            RequestSerialization();
            _PlayTape();
            UpdateVisuals();
        }
        else if (!isTapePlaying)
        {
            if (videoPlayer != null)
            {
                videoPlayer.Play();
                isTapePlaying = true;
                RequestSerialization();
                UpdateVisuals();
            }
        }
    }

    public void InteractButtonPause()
    {
        if (isInteractedLocked || !isTapeInserted || !radioPowerOn) return;

        TakeOwnership();
        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
        }

        if (isTapeStopped) return;

        if (videoPlayer != null)
        {
            if (isTapePlaying)
            {
                videoPlayer.Pause();
                isTapePlaying = false;
                tapeStartTime = -videoPlayer.GetTime();
            }
            else
            {
                videoPlayer.Play();
                isTapePlaying = true;
                tapeStartTime = Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds - videoPlayer.GetTime();
            }
            RequestSerialization();
            UpdateVisuals();
        }
    }

    public void InteractButtonFastForward()
    {
        if (isInteractedLocked || !isTapeInserted || !radioPowerOn) return;

        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
        }

        if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_FF");

        if (isTapeStopped || videoPlayer == null) return;

        TakeOwnership();
        float targetTime = Mathf.Min((float)videoPlayer.GetDuration(), videoPlayer.GetTime() + 10f);
        videoPlayer.SetTime(targetTime);

        tapeStartTime = isTapePlaying ? (Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds - targetTime) : -targetTime;
        RequestSerialization();
    }

    public void InteractButtonRewind()
    {
        if (isInteractedLocked || !isTapeInserted || !radioPowerOn) return;

        if (tapeMechanicsAudioSource != null && powerSwitchOnSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(powerSwitchOnSE);
        }

        if (radioAnimator != null) radioAnimator.SetTrigger("HoboRadio_REW");

        if (isTapeStopped || videoPlayer == null) return;

        TakeOwnership();
        float targetTime = Mathf.Max(0f, videoPlayer.GetTime() - 10f);
        videoPlayer.SetTime(targetTime);

        tapeStartTime = isTapePlaying ? (Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds - targetTime) : -targetTime;
        RequestSerialization();
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

        if (isTapeInserted && insertedTape == null)
        {
            _RestoreLateJoinerTape();
        }

        if (isFirstSync || loadedChannelIndex != currentChannelIndex)
        {
            _ApplyChannel();
        }
        else
        {
            UpdateVisuals();
        }
    }

    public void _RestoreLateJoinerTape()
    {
        if (!isTapeInserted || insertedTape != null) return;

        _FindTapeInSlot();

        if (insertedTape != null)
        {
            if (insertedTape.tapeRigidbody != null) insertedTape.tapeRigidbody.isKinematic = true;
            if (insertedTape.pickup != null)
            {
                insertedTape.pickup.Drop();
                insertedTape.pickup.pickupable = false;
            }

            if (tapeSlot != null)
            {
                Transform target = insertedTape.targetTransform != null ? insertedTape.targetTransform : insertedTape.transform;
                target.SetParent(tapeSlot, true);
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
            }
            UpdateVisuals();
        }
        else
        {
            SendCustomEventDelayedSeconds(nameof(_RestoreLateJoinerTape), 2f);
        }
    }

    public void _FindTapeInSlot()
    {
        if (tapeSlot == null) return;
        Collider[] colliders = Physics.OverlapSphere(tapeSlot.position, 0.2f);
        foreach (Collider col in colliders)
        {
            if (col == null) continue;
            HoboTape tape = col.GetComponent<HoboTape>();
            if (tape == null && col.transform.root != null)
            {
                tape = col.transform.root.GetComponentInChildren<HoboTape>();
            }

            if (tape != null)
            {
                insertedTape = tape;
                break;
            }
        }
    }

    public void _ApplyChannel()
    {
        loadedChannelIndex = currentChannelIndex;
        UpdateVisuals();

        Debug.Log($"[HoboRadio] ApplyChannel: powerOn={radioPowerOn}, waitingPlay={waitingPlay}, currentCh={currentChannelIndex}, isOwner={Networking.IsOwner(gameObject)}");

        if (!radioPowerOn) return;

        // Fetcherへの通知（電源ON時は画面を点灯させる）
        if (infoFetcher != null) infoFetcher.SendCustomEvent("RequestUpdate");

        if (currentMode == 1) return; // テープモード時はラジオ側の動画ロードとノイズ再生をスキップ

        if (videoAudioSource != null) videoAudioSource.mute = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        waitingPlay = true;
        retryCount = 0;
        isRetryScheduled = false;

        CancelPendingNoiseFadeOut();

        SendCustomEventDelayedFrames(nameof(_ExecuteLoad), 2);

        NoiseFadeIn();
    }

    public void _ExecuteLoad()
    {
        if (!radioPowerOn) return;

        if (videoPlayer == null || channels == null || currentChannelIndex >= channels.Length || channels[currentChannelIndex] == null) return;

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
            radioAnimator.SetFloat("HoboRadio_NeedlePosition", channelDialValues[currentChannelIndex]);
        }

        // UI：チャンネル番号表示
        if (channelText != null)
        {
            channelText.text = $"CH{(currentChannelIndex + 1):00}";
        }

        if (radioAnimator == null) return;

        // スロット状態の同期
        if (isSlotOpen && !_animSlotOpen) { radioAnimator.SetTrigger("HoboRadio_SlotOpen"); _animSlotOpen = true; }
        else if (!isSlotOpen && _animSlotOpen) { radioAnimator.SetTrigger("HoboRadio_SlotClose"); _animSlotOpen = false; }

        // ボタン沈み込み状態の同期
        bool shouldPlayDown = isTapeInserted && !isTapeStopped && !isEjecting;
        bool shouldPauseDown = isTapeInserted && !isTapePlaying && !isTapeStopped && !waitingPlay && !isEjecting;

        if (shouldPlayDown && !_animPlayDown) { radioAnimator.SetTrigger("HoboRadio_PlayOn"); _animPlayDown = true; }
        else if (!shouldPlayDown && _animPlayDown) { radioAnimator.SetTrigger("HoboRadio_PlayOff"); _animPlayDown = false; }

        if (shouldPauseDown && !_animPauseDown) { radioAnimator.SetTrigger("HoboRadio_PauseOn"); _animPauseDown = true; }
        else if (!shouldPauseDown && _animPauseDown) { radioAnimator.SetTrigger("HoboRadio_PauseOff"); _animPauseDown = false; }
    }

    private void TakeOwnership()
    {
        if (isGlobal && !Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
    }

    public override void OnVideoReady()
    {
        if (!waitingPlay) return;
        waitingPlay = false;
        isRetryScheduled = false;

        if (videoPlayer == null) return;

        Debug.Log($"[HoboRadio] OnVideoReady: ready={videoPlayer.IsReady} dur={videoPlayer.GetDuration()}");

        if (currentMode == 1) // Tape Mode
        {
            if (videoAudioSource != null) videoAudioSource.mute = true;

            float targetTime = tapeStartTime < 0 ? (float)(-tapeStartTime) : (float)(Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds - tapeStartTime);
            if (targetTime < 0 && tapeStartTime >= 0) targetTime += 86400f;
            videoPlayer.SetTime(targetTime);

            if (isTapePlaying)
            {
                videoPlayer.Play();
            }
            else
            {
                videoPlayer.Pause();
            }

            if (statusText != null) statusText.text = "";

            UpdateVisuals();
            SendCustomEventDelayedSeconds(nameof(_RestoreTapeAudio), 1.0f);
            StartNoiseFadeOutDelay(0.5f);
        }
        else // Radio Mode
        {
            float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
            videoPlayer.SetTime(syncTime);
            videoPlayer.Play();

            if (statusText != null) statusText.text = "";

            StartNoiseFadeOutDelay(3f);
            SendCustomEventDelayedSeconds(nameof(_ReSyncSeek), 30f); // 30秒後に微調整
        }
    }

    public void _ReSyncSeek()
    {
        if (currentMode != 0) return;

        if (videoPlayer != null && videoPlayer.IsPlaying)
        {
            float syncTime = Networking.GetNetworkDateTime().Minute * 60f + Networking.GetNetworkDateTime().Second;
            videoPlayer.SetTime(syncTime);
        }
    }

    public void _RestoreTapeAudio()
    {
        if (currentMode == 1 && isTapePlaying && videoAudioSource != null)
        {
            videoAudioSource.mute = false;
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

            if (videoPlayer != null) videoPlayer.Stop();

            if (currentMode == 1)
            {
                SendCustomEventDelayedSeconds(nameof(_ExecuteTapeLoad), RetryDelay);
            }
            else
            {
                SendCustomEventDelayedSeconds(nameof(_ExecuteLoad), RetryDelay);
            }
        }
        else
        {
            Debug.LogError("[HoboRadio] Load Failed: Max retry limit reached.");
            waitingPlay = false;
            isRetryScheduled = false;
            if (videoPlayer != null) videoPlayer.Stop();
            CancelPendingNoiseFadeOut();
            NoiseFadeOut();
            if (statusText != null) statusText.text = "LOAD ERROR";
        }
    }

    #endregion

    #region --- Audio Effects ---

    public void UpdateMasterVolume(float newVolume)
    {
        masterVolume = Mathf.Clamp01(newVolume);

        if (videoAudioSource != null)
        {
            videoAudioSource.volume = masterVolume;
        }

        if (channelNoiseSE != null && channelNoiseSE.isPlaying && noiseFadeMode == NoiseFadeNone)
        {
            channelNoiseSE.volume = masterVolume;
        }
    }

    public void NoiseFadeIn()
    {
        if (channelNoiseSE == null) return;
        CancelPendingNoiseFadeOut();
        if (defaultRadioNoiseSE != null && channelNoiseSE.clip != defaultRadioNoiseSE)
        {
            channelNoiseSE.clip = defaultRadioNoiseSE;
        }
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
            channelNoiseSE.volume = Mathf.Lerp(0f, 1f, fadeProgress) * masterVolume;

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
            channelNoiseSE.volume = Mathf.Lerp(1f, 0f, fadeProgress) * masterVolume;

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

    #region --- Tape Playback Control ---

    public void InsertTape(HoboTape tape)
    {
        if (tape == null || isTapeInserted || pendingInsertTape != null) return;

        TakeOwnership();

        pendingInsertTape = tape;
        isTapeInserted = true;
        isEjecting = false;
        isSlotOpen = true;
        isTapeStopped = true;

        if (tape.pickup != null)
        {
            tape.pickup.Drop();
            tape.pickup.pickupable = false;
        }

        if (tape.tapeRigidbody != null)
        {
            tape.tapeRigidbody.isKinematic = true;
        }

        Transform target = tape.targetTransform != null ? tape.targetTransform : tape.transform;
        if (tapeSlot != null)
        {
            target.SetParent(tapeSlot, true);
        }

        RequestSerialization();
        UpdateVisuals();

        if (tapeMechanicsAudioSource != null && tapeInsertSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(tapeInsertSE);
        }

        SendCustomEventDelayedSeconds(nameof(_CompleteInsertSnap), slotOpenDelay);
    }

    public void _CompleteInsertSnap()
    {
        if (pendingInsertTape == null) return;

        insertedTape = pendingInsertTape;
        pendingInsertTape = null;

        currentMode = 1;
        currentTapeUrl = insertedTape.tapeUrl;
        tapeStartTime = Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds;
        isSlotOpen = false;

        if (insertedTape.tapeRigidbody != null)
        {
            insertedTape.tapeRigidbody.isKinematic = true;
        }

        if (tapeSlot != null)
        {
            Transform target = insertedTape.targetTransform != null ? insertedTape.targetTransform : insertedTape.transform;
            target.SetParent(tapeSlot, true);
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
        }

        insertedTape.UpdateTapeProgress(0f);

        RequestSerialization();
        UpdateVisuals();

        if (videoPlayer != null) videoPlayer.Stop();
        CancelPendingNoiseFadeOut();
        StopChannelNoise();

        if (infoFetcher != null) infoFetcher.SendCustomEvent("RequestUpdate");

        InteractButtonPlay();
    }

    public void EjectTape(HoboTape tape)
    {
        if (!isTapeInserted || isEjecting) return;

        TakeOwnership();

        isEjecting = true;
        isSlotOpen = true;

        if (videoPlayer != null) videoPlayer.Stop();
        isTapePlaying = false;

        RequestSerialization();
        UpdateVisuals();

        if (tapeMechanicsAudioSource != null && tapeEjectSE != null)
        {
            tapeMechanicsAudioSource.PlayOneShot(tapeEjectSE);
        }

        SendCustomEventDelayedSeconds(nameof(_StartEjectAnimation), slotOpenDelay);
    }

    public void _StartEjectAnimation()
    {
        if (insertedTape != null && tapeSlot != null)
        {
            Transform target = insertedTape.targetTransform != null ? insertedTape.targetTransform : insertedTape.transform;
            target.SetParent(tapeSlot, true);

            if (insertedTape.tapeRigidbody != null)
            {
                insertedTape.tapeRigidbody.isKinematic = true;
            }

            ejectStartPos = target.localPosition;
            ejectEndPos = Vector3.up * 0.05f;
            ejectAnimTime = 0f;
            isEjectAnimating = true;
        }
    }

    public void InteractButtonEject()
    {
        if (!isTapeInserted) return;

        if (insertedTape == null)
        {
            _FindTapeInSlot();
        }

        EjectTape(insertedTape);
    }

    public void _PlayTape()
    {
        if (!radioPowerOn || currentTapeUrl == null) return;

        retryCount = 0;
        isRetryScheduled = false;

        if (insertedTape != null)
        {
            insertedTape.UpdateTapeProgress(0f);
        }

        tapeStartTime = Networking.GetNetworkDateTime().TimeOfDay.TotalSeconds;

        if (videoPlayer != null) videoPlayer.Stop();
        waitingPlay = true;

        SendCustomEventDelayedFrames(nameof(_ExecuteTapeLoad), 2);
    }

    public void _ExecuteTapeLoad()
    {
        if (!radioPowerOn || currentTapeUrl == null) return;

        if (videoPlayer != null)
        {
            Debug.Log($"[HoboRadio] LoadURL Executed (Tape Attempt {retryCount + 1}): {currentTapeUrl}");
            videoPlayer.LoadURL(currentTapeUrl);
            waitingPlay = true;
            isRetryScheduled = false;
            videoLoadStartTime = Time.timeSinceLevelLoad;
            SendCustomEventDelayedSeconds(nameof(_CheckLoadingTimeout), LoadingTimeout);

            if (channelNoiseSE != null && tapeLoadingSE != null)
            {
                CancelPendingNoiseFadeOut();
                channelNoiseSE.clip = tapeLoadingSE;
                channelNoiseSE.volume = masterVolume;
                if (!channelNoiseSE.isPlaying) channelNoiseSE.Play();
                noiseFadeMode = NoiseFadeNone;
            }
        }
    }

    #endregion

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || isTapeInserted || pendingInsertTape != null) return;

        HoboTape tape = other.GetComponent<HoboTape>();
        if (tape == null && other.transform.root != null)
        {
            tape = other.transform.root.GetComponentInChildren<HoboTape>();
        }

        if (tape != null)
        {
            InsertTape(tape);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || !isTapeInserted || !isEjecting || insertedTape == null) return;

        Transform tapeRoot = insertedTape.targetTransform != null ? insertedTape.targetTransform : insertedTape.transform;

        bool isTargetCollider = false;
        if (other.transform == tapeRoot || other.transform.IsChildOf(tapeRoot))
        {
            isTargetCollider = true;
        }

        if (isTargetCollider)
        {
            tapeRoot.SetParent(insertedTape.originalParent, true);

            if (insertedTape.tapeRigidbody != null)
            {
                insertedTape.tapeRigidbody.isKinematic = false;
            }

            insertedTape = null;
            isTapeInserted = false;
            isEjecting = false;
            isEjectAnimating = false;
            isTapeStopped = false;
            currentMode = 0;
            isSlotOpen = false;

            TakeOwnership();
            RequestSerialization();
            UpdateVisuals();
            _ApplyChannel();
        }
    }
}
