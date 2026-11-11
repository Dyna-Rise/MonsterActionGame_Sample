# Battle Player

Battle シーンのプレイヤーは **「共通部分」＋「タイプ（武器）部分」** に分かれています。
共通部分は先生が管理し、各担当は自分のフォルダの中だけを触れば開発できるようにしています。

## 操作
| 操作 | キーボード/マウス | ゲームパッド | 担当 |
|---|---|---|---|
| 移動 | WASD | 左スティック | 共通 |
| カメラ | マウス | 右スティック | 共通 |
| ダッシュ（押している間） | 左Shift | R1（またはL3） | 共通 |
| 回避（ローリング） | Space | ×（South） | 共通 |
| ターゲットカメラ（ロックオン）切替 | Q / マウス中ボタン | 〇（East） | 共通 |
| 集中モード（押している間） | マウス右ボタン | L1 | 共通 |
| 攻撃 | 左クリック | □（West） | **タイプ別** |
| ガード（片手剣） | 左クリック長押し | □長押し | タイプA |
| アイテム使用（回復薬） | R | △（North） | 共通 |
| カーソル解除 / 再ロック | Esc / クリック | - | 共通 |

- ターゲットカメラ：近くの `LockOnTarget`（ボスなど）を画面に捉え続けます。相手がいないときに押すとカメラが背後に戻ります
- 集中モード：カメラが肩越しに寄り、画面中央に照準が出ます。キャラはカメラの向きを向いたまま8方向に走ります

## セットアップ
メニュー `MonsterAction > Player` から：
1. `Build PlayerA (Sword & Shield)` … 片手剣の Animator と Prefab を作る
2. `Build PlayerB (Bow) Template` … 弓の雛形を作る（攻撃なし。弓担当が作り込む）
3. Project ウィンドウで使いたい Player の Prefab を選んで `Place Selected Player Prefab + Camera In Scene`
   （シーンのプレイヤーを入れ替えるときもこれ。カメラ・HUD・BattleFlow も自動で設定されます）
4. `Place Training Dummy In Scene` … 打たれ役（ロックオンもできる・攻撃してくる）を置く

### カカシ（テスト用の敵）
| コンポーネント | 役割 |
|---|---|
| `TrainingDummy` | 攻撃を受ける（Console にダメージ表示）。`Quest Clear Damage` でクリアのテスト |
| `LockOnTarget` | ロックオン対象 |
| `DummyAttacker` | **遠ければ球を撃つ（黄色に点滅が予兆）／近ければ周囲を殴る（赤く点滅が予兆）**。回避・被弾・ゲームオーバーのテスト用。止めたいときはコンポーネントのチェックを外す |

`DummyAttacker`（距離で行動を選ぶ → 予兆 → 攻撃 → 待ち）と `DummyProjectile`（まっすぐ飛ぶ弾）は、
ボスの AI や弓の矢を作るときの一番シンプルな見本にもなります。

HUD だけを作り直す / 置くとき：`MonsterAction > Battle > Build Battle HUD Prefab` / `Setup HUD + BattleFlow In Scene`

## バトルの流れ（BattleFlow）
| できごと | 流れ |
|---|---|
| プレイヤー死亡 | 2秒後に「QUEST FAILED」→ 5秒後に **Home シーン**へ |
| ボス討伐 | ボスが `BattleFlowController.Instance.ClearQuest()` を呼ぶ → 「QUEST CLEAR」→ 5秒後に Home へ |

- 秒数・戻り先シーン名はシーンの `BattleFlow` オブジェクトで変更できます
- テスト用キー：**F1 = 自分を倒す（失敗）、F2 = クエストクリア**（`Debug Keys` で OFF にできる）
- カカシの `Quest Clear Damage` に値を入れると、その合計ダメージでクリアになります（ボス討伐の練習）
- 報酬金の加算などは、Home と共有するデータを作ってから `QuestCleared` / `QuestFailed` イベントに足す予定

## アイテムと所持金（Home と共有）
| しくみ | 場所 |
|---|---|
| アイテムの設定（名前・値段・回復量・最大数） | `ItemDefinition`（ScriptableObject）。`Assets/Game/Resources/Items/` に置く。右クリック → Create → MonsterAction → Item |
| 所持金・アイテムの所持数 | `GameData`（static。シーンを切り替えても残る。Play のたびに初期値：所持金 300G・回復薬 2 個） |

