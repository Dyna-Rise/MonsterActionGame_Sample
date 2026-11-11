using System;
using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// Player タイプA：片手剣（剣と盾）。全員に配る基本タイプです。
    ///
    /// ・攻撃ボタンを「押してすぐ離す」→ 最大3段コンボ（縦斬り → 横斬り → 盾攻撃）。最後まで行くと1段目に戻る
    ///   攻撃中に次の攻撃ボタンを押しておくと（先行入力）、comboNext のタイミングで次段へ
    ///   dodgeCancel 以降は回避でキャンセル、moveCancel 以降は移動入力でキャンセル
    /// ・攻撃ボタンを「長押し」→ 盾でガード。離すと解除
    ///   正面（guardAngle の範囲）からの攻撃を防ぐ。防ぐたびにスタミナを使い、足りないとガードが崩れる
    ///   コンボの最後までボタンを押しっぱなしにしていると、そのままガードに移る
    ///
    /// 新しいタイプを作るときは、このファイルをコピーして中身を書き換えるのが近道です。
    /// </summary>
    public class PlayerTypeA_SwordShield : PlayerBattleController
    {
        /// <summary>アクション中に何をしているか</summary>
        private enum Mode
        {
            Pending, // ボタンを押した直後：短押し（攻撃）か長押し（ガード）か判定中
            Combo,   // 攻撃
            Guard,   // ガード
        }

        [Header("片手剣")]
        [SerializeField] private WeaponHitbox weapon;
        [SerializeField] private List<AttackStep> combo = new List<AttackStep>();
        [Tooltip("最終段の後に攻撃ボタンで1段目に戻るか")]
        [SerializeField] private bool loopCombo = true;
        [Tooltip("攻撃開始時、入力方向へ向き直れる時間（割合）")]
        [SerializeField, Range(0f, 1f)] private float turnWindow = 0.15f;

        [Header("ガード（攻撃ボタン長押し）")]
        [Tooltip("この秒数より長く押し続けたらガード（短ければ攻撃）")]
        [SerializeField] private float guardHoldTime = 0.2f;
        [Tooltip("ガードできる正面の角度（度）。180 なら前半分")]
        [SerializeField, Range(0f, 360f)] private float guardAngle = 150f;
        [Tooltip("ガード時に受けるダメージの割合（削りダメージ）。0 = ノーダメージ、0.15 = 15% だけ受ける")]
        [SerializeField, Range(0f, 1f)] private float guardDamageRate = 0.15f;
        [Tooltip("防いだダメージ1につき使うスタミナ")]
        [SerializeField] private float guardStaminaPerDamage = 1f;
        [Tooltip("防いだときに後ろへ押される距離（m）")]
        [SerializeField] private float guardPushDistance = 0.4f;

        private const string AnimStateGuard = "Guard";

        private Mode mode;
        private int comboIndex;
        private bool nextQueued;

        /// <summary>今何段目か（0始まり、攻撃していなければ -1）</summary>
        public int ComboIndex => CurrentState == State.Action && mode == Mode.Combo ? comboIndex : -1;

        /// <summary>ガード中か</summary>
        public bool IsGuarding => CurrentState == State.Action && mode == Mode.Guard;

        /// <summary>スタミナ切れでガードが崩れた</summary>
        public event Action<DamageInfo> GuardBroken;

        protected override void Awake()
        {
            base.Awake();
            if (weapon == null) weapon = GetComponentInChildren<WeaponHitbox>();
            if (weapon != null) weapon.SetOwner(transform);
        }

        // ================================================================
        //  開始
        // ================================================================

        protected override bool TryStartAction()
        {
            if (!Input.ConsumeAttack()) return false;

            // まだ攻撃かガードか分からないので「判定中」で待つ（その場で止まる）
            mode = Mode.Pending;
            BeginAction();
            Anim.SetFloat(AnimParamSpeed, 0f);
            Anim.SetFloat(AnimParamMoveX, 0f);
            Anim.SetFloat(AnimParamMoveY, 0f);
            return true;
        }

        protected override void UpdateAction(float dt)
        {
            switch (mode)
            {
                case Mode.Pending: UpdatePending(); break;
                case Mode.Combo: UpdateCombo(); break;
                case Mode.Guard: UpdateGuard(); break;
            }
        }

        private void UpdatePending()
        {
            if (TryCancelIntoDodge()) return;

            if (!Input.AttackHeld)
            {
                // すぐ離した → 攻撃
                if (combo.Count > 0) StartStep(0);
                else ReturnToLocomotion();
                return;
            }
            if (StateTime >= guardHoldTime) StartGuard(); // 押し続けている → ガード
        }

        // ================================================================
        //  コンボ
        // ================================================================

        private void StartStep(int index)
        {
            mode = Mode.Combo;
            comboIndex = index;
            nextQueued = false;
            AttackStep step = combo[index];

            BeginAction();
            FaceDirection(GetAttackFacingDirection(), instant: true); // 入力方向（集中中は照準、ロックオン中はターゲット）へ向き直ってから斬る

            Anim.SetFloat(AnimParamActionSpeed, step.animSpeed);
            CrossFade(step.animState, 0.08f);

            if (weapon != null)
            {
                weapon.BeginSwing(step.damage, step.causesFlinch);
                weapon.SetActive(false);
            }
        }

        private void UpdateCombo()
        {
            AttackStep step = combo[comboIndex];
            float t = StateTime / step.Duration;

            // 出だしは少しだけ向きを変えられる
            if (t < turnWindow) FaceDirection(GetAttackFacingDirection());

            // 踏み込み
            if (t >= step.lungeStart && t < step.lungeEnd)
            {
                float lungeTime = Mathf.Max(0.01f, (step.lungeEnd - step.lungeStart) * step.Duration);
                SetHorizontalVelocity(transform.forward * (step.lungeDistance / lungeTime));
            }

            // 攻撃判定
            if (weapon != null) weapon.SetActive(t >= step.hitStart && t <= step.hitEnd);

            // 次段の先行入力
            if (t >= step.comboInputStart && !nextQueued && Input.ConsumeAttack()) nextQueued = true;

            // 次段へ
            if (nextQueued && t >= step.comboNext)
            {
                int next = comboIndex + 1;
                if (next < combo.Count) { StartStep(next); return; }
                if (loopCombo) { StartStep(0); return; }
            }

            // 回避キャンセル
            if (t >= step.dodgeCancel && TryCancelIntoDodge()) return;

            // 移動キャンセル（後隙）
            if (t >= step.moveCancel && !nextQueued && GetMoveDirection().sqrMagnitude > 0.01f)
            {
                ReturnToLocomotion(0.2f);
                return;
            }

            if (t >= 1f)
            {
                // ボタンを押しっぱなしならそのままガード
                if (Input.AttackHeld && !nextQueued) StartGuard();
                else ReturnToLocomotion(0.2f);
            }
        }

        // ================================================================
        //  ガード
        // ================================================================

        private void StartGuard()
        {
            mode = Mode.Guard;
            if (weapon != null) weapon.SetActive(false);
            BeginAction();
            UseMixamoMotion(); // Guard は Mixamo のモーションなので SpineProxy を止める
            FaceDirection(GetAttackFacingDirection(), instant: true);
            CrossFade(AnimStateGuard, 0.12f);
        }

        private void UpdateGuard()
        {
            if (TryCancelIntoDodge()) return;

            if (!Input.AttackHeld)
            {
                ReturnToLocomotion(0.15f);
                return;
            }

            // その場で構えたまま、入力方向（ロックオン中はターゲット）へ向きを変えられる
            FaceDirection(GetAttackFacingDirection());
        }

        protected override bool OnBeforeDamage(ref DamageInfo info)
        {
            if (!IsGuarding) return true;

            // 攻撃してきた方向が正面の範囲内か
            Vector3 toAttacker = -info.Direction;
            toAttacker.y = 0f;
            bool fromFront = toAttacker.sqrMagnitude < 0.0001f ||
                             Vector3.Angle(transform.forward, toAttacker) <= guardAngle * 0.5f;
            if (!fromFront) return true; // 背中や横から → 普通にくらう

            float cost = info.Amount * guardStaminaPerDamage;
            if (TryUseStamina(cost))
            {
                // ガード成功：ダメージは一部だけ（削り）、のけぞりなし、少し押し戻される
                info.Amount *= guardDamageRate;
                info.CausesFlinch = false;
                info.KnockDown = false; // 大攻撃でもガードできればダウンしない
                AddPush(info.Direction, guardPushDistance + info.KnockbackDistance * 0.3f);
                NotifyGuarded(info); // エフェクトなどへ「防いだ」と知らせる
            }
            else
            {
                // スタミナ不足：ガードが崩れて、そのままくらう
                TryUseStamina(CurrentStamina);
                info.CausesFlinch = true;
                GuardBroken?.Invoke(info);
            }
            return true;
        }

        protected override void OnActionInterrupted()
        {
            if (weapon != null) weapon.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用</summary>
        public void EditorSetup(WeaponHitbox hitbox, List<AttackStep> steps)
        {
            weapon = hitbox;
            combo = steps;
        }
#endif
    }
}
