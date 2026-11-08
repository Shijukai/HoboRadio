---
# VPMパッケージはこちら！
https://shijukai.github.io/HoboRadio/

---


# プロジェクトに導入後の設置方法
Hierarchyを右クリックしてHoboRadioから導入したいバージョンを選択してください。  
Radio_Informationは紐づけたいHierarchy上のラジオを右クリックして選択すると導入することができます。

### 注意事項
**TMPについて**  
- このアセットはTextMeshProを使用しています。インポート後にTMPをインポートするか尋ねるウィンドウが出ますので必ずインポートしてください。

**グローバル版について**
- グローバル版はチャンネル切り替えのみ同期します、電源のON/OFFや音量は同期しませんのでご注意ください。
- v2.2.0より電源も同期するようになりました。


**ラジオの複数設置について**  
- ワールドに複数配置すると初期電源ONや時間による番組の切り替わり等のタイミングで一斉にURLを読もうとしてしまい
VRCの制約上Loadingエラーが発生する可能性があるため非推奨です。
電源の入れ直しやチャンネルの変更で再ロード出来ますが、どうしてもという場合を除きワールドに一つだけ置くようにしてください。

**パブリック化について**  
- VRChatにはパブリックインスタンスではYoutubeなどのVRChat側が信頼しているものを除き、
あらかじめ登録されたドメイン以外を弾く仕様があり、そのままだとパブリックインスタンスでのみラジオが流れません。
パブリック化する際にはウェブサイト上からワールド情報の編集画面を開き、
VideoPlayerAllowedDomainsの欄に**「shijukairadio.com」「media.shijukairadio.com」**を追加登録してください。

**Unityエディタ上でのテスト実行について**  
- クライアントを起動せずUnityエディタ上で直接再生した場合、動画ストリーミングの取得が行われないため選局ノイズSEのみが再生されます。

**音声仕様（2Dブレンド）について**  
- ステレオ音源やバイノーラル音源（ASMR等）のパン表現や定位感を維持するため、`VRC_SpatialAudioSource` を適用した上で `Spatial Blend` を2D設定としています。

---

# 各種設定の変更方法

**電源の初期状態の設定**  
- 導入後、Hierarchy上でRadio_〇〇/Radio_Rootを選択してください。
Inspectorを確認していただくと項目が並んでいると思いますので、
その中のRadio Power On のチェックボックスにチェックが入っていると初期状態で電源がオンになります。
このチェックを外すと初期状態で電源がオフになりますので、お好みで設定ください。

**初期チャンネルの設定**  
-  導入後、Hierarchy上でRadio_〇〇/Radio_Rootをクリック、
CurrentChannelIndexの数値を0-3の間で変更すると、
0でCH1、1でCH2、2でCH3、3でCH4が初期チャンネルになります。 

**カラーリングの変更/限定モデルの導入**  
- 1.上部ツールバーのToolsの中にある Shijukai/Hoboradio_ColorChange をクリックしてください。
- 2.専用のウィンドウが開きますので、Radio_Local/Radio_GlobalにHierarchyから色を変更したいラジオをD&Dしてください。
- 3.通常カラーもしくは限定モデルのタブからカラーリングを選択して置き換え実行を押すと指定したカラーリングになります。

**UI版の背景について**  
- 以下のマテリアルを編集することでカラー等の変更ができます。  
`Packages/com.shijukai.hoboradio/Runtime/Material/UI/Shijukai_UI_Background.mat`  
また、RenderingModeをCutoutに変更して、メインカラーのAlpha（不透明度）を0にすることで透明にすることも可能です。

**音量・減衰距離の変更**
- デフォルト設定では広範囲に音が届くよう設定（Far: 250）されています。ワールドの環境に合わせて調整してください。
  1. Hierarchy上で以下の3つのオブジェクトを同時に選択します。
     - `Radio_〇〇/Radio_Root/Radio_Speaker`
     - `Radio_〇〇/Radio_Root/AudioSource/Switch_SE`
     - `Radio_〇〇/Radio_Root/AudioSource/ChannelNoiseSE`
  2. Inspectorの `VRC Spatial Audio Source` コンポーネントで以下の各項目を調整します。
     - **Gain**: 音量
     - **Far**: 音が届く距離
     - **Near（Advanced Options内）**: 減衰が始まる距離

---

# ライセンス・使用素材（Credits）

本パッケージに含まれる音声素材には、以下の素材を使用しています。

- **効果音（ボタン操作音・ノイズ）:** OtoLogic (https://otologic.jp) / CC BY 4.0