- **回復薬**：△ / R で使う。1秒その場で使い、0.6秒で最大HPの30%回復。効果が出る前に攻撃を受けると失敗（減らない）、回避でキャンセル可
- 右下に残り数を表示（0 個で暗くなる）。回復すると緑の火花と回復量の数字
- **報酬金**：ボスの `Reward Money`（初期 1000G）。倒すと所持金に加算し、結果画面に「報酬金 / 所持金」を表示

### Home のショップ担当向け
```csharp
foreach (var item in ItemDefinition.All) { /* item.DisplayName, item.Price, item.Description, item.Icon */ }
GameData.Money                   // 所持金
GameData.GetCount(item)          // 所持数
GameData.TryBuy(item)            // 買う（お金不足 / 最大数なら false）
GameData.MoneyChanged += ...     // 表示更新用イベント
GameData.ItemCountChanged += ...
GameData.GetSkillLevel(type)     // スキルの★（0〜3）
GameData.TryBuySkill(type)       // ★を1つ上げる（お金不足 / 売り切れなら false）
```

## Home（村）
`MonsterAction > Home > Setup Home Scene` を Home シーンで実行すると、全部そろいます（何度実行しても OK）。

| 置かれるもの | 内容 |
|---|---|
| `HomePlayer` | 村を歩くだけのプレイヤー（移動・R1 で走る）|
| カメラ | `HomeCameraController`：角度固定の見下ろし（回転しない）。`Pitch`（見下ろす角度）・`Yaw`（向き）・`Distance` で調整 |
| ドア `Door_painted.001` | 近づくと光る → □ で **装備画面**（`EquipmentMenu`）|
| 村人 `Villager_Shop` | 近づくと光る → □ で **ショップ**（`ShopMenu`）。位置は自由に動かしてOK |
| 出撃口 `DepartureGate` | 柵の間などに置く緑の箱（Scene ビューで表示）。入ると **出撃画面**（`QuestMenu`）→ モンスターを選んで「出撃する」→ Battle へ。閉じたら一度外に出るまで反応しない |
| `HomeHUD` | 右上に所持金、下に「□ 話しかける」などの案内 |

- メニューの操作：✕ / Enter / クリックで決定、〇 / Esc / 右上の × で閉じる
- 調べられる物を増やす：物に `Interactable` を付け、`On Interact` の ＋ で開きたいメニューの `Open` を登録
  （近づくと自動で光ります。光り方は `Highlight Color`、届く距離は `Interact Radius`）

### スキル（`Common/Data/SkillData.cs` で値段・倍率を変更）
| ★ | 倍率 | 値段 |
|---|---|---|
| ★1 | 1.05 | 300G |
| ★2 | 1.10 | 700G |
| ★3 | 1.20 | 1500G（これで売り切れ）|

- 体力アップ（最大HP）／ 攻撃力アップ（与えるダメージ）／ 防御力アップ（受けるダメージ ÷ 倍率）／ スタミナアップ（最大スタミナ）
- 効果があるのは**装備画面で付けたスキルだけ**。同時に付けられるのは `GameData.MaxEquippedSkills`（2個）まで。初めて買ったときは空きがあれば自動で付く
- 回復薬はショップで 100G、10 個まで（それ以上は「所持上限」）

### プレイヤータイプ
- 装備画面で選んだタイプ（`GameData.SelectedPlayerTypeId`）が、Battle の `PlayerSpawner`（`BattlePlayerSpawner`）で出てきます
- Battle シーンで `Setup HUD + BattleFlow In Scene` を実行すると `PlayerSpawner` が置かれます
- 新しいタイプは、`EquipmentMenu` の `Player Types` と `PlayerSpawner` の `Types` に同じ ID で1行ずつ追加

