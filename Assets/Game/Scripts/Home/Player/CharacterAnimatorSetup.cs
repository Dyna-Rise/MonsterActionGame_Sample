using UnityEngine;

namespace MonsterAction.Home.Player
{
    /// <summary>
    /// 子のモデルの Animator に、指定した Animator Controller を起動時にセットします。
    /// （モデルの Prefab 側の設定に頼らず、確実にアニメーションさせるため。村人などに付ける）
    /// </summary>
    public class CharacterAnimatorSetup : MonoBehaviour
    {
        [SerializeField] private RuntimeAnimatorController controller;
        [Tooltip("同じ村人が並んだときに動きがそろわないよう、開始位置をずらす")]
        [SerializeField] private bool randomStartTime = true;

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || controller == null) return;
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != controller) animator.runtimeAnimatorController = controller;
            if (randomStartTime) animator.Play(0, 0, Random.value);
        }
    }
}
