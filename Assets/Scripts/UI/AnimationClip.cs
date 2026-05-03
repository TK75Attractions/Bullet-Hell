using UnityEngine;

namespace BulletHell.UI
{
    [CreateAssetMenu(fileName = "NewAnimationClip", menuName = "Animation/AnimationClip")]
    public class AnimationClip : ScriptableObject
    {
        public Sprite up;
        public Sprite down;
        public Sprite right;
        public Sprite UpRight;
        public Sprite DownRight;
    }
}