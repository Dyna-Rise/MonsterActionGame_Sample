using System.Collections;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// ボスのサンプル：ナイトメアドラゴン（アルビノ）。ティガレックス風の行動パターン。
    ///
    /// 【行動パターン】（★ = 大攻撃：当たるとプレイヤーがダウン）
    ///   戦闘開始 / HP 半分で怒り   → 咆哮（近くにいるとのけぞる）
    ///   近い（meleeRange 以内）    → 噛みつき / ひっかき（小攻撃）。ときどきブレス★ / 投擲★
    ///   中くらい（farRange 以内）  → 突進★（ゆるくホーミング → ドリフトで向き直り → また突進）が多め。ブレス★ / 投擲★
    ///   遠い                       → ブレス★ / 投擲★ / 突進★
    ///   どの攻撃も、振りかぶりの間はプレイヤーへ軸合わせ（向き直し）してくる
    ///   攻撃の後は少し休む（プレイヤーの攻撃チャンス）
    ///
    /// 【ボス担当の人へ】
    /// 自分のボスを作るときは、このファイルをまねして
    ///   ・ThinkNextAction() … どの行動をするか決める（距離と確率）
    ///   ・行動ごとのコルーチン … 「向く → モーション → 軸合わせしながら待つ → 判定 ON/OFF → 休む」
    /// を書けば OK です。
    /// </summary>
    public class DragonNightmareBoss : BossBase
    {
        [Header("距離")]
        [SerializeField] private float meleeRange = 7f;
        [SerializeField] private float farRange = 18f;

        [Header("移動")]
        [SerializeField] private float walkSpeed = 3f;

        [Header("攻撃判定（ボーンの子に置いた BossAttackHitbox）")]
        [SerializeField] private BossAttackHitbox biteHitbox;     // 口（ブレスの発射位置にも使う）
        [SerializeField] private BossAttackHitbox[] clawHitboxes; // 両翼の爪
        [SerializeField] private BossAttackHitbox bodyHitbox;     // 胴（突進用）

        [Header("小攻撃")]
        [SerializeField] private float biteDamage = 20f;
        [SerializeField] private float clawDamage = 15f;

        [Header("突進★")]
        [SerializeField] private float chargeDamage = 40f;
        [SerializeField] private float chargeSpeed = 13f;
        [Tooltip("突進中のホーミングの強さ（旋回速度の倍率）。小さいほど避けやすい")]
        [SerializeField] private float chargeHoming = 0.35f;
        [Tooltip("1回の突進の最大時間（秒）")]
        [SerializeField] private float chargeMaxTime = 2.5f;
        [Tooltip("突進の回数（ドリフトをはさんで最大何回走るか）")]
        [SerializeField] private int maxCharges = 3;
        [Tooltip("ドリフト（滑りながら向き直る）の時間")]
        [SerializeField] private float driftTime = 0.7f;
        [Tooltip("ドリフト中の旋回速度の倍率")]
        [SerializeField] private float driftTurn = 4f;

        [Header("ブレス★（正面へ扇状に、地面すれすれをまっすぐ飛ぶ）")]
        [SerializeField] private BossProjectile breathPrefab; // 空なら球を自動で作る
        [SerializeField] private float breathDamage = 35f;
        [SerializeField] private float breathSpeed = 20f;
        [SerializeField] private float breathRadius = 1.0f;
        [Tooltip("一度に出す火球の数")]
        [SerializeField] private int breathCount = 3;
        [Tooltip("火球どうしの角度（度）")]
        [SerializeField] private float breathSpreadAngle = 12f;
        [Tooltip("飛ぶ高さ（m）。プレイヤーの胸あたり")]
        [SerializeField] private float breathHeight = 1.2f;
        [Tooltip("ボスの中心からどれだけ前で出すか（m）")]
        [SerializeField] private float breathStartForward = 4f;
        [SerializeField] private Color breathColor = new Color(1f, 0.45f, 0.1f);

        [Header("投擲★（正面へ扇状に、低い弾道で決まった距離まで飛ぶ）")]
        [SerializeField] private BossProjectile rockPrefab; // 空なら球を自動で作る
        [SerializeField] private float rockDamage = 30f;
        [SerializeField] private float rockRadius = 0.8f;
        [Tooltip("一度に投げる岩の数")]
        [SerializeField] private int rockCount = 5;
        [Tooltip("岩どうしの角度（度）")]
        [SerializeField] private float rockSpreadAngle = 12f;
        [Tooltip("岩が地面に落ちるまでの距離（m）")]
        [SerializeField] private float rockDistance = 16f;
        [Tooltip("岩が落ちるまでの時間（秒）")]
        [SerializeField] private float rockFlightTime = 1.0f;
        [Tooltip("岩の重力。小さいほど低く平らに飛ぶ（プレイヤーの高さを通り続ける）")]
        [SerializeField] private float rockGravity = -6f;
        [SerializeField] private float rockStartHeight = 1.5f;
        [SerializeField] private float rockStartForward = 3f;
        [SerializeField] private Color rockColor = new Color(0.45f, 0.4f, 0.35f);

        [Header("咆哮")]
        [SerializeField] private float roarRadius = 12f;

        [Header("怒り")]
        [Tooltip("怒り状態のモーション速度")]
        [SerializeField] private float enragedAnimSpeed = 1.2f;
        [Tooltip("怒り状態の休み時間の倍率")]
        [SerializeField] private float enragedRestRate = 0.6f;

        // Animator のステート名（DragonNightmare 用の Animator Controller と同じ名前）
        private const string Walk = "Walk";
        private const string Run = "Run";
        private const string BasicAttack = "Basic Attack";
        private const string ClawAttack = "Claw Attack";
        private const string HornAttack = "Horn Attack";
        private const string Scream = "Scream";

        private bool hasRoared;
        private bool enraged;

        private float RestTime(float seconds) => enraged ? seconds * enragedRestRate : seconds;

        // ================================================================
        //  どの行動をするか決める
        // ================================================================

        protected override IEnumerator ThinkNextAction()
        {
            if (!hasRoared)
            {
                hasRoared = true;
                return Roar();
            }
            if (!enraged && HpRate <= 0.5f)
            {
                enraged = true;
                Anim.speed = enragedAnimSpeed;
                return Roar();
            }

            float distance = DistanceToPlayer;
            int r = Random.Range(0, 100); // 0〜99 のサイコロ。数字の幅が出やすさ

            if (distance <= meleeRange)
            {
                // 近距離：小攻撃メイン、ときどき遠距離攻撃
                if (r < 40) return Bite();
                if (r < 75) return Scratch();
                if (r < 88) return Breath();
                return Throw();
            }

            if (distance <= farRange)
            {
                // 中距離：突進が多め
                if (r < 55) return ChargeCombo();
                if (r < 75) return Breath();
                if (r < 90) return Throw();
                return Approach(2f);
            }

            // 遠距離：ブレスか投擲、ときどき突進
            if (r < 35) return Breath();
            if (r < 70) return Throw();
            return ChargeCombo();
        }

        // ================================================================
        //  咆哮
        // ================================================================

        private IEnumerator Roar()
        {
            yield return TurnToPlayer(1f, Walk);
            Play(Scream);
            yield return WaitStateTime(Scream, 0.25f);
            AreaAttack(transform.position, roarRadius, 0f, knockback: 1.5f); // ダメージなし、のけぞり
            yield return WaitStateTime(Scream, 0.95f);
            yield return Rest(RestTime(0.5f));
        }

        // ================================================================
        //  小攻撃
        // ================================================================

        /// <summary>噛みつき（小）：軸合わせしながら踏み込んで噛む</summary>
        private IEnumerator Bite()
        {
            yield return TurnToPlayer(0.5f, Walk);
            Play(BasicAttack);
            // 振りかぶり：軸合わせしながら少し踏み込む
            while (GetStateTime(BasicAttack) < 0.3f)
            {
                FaceTowards(DirectionToPlayer, 1.5f);
                Move(transform.forward * 2.5f);
                yield return null;
            }
            biteHitbox.Begin(biteDamage, knockback: 1f);
            yield return WaitStateTime(BasicAttack, 0.55f);
            biteHitbox.End();
            yield return WaitStateTime(BasicAttack, 0.95f);
            yield return Rest(RestTime(1.0f));
        }

        /// <summary>ひっかき（小）：軸合わせしてから翼の爪で軽く払う</summary>
        private IEnumerator Scratch()
        {
            yield return TurnToPlayer(0.5f, Walk);
            Play(ClawAttack);
            yield return WaitStateTimeTracking(ClawAttack, 0.3f, 1.5f);
            foreach (var h in clawHitboxes) h.Begin(clawDamage, knockback: 0.8f);
            yield return WaitStateTime(ClawAttack, 0.55f);
            foreach (var h in clawHitboxes) h.End();
            yield return WaitStateTime(ClawAttack, 0.95f);
            yield return Rest(RestTime(0.9f));
        }

        // ================================================================
        //  遠距離攻撃（大）
        // ================================================================

        /// <summary>
        /// ブレス★：軸合わせしてから、正面へ扇状に火球を撃つ。
        /// 狙いはプレイヤーの位置ではなく「ボスの正面」。地面すれすれをまっすぐ飛ぶので、正面にいればほぼ当たる
        /// </summary>
        private IEnumerator Breath()
        {
            yield return TurnToPlayer(0.8f, Walk);
            Play(Scream);
            yield return WaitStateTimeTracking(Scream, 0.35f, 1.2f); // 軸合わせ（予兆）

            Vector3 start = transform.position + transform.forward * breathStartForward + Vector3.up * breathHeight;
            foreach (Vector3 dir in FanDirections(breathCount, breathSpreadAngle))
            {
                var ball = BossProjectile.Spawn(breathPrefab, start, breathRadius, breathColor);
                ball.Launch(dir * breathSpeed, MakeDamage(breathDamage, knockback: 4f, knockDown: true), transform);
            }

            yield return WaitStateTime(Scream, 0.95f);
            yield return Rest(RestTime(1.4f));
        }

        /// <summary>
        /// 投擲★：軸合わせしてから、正面へ扇状に岩を投げる。
        /// いつも同じ角度・同じ距離（rockDistance）に落ちる。低い弾道なので、途中にいても当たる
        /// </summary>
        private IEnumerator Throw()
        {
            yield return TurnToPlayer(0.8f, Walk);
            Play(HornAttack);
            yield return WaitStateTimeTracking(HornAttack, 0.4f, 1.2f); // 軸合わせ（予兆）

            Vector3 start = transform.position + transform.forward * rockStartForward + Vector3.up * rockStartHeight;
            foreach (Vector3 dir in FanDirections(rockCount, rockSpreadAngle))
            {
                Vector3 landing = transform.position + dir * rockDistance; // 地面に落ちる位置（毎回同じ）
                landing.y = transform.position.y;
                var rock = BossProjectile.Spawn(rockPrefab, start, rockRadius, rockColor);
                rock.Launch(BossProjectile.BallisticVelocity(start, landing, rockFlightTime, rockGravity),
                            MakeDamage(rockDamage, knockback: 3f, knockDown: true), transform, rockGravity);
            }

            yield return WaitStateTime(HornAttack, 0.95f);
            yield return Rest(RestTime(1.4f));
        }

        /// <summary>正面を中心に、count 本の方向を angle 度ずつ左右に広げて返す（水平）</summary>
        private System.Collections.Generic.IEnumerable<Vector3> FanDirections(int count, float angle)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * angle; // 例：3本なら -angle, 0, +angle
                yield return Quaternion.AngleAxis(offset, Vector3.up) * forward;
            }
        }

        // ================================================================
        //  突進（大）
        // ================================================================

        /// <summary>
        /// 突進コンボ★：ゆるくホーミングして走り抜ける → ドリフトで向き直る → また突進 …（最大 maxCharges 回）
        /// </summary>
        private IEnumerator ChargeCombo()
        {
            yield return TurnToPlayer(0.8f, Walk);

            // 予兆：短く吠える
            Play(Scream, 0.1f);
            yield return WaitStateTimeTracking(Scream, 0.25f, 1.5f);

            int charges = Random.Range(1, maxCharges + 1);
            for (int i = 0; i < charges; i++)
            {
                yield return ChargeRun();
                if (i < charges - 1) yield return Drift();
            }

            // 最後はブレーキをかけて止まる
            yield return Brake(0.6f);
            yield return Rest(RestTime(1.6f));
        }

        /// <summary>1回分の突進：ゆるく追いかけながら走り、プレイヤーを通り過ぎたら終わる</summary>
        private IEnumerator ChargeRun()
        {
            Play(Run, 0.1f);
            bodyHitbox.Begin(chargeDamage, knockback: 6f, knockDown: true);

            float time = 0f;
            float passedTime = 0f;
            while (time < chargeMaxTime)
            {
                FaceTowards(DirectionToPlayer, chargeHoming); // ゆるくホーミング
                Move(transform.forward * chargeSpeed);

                // プレイヤーが真後ろ側に回った（通り過ぎた）ら、少し走ってから終わる
                if (Mathf.Abs(AngleToPlayer) > 100f) passedTime += Time.deltaTime;
                if (passedTime > 0.35f) break;

                time += Time.deltaTime;
                yield return null;
            }
            bodyHitbox.End();
        }

        /// <summary>ドリフト：勢いで滑りながら、すばやくプレイヤーの方へ向き直る（この間も当たる）</summary>
        private IEnumerator Drift()
        {
            Vector3 slide = transform.forward * chargeSpeed;
            Play(AngleToPlayer > 0f ? "Walk Right" : "Walk Left", 0.1f);
            bodyHitbox.Begin(chargeDamage * 0.7f, knockback: 4f, knockDown: true);

            float time = 0f;
            while (time < driftTime)
            {
                float rate = 1f - time / driftTime;
                Move(slide * rate);                           // 勢いがだんだん落ちる
                FaceTowards(DirectionToPlayer, driftTurn);    // すばやく向き直る
                time += Time.deltaTime;
                yield return null;
            }
            bodyHitbox.End();
        }

        /// <summary>減速して止まる</summary>
        private IEnumerator Brake(float seconds)
        {
            Play(Walk, 0.2f);
            float time = 0f;
            while (time < seconds)
            {
                Move(transform.forward * chargeSpeed * (1f - time / seconds) * 0.5f);
                time += Time.deltaTime;
                yield return null;
            }
        }

        // ================================================================
        //  移動
        // ================================================================

        /// <summary>歩いて近づく</summary>
        private IEnumerator Approach(float maxSeconds)
        {
            Play(Walk, 0.25f);
            float end = Time.time + maxSeconds;
            while (Time.time < end && DistanceToPlayer > meleeRange * 0.8f)
            {
                FaceTowards(DirectionToPlayer);
                Move(transform.forward * walkSpeed);
                yield return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用</summary>
        public void EditorSetup(BossAttackHitbox bite, BossAttackHitbox[] claws, BossAttackHitbox body)
        {
            biteHitbox = bite;
            clawHitboxes = claws;
            bodyHitbox = body;
        }
#endif
    }
}
