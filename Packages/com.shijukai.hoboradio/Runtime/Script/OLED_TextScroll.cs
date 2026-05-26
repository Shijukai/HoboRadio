using UnityEngine;
using TMPro;
using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OLED_TextScroll : UdonSharpBehaviour
{
    [Header("Scrolling Objects")]
    [SerializeField] private RectTransform textA;
    [SerializeField] private RectTransform textB;

    [Header("Settings")]
    [SerializeField] private float scrollSpeed = 30f;
    [SerializeField] private float spacing = 50f;
    [SerializeField] private TextMeshProUGUI masterTmp;

    private TextMeshProUGUI _tmpA;
    private TextMeshProUGUI _tmpB;
    private float _widthA;
    private float _widthB;
    private Vector2 _posA;
    private Vector2 _posB;
    private float _startX;

    private string _pendingText = "";
    private bool _isInitialized = false;
    private bool _isWaitingA = false;
    private bool _isWaitingB = false;

    void Start()
    {
        if (textA != null)
        {
            _tmpA = textA.GetComponent<TextMeshProUGUI>();
            _startX = textA.anchoredPosition.x;
        }
        if (textB != null)
        {
            _tmpB = textB.GetComponent<TextMeshProUGUI>();
        }
    }

    public void ResetScroll()
    {
        if (masterTmp == null || _tmpA == null || _tmpB == null) return;
        _isInitialized = false;
        _isWaitingA = false;
        _isWaitingB = false;

        _pendingText = masterTmp.text;
        _tmpA.text = _pendingText;
        _tmpB.text = _pendingText;

        // 文字枠が更新されるのを待ってから初期配置します
        SendCustomEventDelayedFrames(nameof(_ApplyReset), 2);
    }

    public void _ApplyReset()
    {
        if (textA == null || textB == null) return;

        _widthA = textA.rect.width;
        _widthB = textB.rect.width;

        _posA = new Vector2(_startX, textA.anchoredPosition.y);

        // 中心(Pivot=0.5)を基準に、Aの幅の半分とBの幅の半分を足して完璧に繋げます
        _posB = new Vector2(_startX + (_widthA / 2f) + (_widthB / 2f) + spacing, textB.anchoredPosition.y);

        textA.anchoredPosition = _posA;
        textB.anchoredPosition = _posB;
        _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized) return;

        if (masterTmp != null && masterTmp.text != _pendingText)
        {
            _pendingText = masterTmp.text;
        }

        float moveAmount = scrollSpeed * Time.deltaTime;

        // 待機中（幅の更新待ち）でなければスクロールを進める
        _posA.x -= moveAmount;
        _posB.x -= moveAmount;

        // 画面外判定（お嬢様の元のロジックを尊重）
        float leftBoundaryA = _startX - _widthA - spacing;
        if (!_isWaitingA && _posA.x <= leftBoundaryA)
        {
            _tmpA.text = _pendingText;
            _isWaitingA = true;
            // 文字を変えたら、すぐに移動させず2フレーム待つ（ここで空白バグを防ぎます！）
            SendCustomEventDelayedFrames(nameof(_RepositionA), 2);
        }

        float leftBoundaryB = _startX - _widthB - spacing;
        if (!_isWaitingB && _posB.x <= leftBoundaryB)
        {
            _tmpB.text = _pendingText;
            _isWaitingB = true;
            SendCustomEventDelayedFrames(nameof(_RepositionB), 2);
        }

        textA.anchoredPosition = _isWaitingA ? new Vector2(_posA.x, 9999f) : _posA;
        textB.anchoredPosition = _isWaitingB ? new Vector2(_posB.x, 9999f) : _posB;
    }

    public void _RepositionA()
    {
        if (textA == null) return;
        _widthA = textA.rect.width; // 2フレーム待ったので、最新の正しい幅が取れます
        _posA.x = _posB.x + (_widthB / 2f) + (_widthA / 2f) + spacing;
        _isWaitingA = false;

        textA.anchoredPosition = _posA;
    }

    public void _RepositionB()
    {
        if (textB == null) return;
        _widthB = textB.rect.width;
        _posB.x = _posA.x + (_widthA / 2f) + (_widthB / 2f) + spacing;
        _isWaitingB = false;

        textB.anchoredPosition = _posB;
    }
}
