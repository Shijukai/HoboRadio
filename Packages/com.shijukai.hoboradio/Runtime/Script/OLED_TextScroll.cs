using UnityEngine;
using TMPro;
using UdonSharp;

public class OLED_TextScroll : UdonSharpBehaviour
{
    [Header("Scrolling Objects")]
    [SerializeField] private RectTransform textA;
    [SerializeField] private RectTransform textB;

    [Header("Settings")]
    [SerializeField] private float scrollSpeed = 30f;
    [SerializeField] private float spacing = 50f;
    [SerializeField] private TextMeshProUGUI masterTmp; // Fetcherが書き換えるマスターテキスト

    private TextMeshProUGUI _tmpA;
    private TextMeshProUGUI _tmpB;
    private float _widthA;
    private float _widthB;
    private Vector2 _posA;
    private Vector2 _posB;
    private float _startX;

    private string _currentText = "";  // 現在流しているテキスト
    private string _pendingText = "";  // 次に流す予定のテキスト
    private bool _isInitialized = false;

    void Start()
    {
        _tmpA = textA.GetComponent<TextMeshProUGUI>();
        _tmpB = textB.GetComponent<TextMeshProUGUI>();
        _startX = textA.anchoredPosition.x;

        // 初期化：最初はマスターの文字を入れる
        if (masterTmp != null)
        {
            _currentText = masterTmp.text;
            _pendingText = _currentText;
            _tmpA.text = _currentText;
            _tmpB.text = _currentText;
        }

        SendCustomEventDelayedFrames(nameof(_ForceLayoutUpdate), 5);
    }

    public void _ForceLayoutUpdate()
    {
        _widthA = textA.rect.width;
        _widthB = textB.rect.width;

        _posA = new Vector2(_startX, 0);
        _posB = new Vector2(_startX + _widthA + spacing, 0);

        textA.anchoredPosition = _posA;
        textB.anchoredPosition = _posB;
        _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized) return;

        // 1. Fetcherの更新を監視（予約を入れる）
        if (masterTmp != null && masterTmp.text != _pendingText)
        {
            _pendingText = masterTmp.text;
        }

        float moveAmount = scrollSpeed * Time.deltaTime;
        _posA.x -= moveAmount;
        _posB.x -= moveAmount;

        // 2. Text A が左に消えたとき
        if (_posA.x <= _startX - _widthA - spacing)
        {
            // ここで内容を最新（Pending）に更新！
            _tmpA.text = _pendingText;
            // 数フレーム待たずに即座に幅を再計算（文字が変わったので）
            Canvas.ForceUpdateCanvases();
            _widthA = textA.rect.width;

            // Bの後ろにピタッとくっつける
            _posA.x = _posB.x + _widthB + spacing;
        }

        // 3. Text B が左に消えたとき
        if (_posB.x <= _startX - _widthB - spacing)
        {
            _tmpB.text = _pendingText;
            Canvas.ForceUpdateCanvases();
            _widthB = textB.rect.width;

            // Aの後ろにピタッとくっつける
            _posB.x = _posA.x + _widthA + spacing;
        }

        textA.anchoredPosition = _posA;
        textB.anchoredPosition = _posB;
    }
}