using UnityEngine;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// Player タイプB：弓（★ 弓担当の課題 ★）
    ///
    /// 移動・ダッシュ・回避・被弾・ターゲットカメラ・集中モードは、親クラス PlayerBattleController が
    /// すでにやってくれます。このクラスでは「弓の攻撃」だけを作ってください。
    ///
    /// ──────────────────────── 仕様 ────────────────────────
    ///  ① □（攻撃ボタン）を押す                     → 前方に矢を放つ
    ///  ② L1 集中モード中に □                        → 照準（画面中央）の方向へ矢を放つ
    ///  ③ □ を長押し                                 → 溜める。離したらチャージショット（威力アップ）
    ///  （④ 余裕があれば：溜め段階、スタミナ消費、連射、拡散矢 …など自由に）
    ///
    /// ──────────────────── 使える道具（親クラス） ────────────────────
    ///  入力
    ///    Input.ConsumeAttack()          … □ が押された（先行入力つき）
    ///    Input.AttackHeld               … □ を押し続けている
    ///    Input.AttackReleasedThisFrame  … □ をこのフレームで離した
    ///  状態
    ///    IsFocusing                     … 集中モード中か（L1）
    ///    LockTarget                     … ロックオン中の相手（null なら無し）
    ///    StateTime                      … アクション開始からの経過秒数
    ///  狙い
    ///    GetAimDirection(発射位置)      … 発射位置から照準へ向かう方向
    ///    GetAimPoint()                  … 照準が指している場所
    ///    GetAttackFacingDirection()     … 攻撃時に向くべき方向（集中中は照準、入力があれば入力方向）
    ///    FaceDirection(方向, instant)   … キャラをその方向へ向ける
    ///  流れ
    ///    BeginAction()                  … アクション状態に入る（以降 UpdateAction が毎フレーム呼ばれる）
    ///    ReturnToLocomotion()           … 通常移動へ戻る
    ///    TryCancelIntoDodge()           … 回避が押されていれば回避でキャンセル
    ///    CrossFade("ステート名")        … Animator のステートへ切り替え
    ///    SetHorizontalVelocity(速度)    … 撃ちながら移動したいときなど
    ///    TryUseStamina(量)              … スタミナを使う（足りなければ false）
    ///  ダメージ
    ///    矢が当たった Collider から GetComponentInParent&lt;IDamageable&gt;() → ApplyDamage(new DamageInfo(...))
    ///    （PlayerTypeA の WeaponHitbox.cs が参考になります）
    ///
    /// ──────────────────── 使えるアセット ────────────────────
    ///  モーション：MyAssets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/
    ///    HumanM@BowShot01 - Load（引く）/ - Hold（引いたまま）/ - Release（放つ）
    ///  矢のモデル：.../Human Archer Animations/Prefabs/HumanArcher_ArrowToShoot.prefab
    ///  弓の見た目：モデルに付いている KevinIglesias.HumanArcherController の
    ///    LoadBow(delay, duration) / ShootArrow(delay, duration) / GetArrow(delay) を呼ぶと弦が動きます
    ///
    /// Animator（Assets/Game/Animations/Player/PlayerB_Bow.controller）に自分でステートを追加してください。
    /// Locomotion / FocusLocomotion / Dodge / Hit / Death は共通で使うので名前を変えないこと。
    /// </summary>
    public class PlayerTypeB_Bow : PlayerBattleController
    {
        // [Header("弓")]
        // TODO: 矢の Prefab、発射位置、ダメージ、溜め時間などをここに追加

        protected override bool TryStartAction()
        {
            // TODO: □ が押されたら射撃を始める
            // if (Input.ConsumeAttack())
            // {
            //     BeginAction();
            //     FaceDirection(GetAttackFacingDirection(), instant: true);
            //     CrossFade("BowLoad");
            //     return true;
            // }
            return false;
        }

        protected override void UpdateAction(float deltaTime)
        {
            // TODO: 溜め・発射・後隙を作る
            // ・Input.AttackHeld の間は溜める（StateTime で溜め時間が分かる）
            // ・離したら（または短押しなら）矢を出す
            // ・撃ち終わったら ReturnToLocomotion()
            ReturnToLocomotion();
        }

        protected override void OnActionInterrupted()
        {
            // TODO: 回避や被弾で中断されたときの後始末（溜めのリセットなど）
        }
    }
}