### 出撃（モンスター選択）
- 出撃画面で選んだボス（`GameData.SelectedBossId`）が、Battle の `BossSpawner`（`BattleBossSpawner`）でシーンのボスと差し替わります
- Battle シーンで `Setup HUD + BattleFlow In Scene` を実行すると `BossSpawner` が置かれます（空の Prefab 欄には `Prefabs/Boss/Boss_<ID>.prefab` が自動で入る）
- 新しいボスは、`QuestMenu` の `Quests` と `BossSpawner` の `Bosses` に同じ ID で1行ずつ追加（`Boss Prefab` を入れると報酬金も表示される）

## ヒットエフェクト（HitEffects）
シーンの `HitEffects` オブジェクトが、プレイヤー / ボスの `Damaged`・`Guarded` イベントを聞いて演出を出します。
| 当たったとき | 演出 |
|---|---|
| Player → Boss | 火花（弱点は橙で大きめ）＋ダメージの数字＋一瞬のヒットストップ |
| Boss → Player | 赤い火花＋画面の縁が赤く光る＋カメラの揺れ（ダウンは強め） |
| ガード成功 | 水色の火花＋小さな揺れ |
色・ヒットストップの長さ・揺れの強さはインスペクターで調整できます。
ガードのような「防いだ」演出を出したいタイプは、`NotifyGuarded(info)` を呼んでください。

## HUD（Canvas）
`Assets/Game/Prefabs/UI/BattleHUD.prefab` を開くと中身を編集できます。
```
BattleHUD            Canvas ＋ PlayerStatusHUD（各 UI をインスペクターで参照）
├ StatusBars
│ ├ HPBar            Back / Fill（Image Type = Filled。fillAmount で長さ）
│ └ StaminaBar       Back / Fill（疲労中は色が変わる）
├ Reticle            集中モードの照準（L1 中だけ表示）
├ LockOnMarker       ロックオン中、相手の位置に表示
└ ResultPanel        QUEST FAILED / CLEAR（BattleResultUI。普段は透明）
```

## フォルダ構成（Assets/Game/Scripts）
```
Common/Combat/            ← 全員共通（Player も Boss も使う）
  IDamageable.cs            攻撃を受けられるもの
  DamageInfo.cs             ダメージ情報
  LockOnTarget.cs           ロックオンの対象になれる印（ボスに付ける）
Battle/
  Player/
    Core/                 ← 共通（先生が管理）
      PlayerBattleController.cs  移動・ダッシュ・スタミナ・回避・被弾・死亡・ロックオン・集中モード・照準
      PlayerInputReader.cs       入力と先行入力
    Weapons/
      WeaponHitbox.cs       近接武器の当たり判定（剣以外の近接タイプでも使える）
    Types/
      SwordShield/        ← タイプA：片手剣（見本）
        PlayerTypeA_SwordShield.cs
        AttackStep.cs
      Bow/                ← タイプB：弓（★ 弓担当の作業場所）
        PlayerTypeB_Bow.cs
  Camera/BattleCameraController.cs   通常 / ターゲット / 集中 の3モード
  UI/
    PlayerStatusHUD.cs               HP・スタミナ・照準・ロックオンマーカー
    BattleResultUI.cs                QUEST FAILED / CLEAR の表示
  Flow/BattleFlowController.cs       失敗・クリア → Home へ戻る
  Debug/
    TrainingDummy.cs                 打たれ役
    DummyAttacker.cs                 カカシの攻撃 AI（射撃 / 近接）
    DummyProjectile.cs               まっすぐ飛ぶ弾
Editor/
  PlayerSetup/
    PlayerSetupCommon.cs   共通の Animator / Prefab / シーン配置
    PlayerASetupTool.cs    片手剣だけの設定
    PlayerBSetupTool.cs    弓の雛形
  UI/BattleHUDBuilder.cs   HUD プレハブの生成
```
生成物：`Assets/Game/Animations/Player/`、`Assets/Game/Prefabs/Player/`、`Assets/Game/Prefabs/UI/`

