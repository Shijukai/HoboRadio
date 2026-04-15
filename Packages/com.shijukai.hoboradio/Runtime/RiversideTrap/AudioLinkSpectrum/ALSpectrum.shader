Shader "RiversideTrap/ALSpectrum"
{
    Properties
    {
        [Header(Base Color)]
            [HDR]_BaseColor("   Base Color", Color) = (0,1,1,1)
            _BaseIntensity ("   Intensity" ,range(0,4)) = 1
        [Header(Overrap Color)]
            _OverrapCol ("   Overrap Color", Color) = (1,1,0.5,1)
            _OverrapIntensity ("   Intensity", range(0,2)) = 0.5
            _OverrapSpeed ("   Speed" ,range(-1,1)) = 0.1
            [Enum(Bass, 0, Low_mid, 1, High_mid, 2, Treble, 3)] _OverrapType ("   Type", int) = 0
        [Header(Band Setting)]
            _BandWidth ("   Band Width", range(0.0001,81)) = 4
            _gapWidthX ("   Gap Width", range(0,100)) = 0
            _hight ("   Band Height", range(0.0001,50)) = 0.0001
            _gapWidthY ("   Gap Height", range(0,50)) = 0
            _BandTopHight ("   BandTop Height", range(0,0.02)) = 0.005
        [Header(Extra)]
            _SpectrumHight ("   Spectrum Height" ,range(0,2)) =1
            _BasePos ("   Base Position",range(0,1)) = 0.05
            [Enum(normal, 0, block, 1)] _isBlock ("   Color Type", int) = 0
            [Enum(normal, 0, horizontal, 1, vertical, 2)] _ColorMode ("   Color Mode", int) = 0
            [Enum(on, 1 , off, 0)] _WaitMode ("   Wait Mode", int) = 0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        cull off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "RGB_HSV.cginc"
            #include "Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BaseColor; float _BaseIntensity;
            float4 _OverrapCol; float _OverrapIntensity; int _OverrapType; float _OverrapSpeed;
            float _BandWidth; float _gapWidthX;
            float _hight; float _gapWidthY;
            float _BandTopHight;
            int _isBlock; int _ColorMode; int _WaitMode;
            float _SpectrumHight; float _BasePos;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

                //*********** ColorMode Horizontal(1) ***********
            float3 Color_Horizontal(float3 Color, float noteno, float uv, float GapY, float Hight){
                Color = rgb2hsv(Color);
                Color.x += noteno/240;
                Color.y -= floor(uv*240 /(GapY + Hight))*(GapY + Hight)/240 *0.7;
                Color.y = saturate(Color.y);
                Color.x = lerp(Color.x , Color.x -1 ,step(1, Color.x));
                //if(Color.x >=1)
                //    Color.x =  Color.x -1;
                Color.xyz = hsv2rgb(Color.xyz);
                return Color;
            }
                //***********************************************

                //*********** ColorMode Vertical(2) ***********
            float3 Color_Vertical(float3 Color, float uv, float GapY, float Hight){
                Color = rgb2hsv(Color);
                Color.x += floor(uv*240 /(GapY + Hight))*(GapY + Hight)/240  ;
                Color.x = lerp(Color.x , Color.x -1 ,step(1, Color.x));
                Color.xyz = hsv2rgb(Color.xyz);
                return Color;
                }
                //*********************************************
            
            float2 floorWidth(float uv, float BandWidth){
                float noteno = uv*240;
                BandWidth = floor(240/BandWidth);
                BandWidth = 240/BandWidth;
                noteno = floor(noteno/BandWidth)*BandWidth;
                return float2(noteno,BandWidth);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float _IsActive = AudioLinkData( ALPASS_GENERALVU + uint2( 8, 0 )).x;//Current intensity of Sound オーディオの有無判定用

                //_BandWidthの値で均す
                float noteno = floorWidth(i.uv.x, _BandWidth).x;
                float _BandWidthF = floorWidth(i.uv.x, _BandWidth).y;

                //隙間を開ける基準
                float _gapX = fmod((i.uv.x)*240, _BandWidthF);
                float _gapY = fmod((i.uv.y)*240, _gapWidthY + _hight);

                //*********** Overrap Color ***********
                float uvOR = 1 - i.uv.x;
                uvOR = lerp(uvOR,i.uv.x,step(0, _OverrapSpeed));
                float _Overrap = AudioLinkLerp( ALPASS_AUDIOLINK + float2( uvOR * AUDIOLINK_WIDTH * abs(_OverrapSpeed), _OverrapType ) ).r;
                float _OverrapBL = AudioLinkLerp( ALPASS_AUDIOLINK + float2( floorWidth(uvOR,_BandWidth).x/240 * AUDIOLINK_WIDTH * abs(_OverrapSpeed), _OverrapType ) ).r;
                _Overrap = _isBlock * _OverrapBL + (1-_isBlock) * _Overrap;
                _OverrapCol.xyz = _OverrapCol.xyz * _Overrap.r;
                //*************************************

                //*********** Base Color ***********
                float2 spectrum_band = ALPASS_DFT + float2( noteno, 0. );
                float3 spectrum_value =(((AudioLinkLerpMultiline( spectrum_band))*0.6 +_BasePos)*_SpectrumHight).xyz;
                float4 spectrum_color  =   float4 (spectrum_value.z+0.2, spectrum_value.z+0.2, spectrum_value.z+0.2,1);
                _BaseColor.xyz *= _BaseIntensity;
                float4 _BaseColorIdle = _BaseColor;
                _BaseColor.xyz = (_BaseColor.xyz *saturate((floor(i.uv.x) + 0.1)) * spectrum_color.xyz*10 + _OverrapCol.xyz * _OverrapIntensity)*2;
                //**********************************

                float3 _Horizontal = Color_Horizontal(_BaseColor.rgb, noteno, i.uv.y, _gapWidthY, _hight);
                float3 _Vertical = Color_Vertical(_BaseColor.rbg, i.uv.y, _gapWidthY, _hight);
                float4 _FinalColor = lerp(_BaseColor,lerp(float4(_Horizontal,1),float4(_Vertical,1),step(2,_ColorMode)) ,step(1,_ColorMode));

                spectrum_value.z = _isBlock*((floor(spectrum_value.z*240 /(_gapWidthY + _hight))+1)*(_gapWidthY + _hight)/240) + (1-_isBlock)*spectrum_value.z;

                //アイドル状態用の色
                float uvX = floor(i.uv.x / (_BandWidthF/240))*(_BandWidthF/240);
                float spectrum_value_Idle = 0.5*(sin((_Time.w + uvX*1.5 + sin(_Time.y)))/3+0.35 + 0.5*(sin((_Time.w + uvX/2)*(sin(_Time.x/10)/50))/4+0.2))+0.01;
                spectrum_value_Idle = (_WaitMode * spectrum_value_Idle + (1 - _WaitMode)*0.05)*_SpectrumHight;
                float4 _FinalColor_Idle = float4(Color_Horizontal(_BaseColor.rgb, noteno, i.uv.y, _gapWidthY, _hight),1);
                float _OverrapIdle = sin(i.uv.x+_Time.w)/2 +0.5;
                _FinalColor_Idle =  _FinalColor_Idle * float4(float3(_OverrapIdle- sin(_Time.y*3)*0.1,_OverrapIdle*sin(_Time.y/2)*0.1,_OverrapIdle+sin(_Time.y)*0.2)+0.6,1);

                //アイドル-アクティブの切り替え
                spectrum_value.z =lerp(spectrum_value.z, spectrum_value_Idle, step(_IsActive, 0));
                _FinalColor = lerp(_FinalColor, _FinalColor_Idle, step(_IsActive, 0));

                _FinalColor = lerp (_FinalColor,float4 (1,1,1,1),1- step(i.uv.y, spectrum_value.z - (_gapWidthY/240) -_BandTopHight));//トップを白に
                _FinalColor = lerp (_FinalColor,float4 (0,0,0,0),1- step(_gapWidthX/10, _gapX));//横方向の隙間のアルファを0 (_gapX < _gapWidthX/10)
                _FinalColor = lerp (_FinalColor,float4 (0,0,0,0),1- step(_gapY, _hight));//縦方向の隙間のアルファを0 (_gapY >  _hight)
                _FinalColor = lerp (_FinalColor,float4 (0,0,0,0),1- step(i.uv.y, spectrum_value.z));//描画対象外のアルファを0 (i.uv.y > spectrum_value.z)
                
                float _BlackCheck = _FinalColor.r + _FinalColor.g + _FinalColor.b;
                _FinalColor = lerp (_FinalColor,float4(0,0,0,0),step(_BlackCheck,0));
                clip(_FinalColor.w -0.1);

                return  _FinalColor;
                
            }
            ENDCG
        }
    }
}
