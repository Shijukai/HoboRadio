using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HoboRadio_Signage : UdonSharpBehaviour
{
    private const int SlotCount = 16;
    private const int StateCount = 5; // 0..3: CH1..CH4, 4: PowerOff / Tape
    private const int OfflineStateIndex = 4;

    [Header("--- 連携・表示UI設定 ---")]
    [SerializeField] private HoboRadio_Controller radioController;
    [SerializeField] private RawImage targetImage;
    [SerializeField] private Texture2D defaultFallbackTexture;
    [Tooltip("0以下の場合は起動時のRectTransform横幅を基準にします")]
    [SerializeField] private float fixedDisplayWidth = 0f;

    [Header("--- 固定URL設定（最大16スロット） ---")]
    [SerializeField] private VRCUrl[] lowResUrls = new VRCUrl[SlotCount];
    [Tooltip("未設定（空）または軽量版と同一URLの場合は軽量版テクスチャを共有します")]
    [SerializeField] private VRCUrl[] highResUrls = new VRCUrl[SlotCount];

    [Header("--- JSONメタデータ連携（正式経路） ---")]
    [Tooltip("全チャンネル（ch1〜ch4）およびofflineをまとめたJSONのURL")]
    [SerializeField] private VRCUrl signageJsonUrl;
    [SerializeField] private float defaultSlideInterval = 10.0f;
    [SerializeField] private float jsonFetchDelay = 3.0f;

    [Header("--- 暫定制御設定（JSON未設定・暫定運用時） ---")]
    [Tooltip("有効時はJSON取得を行わず、以下のInspector設定を使用します（JSON URL未設定時も自動適用）")]
    [SerializeField] private bool useInspectorPreset = true;

    [SerializeField] private int[] ch1Slots = new int[] { 0 };
    [SerializeField] private int[] ch2Slots = new int[] { 1 };
    [SerializeField] private int[] ch3Slots = new int[0];
    [SerializeField] private int[] ch4Slots = new int[] { 3 };

    [Tooltip("電源OFF・テープ再生時に表示するスロット番号（現在利用可能な1・2・4CH番組表）")]
    [SerializeField] private int[] offlineSlots = new int[] { 0, 1, 3 };

    [Header("--- メモリ・通信・操作パラメータ ---")]
    [SerializeField] private int maxHighResCache = 3;
    [SerializeField] private float manualModeTimeout = 30.0f;
    [SerializeField] private float imageDownloadInterval = 5.5f;

    // Image Cache
    private Texture2D[] _lowResTextures = new Texture2D[SlotCount];
    private IVRCImageDownload[] _lowResDownloads = new IVRCImageDownload[SlotCount];
    private bool[] _lowResFailed = new bool[SlotCount];

    private Texture2D[] _highResTextures = new Texture2D[SlotCount];
    private IVRCImageDownload[] _highResDownloads = new IVRCImageDownload[SlotCount];
    private float[] _highResLastUsedTime = new float[SlotCount];
    private bool[] _highResFailed = new bool[SlotCount];

    private VRCImageDownloader _imageDownloader;
    private IVRCImageDownload _activeImageDownload;
    private int _downloadingSlot = -1;
    private bool _downloadingHighRes = false;
    private float _lastImageRequestTime = -10f;

    // JSON Cache & Queue
    private string _cachedJsonString = "";
    private bool _hasFetchedJson = false;
    private float _lastStringRequestTime = -10f;
    private bool _isJsonRequestPending = false;
    private float _jsonScheduledTime = 0f;

    // Slide Rotation State
    private int[] _activeSlots = new int[0];
    private float[] _activeIntervals = new float[0];
    private int _currentSlideIndex = 0;
    private float _slideTimer = 0f;
    private float _initialWidth = 100f;

    // Radio & Interaction State
    private int _lastRadioStateIndex = -1;
    private bool _isPlayerNear = false;
    private bool _isManualMode = false;
    private float _lastInteractTime = 0f;

    private void Start()
    {
        _imageDownloader = new VRCImageDownloader();

        if (targetImage != null)
        {
            _initialWidth = targetImage.rectTransform.sizeDelta.x;
            if (_initialWidth <= 0f) _initialWidth = 100f;
        }

        ApplyTextureToUI(defaultFallbackTexture);

        int initialState = GetCurrentRadioStateIndex();
        _lastRadioStateIndex = initialState;
        OnRadioStateChanged(initialState);
    }

    private void OnDestroy()
    {
        DisposeAllHighRes();

        for (int i = 0; i < SlotCount; i++)
        {
            if (_lowResDownloads[i] != null)
            {
                _lowResDownloads[i].Dispose();
                _lowResDownloads[i] = null;
            }
            _lowResTextures[i] = null;
        }

        if (_imageDownloader != null)
        {
            _imageDownloader.Dispose();
            _imageDownloader = null;
        }
    }

    private void Update()
    {
        // 1. ラジオ本体状態の監視（電源・モード・チャンネル）
        int currentState = GetCurrentRadioStateIndex();
        if (currentState != _lastRadioStateIndex)
        {
            _lastRadioStateIndex = currentState;
            OnRadioStateChanged(currentState);
        }

        // 2. JSON取得キューの処理（音声・文字情報通信優先のため遅延＋レート制限遵守）
        ProcessJsonQueue();

        // 3. 画像ダウンロードキューの処理（5.5秒間隔で1枚ずつ取得）
        ProcessImageQueue();

        // 4. 手動送りモードの無操作タイムアウト判定
        if (_isManualMode && Time.timeSinceLevelLoad - _lastInteractTime >= manualModeTimeout)
        {
            _isManualMode = false;
            _slideTimer = 0f;
            if (!_isPlayerNear)
            {
                DisposeAllHighRes();
                DisplayCurrentSlide();
            }
        }

        // 5. 自動ローテーションタイマー
        if (!_isManualMode && _activeSlots.Length > 1)
        {
            _slideTimer += Time.deltaTime;
            float interval = (_currentSlideIndex < _activeIntervals.Length && _activeIntervals[_currentSlideIndex] > 0f)
                ? _activeIntervals[_currentSlideIndex]
                : defaultSlideInterval;

            if (_slideTimer >= interval)
            {
                _slideTimer = 0f;
                _currentSlideIndex = (_currentSlideIndex + 1) % _activeSlots.Length;
                DisplayCurrentSlide();
            }
        }
    }

    public void OnSignageInteract()
    {
        if (_activeSlots.Length == 0) return;

        _isManualMode = true;
        _lastInteractTime = Time.timeSinceLevelLoad;
        _slideTimer = 0f;
        _currentSlideIndex = (_currentSlideIndex + 1) % _activeSlots.Length;
        DisplayCurrentSlide();
    }

    public void OnPlayerApproachEnter()
    {
        _isPlayerNear = true;
        DisplayCurrentSlide();
    }

    public void OnPlayerApproachExit()
    {
        _isPlayerNear = false;
        _isManualMode = false;
        DisposeAllHighRes();
        DisplayCurrentSlide();
    }

    private int GetCurrentRadioStateIndex()
    {
        if (radioController == null || !radioController.radioPowerOn || radioController.currentMode != 0)
        {
            return OfflineStateIndex;
        }

        int ch = radioController.currentChannelIndex;
        if (ch < 0 || ch >= 4) return OfflineStateIndex;
        return ch;
    }

    private void OnRadioStateChanged(int stateIndex)
    {
        bool hasValidJsonUrl = signageJsonUrl != null && !string.IsNullOrEmpty(signageJsonUrl.Get());

        if (useInspectorPreset || !hasValidJsonUrl)
        {
            _isJsonRequestPending = false;
            ApplyInspectorPreset(stateIndex);
            return;
        }

        // 取得済みJSONがあればローカルから該当系統を即座に適用
        if (!string.IsNullOrEmpty(_cachedJsonString))
        {
            if (!TryApplyJsonForState(_cachedJsonString, stateIndex))
            {
                ApplyInspectorPreset(stateIndex);
            }
            return;
        }

        // 未取得時は暫定プリセットで表示しつつ、通信競合を避けるため遅延してJSON取得を予約
        ApplyInspectorPreset(stateIndex);
        if (!_hasFetchedJson && !_isJsonRequestPending)
        {
            _jsonScheduledTime = Time.timeSinceLevelLoad + jsonFetchDelay;
            _isJsonRequestPending = true;
        }
    }

    private void ProcessJsonQueue()
    {
        if (!_isJsonRequestPending) return;
        if (Time.timeSinceLevelLoad < _jsonScheduledTime) return;
        if (Time.timeSinceLevelLoad - _lastStringRequestTime < 5.5f) return;

        _isJsonRequestPending = false;

        if (signageJsonUrl == null || string.IsNullOrEmpty(signageJsonUrl.Get())) return;

        _lastStringRequestTime = Time.timeSinceLevelLoad;
        VRCStringDownloader.LoadUrl(signageJsonUrl, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (result == null || string.IsNullOrEmpty(result.Result)) return;

        _hasFetchedJson = true;
        _cachedJsonString = result.Result;
        if (!useInspectorPreset)
        {
            if (!TryApplyJsonForState(_cachedJsonString, _lastRadioStateIndex))
            {
                ApplyInspectorPreset(_lastRadioStateIndex);
            }
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogWarning($"[HoboRadio_Signage] JSON Load Error: {(result != null ? result.Error : "Unknown")}");
    }

    private string GetStateJsonKey(int stateIndex)
    {
        switch (stateIndex)
        {
            case 0: return "ch1";
            case 1: return "ch2";
            case 2: return "ch3";
            case 3: return "ch4";
            default: return "offline";
        }
    }

    private bool TryApplyJsonForState(string jsonStr, int stateIndex)
    {
        if (!VRCJson.TryDeserializeFromJson(jsonStr, out DataToken rootToken)) return false;
        if (rootToken.TokenType != TokenType.DataDictionary) return false;

        DataDictionary rootDict = rootToken.DataDictionary;
        float fallbackInterval = defaultSlideInterval;

        if (rootDict.TryGetValue("defaultInterval", TokenType.Double, out DataToken defIntToken))
        {
            float parsedDef = (float)defIntToken.Double;
            if (parsedDef > 0f) fallbackInterval = parsedDef;
        }

        string stateKey = GetStateJsonKey(stateIndex);
        if (!rootDict.TryGetValue(stateKey, TokenType.DataList, out DataToken slidesToken)) return false;

        DataList slidesList = slidesToken.DataList;
        int rawCount = slidesList.Count;
        if (rawCount == 0)
        {
            SetSlideDeck(new int[0], new float[0]);
            return true;
        }

        int[] tempSlots = new int[rawCount];
        float[] tempIntervals = new float[rawCount];
        int validCount = 0;

        for (int i = 0; i < rawCount; i++)
        {
            if (!slidesList.TryGetValue(i, TokenType.DataDictionary, out DataToken itemToken)) continue;
            DataDictionary itemDict = itemToken.DataDictionary;

            if (!itemDict.TryGetValue("slot", TokenType.Double, out DataToken slotToken)) continue;
            int slot = (int)slotToken.Double;
            if (slot < 0 || slot >= SlotCount) continue;

            float interval = fallbackInterval;
            if (itemDict.TryGetValue("interval", TokenType.Double, out DataToken intToken))
            {
                float parsedInt = (float)intToken.Double;
                if (parsedInt > 0f) interval = parsedInt;
            }

            tempSlots[validCount] = slot;
            tempIntervals[validCount] = interval;
            validCount++;
        }

        int[] finalSlots = new int[validCount];
        float[] finalIntervals = new float[validCount];
        Array.Copy(tempSlots, finalSlots, validCount);
        Array.Copy(tempIntervals, finalIntervals, validCount);

        SetSlideDeck(finalSlots, finalIntervals);
        return true;
    }

    private void ApplyInspectorPreset(int stateIndex)
    {
        int[] srcSlots;

        switch (stateIndex)
        {
            case 0:
                srcSlots = ch1Slots;
                break;
            case 1:
                srcSlots = ch2Slots;
                break;
            case 2:
                srcSlots = ch3Slots;
                break;
            case 3:
                srcSlots = ch4Slots;
                break;
            default:
                srcSlots = offlineSlots;
                break;
        }

        if (srcSlots == null || srcSlots.Length == 0)
        {
            SetSlideDeck(new int[0], new float[0]);
            return;
        }

        int[] tempSlots = new int[srcSlots.Length];
        float[] tempIntervals = new float[srcSlots.Length];
        int validCount = 0;

        for (int i = 0; i < srcSlots.Length; i++)
        {
            int s = srcSlots[i];
            if (s < 0 || s >= SlotCount) continue;

            tempSlots[validCount] = s;
            tempIntervals[validCount] = defaultSlideInterval;
            validCount++;
        }

        int[] finalSlots = new int[validCount];
        float[] finalIntervals = new float[validCount];
        Array.Copy(tempSlots, finalSlots, validCount);
        Array.Copy(tempIntervals, finalIntervals, validCount);

        SetSlideDeck(finalSlots, finalIntervals);
    }

    private void SetSlideDeck(int[] slots, float[] intervals)
    {
        int prevSlot = (_activeSlots.Length > 0 && _currentSlideIndex < _activeSlots.Length)
            ? _activeSlots[_currentSlideIndex]
            : -1;

        _activeSlots = slots;
        _activeIntervals = intervals;
        _slideTimer = 0f;

        if (_activeSlots.Length == 0)
        {
            _currentSlideIndex = 0;
            DisplayCurrentSlide();
            return;
        }

        int preservedIndex = -1;
        for (int i = 0; i < _activeSlots.Length; i++)
        {
            if (_activeSlots[i] == prevSlot)
            {
                preservedIndex = i;
                break;
            }
        }

        _currentSlideIndex = preservedIndex >= 0 ? preservedIndex : 0;
        DisplayCurrentSlide();
    }

    private void DisplayCurrentSlide()
    {
        if (_activeSlots.Length == 0)
        {
            ApplyTextureToUI(defaultFallbackTexture);
            return;
        }

        int slot = _activeSlots[_currentSlideIndex];

        if (_isPlayerNear || _isManualMode)
        {
            if (_highResTextures[slot] != null)
            {
                _highResLastUsedTime[slot] = Time.timeSinceLevelLoad;
                ApplyTextureToUI(_highResTextures[slot]);
                return;
            }
        }

        if (_lowResTextures[slot] != null)
        {
            ApplyTextureToUI(_lowResTextures[slot]);
            return;
        }

        ApplyTextureToUI(defaultFallbackTexture);
    }

    private void ApplyTextureToUI(Texture2D tex)
    {
        if (targetImage == null) return;

        targetImage.texture = tex;

        if (tex != null && tex.width > 0 && tex.height > 0)
        {
            RectTransform rt = targetImage.rectTransform;
            float width = fixedDisplayWidth > 0f ? fixedDisplayWidth : _initialWidth;
            float height = width * ((float)tex.height / (float)tex.width);
            rt.sizeDelta = new Vector2(width, height);
        }
    }

    private void ProcessImageQueue()
    {
        if (_imageDownloader == null || _activeImageDownload != null || _activeSlots.Length == 0) return;
        if (Time.timeSinceLevelLoad - _lastImageRequestTime < imageDownloadInterval) return;

        // 優先1: 接近または手動操作中の現在表示スロットの高画質版オンデマンド取得
        if (_isPlayerNear || _isManualMode)
        {
            int currentSlot = _activeSlots[_currentSlideIndex];
            if (_highResTextures[currentSlot] == null && !_highResFailed[currentSlot])
            {
                VRCUrl lowUrl = GetValidUrl(lowResUrls, currentSlot);
                VRCUrl highUrl = GetValidUrl(highResUrls, currentSlot);
                VRCUrl resolvedHighUrl = highUrl != null ? highUrl : lowUrl;

                if (resolvedHighUrl != null)
                {
                    // 高画質URLが未設定または軽量版と同一URLで既に軽量版取得済みの場合は参照共有
                    if (lowUrl != null && resolvedHighUrl.Get() == lowUrl.Get() && _lowResTextures[currentSlot] != null)
                    {
                        _highResTextures[currentSlot] = _lowResTextures[currentSlot];
                        _highResLastUsedTime[currentSlot] = Time.timeSinceLevelLoad;
                        DisplayCurrentSlide();
                        return;
                    }

                    StartImageDownload(currentSlot, resolvedHighUrl, true);
                    return;
                }
            }
        }

        // 優先2: アクティブスライド内の軽量版を現在表示位置から順に1枚ずつプリロード
        int count = _activeSlots.Length;
        for (int offset = 0; offset < count; offset++)
        {
            int idx = (_currentSlideIndex + offset) % count;
            int slot = _activeSlots[idx];

            if (_lowResTextures[slot] == null && !_lowResFailed[slot])
            {
                VRCUrl lowUrl = GetValidUrl(lowResUrls, slot);
                if (lowUrl != null)
                {
                    StartImageDownload(slot, lowUrl, false);
                    return;
                }
            }
        }
    }

    private VRCUrl GetValidUrl(VRCUrl[] urlArray, int slot)
    {
        if (urlArray == null || slot < 0 || slot >= urlArray.Length) return null;
        VRCUrl url = urlArray[slot];
        if (url == null || string.IsNullOrEmpty(url.Get())) return null;
        return url;
    }

    private void StartImageDownload(int slot, VRCUrl url, bool isHighRes)
    {
        _lastImageRequestTime = Time.timeSinceLevelLoad;
        _downloadingSlot = slot;
        _downloadingHighRes = isHighRes;
        _activeImageDownload = _imageDownloader.DownloadImage(url, null, (IUdonEventReceiver)this, null);
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        _activeImageDownload = null;
        if (result == null) return;

        int slot = _downloadingSlot;
        bool isHighRes = _downloadingHighRes;
        _downloadingSlot = -1;

        if (slot < 0 || slot >= SlotCount || result.Result == null)
        {
            result.Dispose();
            return;
        }

        if (isHighRes)
        {
            if (_highResDownloads[slot] != null)
            {
                _highResDownloads[slot].Dispose();
            }
            _highResDownloads[slot] = result;
            _highResTextures[slot] = result.Result;
            _highResLastUsedTime[slot] = Time.timeSinceLevelLoad;
            EnforceHighResCacheLimit();
        }
        else
        {
            if (_lowResDownloads[slot] != null)
            {
                _lowResDownloads[slot].Dispose();
            }
            _lowResDownloads[slot] = result;
            _lowResTextures[slot] = result.Result;
        }

        if (_activeSlots.Length > 0 && _activeSlots[_currentSlideIndex] == slot)
        {
            DisplayCurrentSlide();
        }
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        _activeImageDownload = null;

        int slot = _downloadingSlot;
        bool isHighRes = _downloadingHighRes;
        _downloadingSlot = -1;

        if (slot >= 0 && slot < SlotCount)
        {
            if (isHighRes)
            {
                _highResFailed[slot] = true;
            }
            else
            {
                _lowResFailed[slot] = true;
            }
        }

        if (result != null)
        {
            Debug.LogWarning($"[HoboRadio_Signage] Image Load Error (Slot {slot}, HighRes={isHighRes}): {result.Error}");
            result.Dispose();
        }

        if (_activeSlots.Length > 0 && _activeSlots[_currentSlideIndex] == slot)
        {
            DisplayCurrentSlide();
        }
    }

    private void EnforceHighResCacheLimit()
    {
        int limit = Mathf.Max(1, maxHighResCache);

        while (true)
        {
            int count = 0;
            int oldestSlot = -1;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_highResDownloads[i] != null)
                {
                    count++;
                    if (_highResLastUsedTime[i] < oldestTime)
                    {
                        oldestTime = _highResLastUsedTime[i];
                        oldestSlot = i;
                    }
                }
            }

            if (count <= limit || oldestSlot < 0) break;

            _highResDownloads[oldestSlot].Dispose();
            _highResDownloads[oldestSlot] = null;
            _highResTextures[oldestSlot] = null;
            _highResLastUsedTime[oldestSlot] = 0f;
        }
    }

    private void DisposeAllHighRes()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_highResDownloads[i] != null)
            {
                _highResDownloads[i].Dispose();
                _highResDownloads[i] = null;
            }
            _highResTextures[i] = null;
            _highResLastUsedTime[i] = 0f;
            _highResFailed[i] = false;
        }
    }
}