### タイプA（片手剣）のガード
- 攻撃ボタンを **0.2秒より長く押す** とガード、短く押して離すと攻撃（`Guard Hold Time`）
- 正面 150° からの攻撃を防ぐ（`Guard Angle`）。ダメージは 15% だけ受ける（削り。`Guard Damage Rate`）、のけぞりなし、少し押し戻される
- 防ぐたびに「ダメージ × `Guard Stamina Per Damage`」のスタミナを使い、足りないとガードが崩れてくらう
- コンボの最後までボタンを押しっぱなしにすると、そのままガードに移る
- ガード中もロックオン相手 / 入力方向へ向きを変えられる。回避でキャンセル可
- モーションは Mixamo の `Assets/MyAssets/Mixamo/Sword And Shield Block Idle.fbx`（ループ）

### ダウン（大攻撃を受けたとき・全タイプ共通）
- `DamageInfo.KnockDown = true` の攻撃を受けると、吹き飛んで倒れる（倒れている間は無敵）
- 1.6秒で起き上がる。0.9秒を過ぎたら回避ボタンで転がって早く起きられる（受け身）
- ガードに成功すればダウンしない
- 倒れるモーションは `KnockDown` ステート（今は死亡モーションで代用）。
  Animator に `GetUp` ステートを追加すると、起き上がりモーションを再生してから立つ

### Mixamo のモーションを使うとき（全タイプ共通）
Kevin Iglesias のモデルには `SpineProxy`（背骨の向きを上書きするスクリプト）が付いているため、
Mixamo のモーションをそのまま再生すると **上半身が直立したまま** になります。
- セットアップツールでは `PlayerSetupCommon.ConfigureMixamoClip(パス, ループするか)` でインポート設定をそろえる
- アクションのコードでは `BeginAction()` の後に **`UseMixamoMotion()`** を呼ぶ（アクション終了で自動的に戻る）
- 回避（ローリング）は基底クラスが自動でやっています
- 防御系のスキルを作るときは、基底クラスの `OnBeforeDamage(ref DamageInfo)` を override すると
  受けるダメージを書き換えられます（ガードもこれで作っています）

### Animator の共通ステート（名前を変えないこと）
| ステート | 内容 |
|---|---|
| Locomotion | 待機 / 歩き / 走り / ダッシュ（Speed） |
| FocusLocomotion | 集中モードの8方向走り（MoveX, MoveY） |
| Dodge | ローリング（Mixamo） |
| Hit / Death | 被弾 / 死亡 |
| Hands レイヤー | 武器を握る手の形 |

タイプ固有のステート（攻撃など）は、この上に追加します。

## 担当別メモ

### 弓（Player タイプ）担当
**作業するのは `Types/Bow/` と `PlayerB_Bow.controller` / `PlayerB_Bow.prefab` だけです。**

課題（`PlayerTypeB_Bow.cs` の先頭に詳しい仕様とヒントがあります）
1. □ で前方に矢を放つ
2. L1 集中モード中の □ で、照準の方向へ矢を放つ（`GetAimDirection(発射位置)`）
3. □ 長押しで溜め、離すとチャージショット（`Input.AttackHeld` / `Input.AttackReleasedThisFrame`）

- 矢のスクリプト（飛んで、当たったら `IDamageable.ApplyDamage`）も `Types/Bow/` に作ってください
- 矢のダメージには `AttackMultiplier`（攻撃力アップのスキル倍率）を掛けてください（剣の `WeaponHitbox` は自動で掛かります）
- 弓のモーション：`Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/`（Load / Hold / Release）
- 弓の弦の動き：モデルの `HumanArcherController` の `LoadBow` / `ShootArrow` / `GetArrow`
- ⚠ `Build PlayerB (Bow) Template` を再実行すると Animator / Prefab が作り直されて、追加した内容が消えます

### 新しいタイプを増やすとき
1. `PlayerBattleController` を継承したクラスを `Types/<タイプ名>/` に作る（`TryStartAction()` と `UpdateAction()` を実装）
2. `Create Common Animator For New Type...` で共通ステート入りの Animator を作り、攻撃ステートを追加
3. Prefab は `PlayerBSetupTool.cs` をまねてセットアップツールを作るのが確実です

### Boss 担当
`BossBase` を継承してボスを作ります（見本：`Battle/Boss/Samples/DragonNightmare/DragonNightmareBoss.cs`）。

