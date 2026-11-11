using System.Collections.Generic;
using UnityEngine;

namespace MonsterAction.Home.Interaction
{
    /// <summary>
    /// Interactable のハイライト（光る縁取り + うっすら光る）を作る仕組み。Interactable が内部で使います。
    /// 元のモデルと同じメッシュを、ハイライト用マテリアルでもう一枚重ねて描きます（元のマテリアルは触らない）。
    /// </summary>
    public class InteractHighlight
    {
        private const string DefaultMaterialPath = "InteractHighlight"; // Resources/InteractHighlight.mat
        private const string ShaderName = "MonsterAction/InteractHighlight";
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Component owner;
        private readonly Renderer[] sources;
        private readonly Color color;
        private Material material;
        private readonly List<GameObject> overlays = new List<GameObject>();
        private readonly List<(Renderer renderer, Material[] original, Material[] withHighlight)> staticTargets =
            new List<(Renderer, Material[], Material[])>();
        private bool built;
        private bool visible;

        public InteractHighlight(Component owner, Renderer[] sources, Material material, Color color)
        {
            this.owner = owner;
            this.sources = sources;
            this.material = material;
            this.color = color;
        }

        public void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            if (on && !built) Build();
            foreach (var go in overlays)
                if (go != null) go.SetActive(on);
            foreach (var t in staticTargets)
                if (t.renderer != null) t.renderer.sharedMaterials = on ? t.withHighlight : t.original;
        }

        public void Dispose()
        {
            foreach (var go in overlays)
                if (go != null) Object.Destroy(go);
            overlays.Clear();
            if (material != null && built) Object.Destroy(material);
        }

        private void Build()
        {
            built = true;

            // マテリアル（Interactable ごとに色を変えられるよう複製する）
            var src = material != null ? material : Resources.Load<Material>(DefaultMaterialPath);
            if (src != null) material = new Material(src);
            else
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"[InteractHighlight] シェーダー {ShaderName} が見つかりません。MonsterAction > Home > Setup Home Scene を実行してください。", owner);
                    return;
                }
                material = new Material(shader);
            }
            material.SetColor(ColorId, color);

            foreach (var r in sources)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var go = CreateOverlay(r);
                if (go != null) overlays.Add(go);
            }
        }

        private GameObject CreateOverlay(Renderer src)
        {
            Mesh mesh = null;
            if (src is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            else if (src is MeshRenderer)
            {
                var mf = src.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            if (mesh == null) return null;

            if (src.isPartOfStaticBatch)
            {
                // 静的バッチ済みのメッシュは複製できないので、マテリアルを1枚足す方法にする
                var original = src.sharedMaterials;
                var mats = new List<Material>(original) { material };
                staticTargets.Add((src, original, mats.ToArray()));
                Debug.LogWarning($"[InteractHighlight] {src.name} は Static（Batching Static）のため、ハイライトが一部にしか付かないことがあります。Inspector 右上の Static のチェックを外してください。", src);
                return null;
            }

            var go = new GameObject(src.name + "_Highlight");
            go.transform.SetParent(src.transform, false);
            go.layer = src.gameObject.layer;

            var mats2 = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats2.Length; i++) mats2[i] = material;

            if (src is SkinnedMeshRenderer s)
            {
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = s.sharedMesh;
                r.bones = s.bones;
                r.rootBone = s.rootBone;
                r.localBounds = s.localBounds;
                r.updateWhenOffscreen = s.updateWhenOffscreen;
                r.sharedMaterials = mats2;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats2;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            go.SetActive(false);
            return go;
        }
    }
}
