using System.Collections;
using MonsterAction.Battle.Boss;
using MonsterAction.Battle.CameraSystem;
using MonsterAction.Battle.Player;
using MonsterAction.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Battle.Effects
{
    /// <summary>
    /// 攻撃が当たったことを分かりやすくする簡単なエフェクト（シーンに1つ置く）。
    ///
    ///   Player → Boss  ：当たった位置に火花（弱点は橙で大きめ）＋ダメージの数字＋一瞬のヒットストップ
    ///   Boss → Player  ：プレイヤーに赤い火花＋画面の縁が赤く光る＋カメラが揺れる
    ///   ガード成功      ：水色の火花＋小さな揺れ
    ///
    /// プレイヤー / ボスのイベント（Damaged / Guarded）を聞いているだけなので、
    /// 攻撃する側のスクリプトは何もしなくてよい。
    /// 見た目はプリミティブ（立方体・球）と UI の Text で作っているので、後でパーティクル等に差し替え可能。
    /// </summary>
    public class HitEffects : MonoBehaviour
    {
        [Header("Player → Boss")]
        [SerializeField] private Color hitColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] private Color weakHitColor = new Color(1f, 0.5f, 0.1f);
        [SerializeField] private bool showDamageNumbers = true;
        [Tooltip("ヒットストップの長さ（秒）。0 で無し")]
        [SerializeField] private float hitStop = 0.04f;

        [Header("Boss → Player")]
        [SerializeField] private Color playerHitColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] private float damageFlashTime = 0.35f;
        [SerializeField] private float playerHitShake = 0.15f;
        [SerializeField] private float knockDownShake = 0.35f;

        [Header("ガード")]
        [SerializeField] private Color guardColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField] private float guardShake = 0.06f;

        private PlayerBattleController player;
        private BossBase boss;
        private BattleCameraController cameraController;
        private Canvas canvas;
        private Image flash;
        private Font font;
        private int guardedFrame = -1;
        private bool inHitStop;

        // ================================================================
        //  プレイヤー / ボスのイベントを購読（後から出てきても対応）
        // ================================================================

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateOverlay();
        }

        private void Update()
        {
            if (player != PlayerBattleController.Active)
            {
                if (player != null) { player.Damaged -= OnPlayerDamaged; player.Guarded -= OnPlayerGuarded; }
                player = PlayerBattleController.Active;
                if (player != null) { player.Damaged += OnPlayerDamaged; player.Guarded += OnPlayerGuarded; }
            }
            if (boss != BossBase.Active)
            {
                if (boss != null) boss.Damaged -= OnBossDamaged;
                boss = BossBase.Active;
                if (boss != null) boss.Damaged += OnBossDamaged;
            }
            if (cameraController == null && Camera.main != null)
                cameraController = Camera.main.GetComponent<BattleCameraController>();

            // 赤いフラッシュを消していく
            if (flash != null && flash.color.a > 0f)
            {
                var c = flash.color;
                c.a = Mathf.Max(0f, c.a - damageFlashColor.a / damageFlashTime * Time.unscaledDeltaTime);
                flash.color = c;
            }
        }

        private void OnDestroy()
        {
            if (player != null) { player.Damaged -= OnPlayerDamaged; player.Guarded -= OnPlayerGuarded; }
            if (boss != null) boss.Damaged -= OnBossDamaged;
            if (inHitStop) Time.timeScale = 1f;
        }

        // ================================================================
        //  反応
        // ================================================================

        private void OnBossDamaged(DamageInfo info, string part, float damage)
        {
            bool weak = damage > info.Amount + 0.01f; // 倍率が 1 より大きい部位 = 弱点
            SpawnSparks(info.HitPoint, weak ? weakHitColor : hitColor, weak ? 12 : 8, weak ? 1.3f : 1f);
            if (showDamageNumbers) SpawnNumber(info.HitPoint, Mathf.RoundToInt(damage), weak ? weakHitColor : Color.white, weak ? 40 : 32);
            if (hitStop > 0f && !inHitStop) StartCoroutine(HitStop(hitStop));
        }

        private void OnPlayerGuarded(DamageInfo info)
        {
            guardedFrame = Time.frameCount;
            Vector3 pos = player.transform.position + Vector3.up * 1.1f + player.transform.forward * 0.5f;
            SpawnSparks(pos, guardColor, 10, 1.2f);
            Shake(guardShake, 0.15f);
        }

        private void OnPlayerDamaged(DamageInfo info)
        {
            if (guardedFrame == Time.frameCount) return; // ガードした（演出はガード側で出した）
            if (info.Amount <= 0f) return;               // 咆哮など、ダメージ無し

            SpawnSparks(player.transform.position + Vector3.up * 1.1f, playerHitColor, 10, 1.2f);
            if (flash != null) flash.color = damageFlashColor;
            Shake(info.KnockDown ? knockDownShake : playerHitShake, info.KnockDown ? 0.4f : 0.25f);
        }

        private void Shake(float strength, float duration)
        {
            if (cameraController != null) cameraController.Shake(strength, duration);
        }

        private IEnumerator HitStop(float seconds)
        {
            inHitStop = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            inHitStop = false;
        }

        // ================================================================
        //  火花（小さな立方体が飛び散って縮む ＋ 中心が一瞬光る）
        // ================================================================

        private static void SpawnSparks(Vector3 position, Color color, int count, float scale)
        {
            var root = new GameObject("HitSparks");
            root.transform.position = position;
            root.AddComponent<SparkBurst>().Play(color, count, scale);
        }

        /// <summary>火花1回分。自分で動いて、終わったら消える</summary>
        private class SparkBurst : MonoBehaviour
        {
            private const float LifeTime = 0.3f;
            private Transform core;
            private Transform[] pieces;
            private Vector3[] velocities;
            private float coreSize, pieceSize;
            private float age;

            public void Play(Color color, int count, float scale)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);

                coreSize = 0.6f * scale;
                core = CreatePiece(PrimitiveType.Sphere, block);

                pieceSize = 0.12f * scale;
                pieces = new Transform[count];
                velocities = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    pieces[i] = CreatePiece(PrimitiveType.Cube, block);
                    pieces[i].rotation = Random.rotation;
                    velocities[i] = Random.onUnitSphere * Random.Range(4f, 9f) * scale;
                }
                Update();
            }

            private Transform CreatePiece(PrimitiveType type, MaterialPropertyBlock block)
            {
                var go = GameObject.CreatePrimitive(type);
                Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.SetPropertyBlock(block);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.SetParent(transform, false);
                return go.transform;
            }

            private void Update()
            {
                age += Time.unscaledDeltaTime;
                float t = age / LifeTime;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                // 中心：一瞬ふくらんで消える
                float coreT = Mathf.Clamp01(t * 2.5f);
                core.localScale = Vector3.one * coreSize * Mathf.Sin(coreT * Mathf.PI);

                // かけら：飛び散りながら小さくなる
                for (int i = 0; i < pieces.Length; i++)
                {
                    pieces[i].localPosition += velocities[i] * Time.unscaledDeltaTime;
                    pieces[i].localScale = Vector3.one * pieceSize * (1f - t);
                }
            }
        }

        // ================================================================
        //  ダメージの数字
        // ================================================================

        private void SpawnNumber(Vector3 worldPos, int value, Color color, int fontSize)
        {
            if (canvas == null) return;
            var go = new GameObject("DamageNumber", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = color;
            text.text = value.ToString();
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            go.AddComponent<DamageNumber>().Play(worldPos + Random.insideUnitSphere * 0.3f, (RectTransform)canvas.transform);
        }

        /// <summary>ダメージの数字1つ。当たった場所に表示して、上に浮かびながら消える</summary>
        private class DamageNumber : MonoBehaviour
        {
            private const float LifeTime = 0.8f;
            private Vector3 worldPos;
            private RectTransform canvasRect;
            private Text text;
            private float age;

            public void Play(Vector3 position, RectTransform canvas)
            {
                worldPos = position;
                canvasRect = canvas;
                text = GetComponent<Text>();
                Update();
            }

            private void Update()
            {
                age += Time.unscaledDeltaTime;
                float t = age / LifeTime;
                var cam = Camera.main;
                if (t >= 1f || cam == null)
                {
                    Destroy(gameObject);
                    return;
                }

                Vector3 screen = cam.WorldToScreenPoint(worldPos);
                text.enabled = screen.z > 0f;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    ((RectTransform)transform).localPosition = local + Vector2.up * (60f * t); // 上へ浮かぶ

                var c = text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; // 後半でフェードアウト
                text.color = c;
            }
        }

        // ================================================================
        //  エフェクト用の Canvas（数字と赤いフラッシュ）
        // ================================================================

        private void CreateOverlay()
        {
            var canvasGo = new GameObject("HitEffectsCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // HUD（10）より奥
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 画面の縁が赤く光る（中心が透明なビネット画像を全画面に）
            var flashGo = new GameObject("DamageFlash", typeof(RectTransform));
            flashGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)flashGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            flash = flashGo.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.sprite = CreateVignetteSprite();
            flash.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        }

        /// <summary>中心が透明で、縁にいくほど濃くなるテクスチャ（ビネット）を作る</summary>
        private static Sprite CreateVignetteSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) - 0.55f) / 0.45f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