| 部品（Battle/Boss/Core） | 役割 |
|---|---|
| `BossBase` | 土台：HP・ひるみ・死亡 → クエストクリア・行動の進め方 |
| `BossHurtbox` | 部位（頭・尻尾など）。ボーンの子に Collider（Trigger）と一緒に付ける。部位ごとのダメージ倍率 |
| `BossAttackHitbox` | 攻撃判定（球）。ボーンの子に付け、`Begin()`〜`End()` の間だけ当たる |
| `BossProjectile` | 飛び道具（ブレス・投石）。まっすぐ / 放物線 |

書くのは2つだけ：
- `ThinkNextAction()` … 距離と確率で、次の行動を選ぶ
- 行動のコルーチン … 「向く → モーション → 軸合わせしながら待つ → 判定 ON/OFF → 休む」

```csharp
IEnumerator Bite()
{
    yield return TurnToPlayer(0.5f);                          // プレイヤーへ向く
    Play("Basic Attack");                                     // モーション再生
    yield return WaitStateTimeTracking("Basic Attack", 0.3f); // 軸合わせしながら 30% まで待つ
    biteHitbox.Begin(20f, knockback: 1f);                     // 判定 ON（小攻撃）
    yield return WaitStateTime("Basic Attack", 0.55f);
    biteHitbox.End();                                         // 判定 OFF
    yield return Rest(1.0f);                                  // 隙
}
```
- **大攻撃（ダウンさせる）**：`Begin(ダメージ, knockback: 5f, knockDown: true)`、範囲なら `AreaAttack(..., knockDown: true)`、
  飛び道具なら `MakeDamage(ダメージ, knockback, knockDown: true)` を `Launch` に渡す
- 攻撃の間隔：`Rest Time Scale`（休み時間の倍率）と `Rest Random Extra`（ランダムに足す秒数）
- ロックオン対象にするには `LockOnTarget` を付ける（`aimPoint` に胸の骨など）
- HP が 0 になると自動でクエストクリア（`BattleFlowController.ClearQuest()`）

#### 見本：ナイトメアドラゴン（ティガレックス風）
| 距離 | 行動（★ = 大攻撃・ダウン） |
|---|---|
| 近い | 噛みつき / ひっかき（小）、ときどきブレス★ / 投擲★ |
| 中距離 | 突進★（ゆるくホーミング → ドリフトで向き直り → 再突進、最大3回）が多め、ブレス★ / 投擲★ |
| 遠い | ブレス★ / 投擲★（岩3つ）/ 突進★ |
| 開始時・HP半分 | 咆哮（のけぞり）。HP半分で怒り（速く・隙が短く） |

### UI 担当
- HP：`HealthChanged (current, max)`、スタミナ：`StaminaChanged (current, max)`、疲労中は `IsExhausted`
- 死亡（クエスト失敗）：`Died`
- ロックオン：`LockTargetChanged`、集中モード：`FocusChanged`
- メニュー表示中は `PlayerInputReader.InputEnabled = false` で操作を止められます
- HUD は `BattleHUD.prefab` を直接編集して OK（Image の色・スプライト・配置など）
- 結果画面の文言・色は `ResultPanel` の `BattleResultUI` で変更できます

## 共通パラメータ（Player のインスペクター）
- **スタミナ**：ダッシュ中は毎秒減り、使っていなければ少し待ってから自然回復。0 で「疲労」になり、30% まで回復するまでダッシュ不可。
  回避でも消費させたいなら `Dodge Stamina Cost`。タイプ固有の行動では `TryUseStamina(量)`
- **回避**：`Dodge Distance` / `Dodge Speed Curve` / 無敵時間。モーションは `Assets/MyAssets/Mixamo/Sprinting Forward Roll.fbx`
  （前後の余計な部分は FBX の Start / End で詰めてから Build を再実行）
- **ターゲット / 集中**：`Lock On Max Distance`、`Focus Move Speed`
- カメラの距離・肩越しの位置・画角は Main Camera の `BattleCameraController` で調整

## ブランチ運用のコツ
- 共通部分（`Core/`, `Common/`, `Camera/`, `InputSystem_Actions.inputactions`）は勝手に変えない。
  必要な機能があれば先生に相談（全員のコンフリクトの元になるため）
- 各自のタイプ / ボスのフォルダの中で作業すれば、マージ時にぶつかりません
