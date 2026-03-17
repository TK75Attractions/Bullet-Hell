using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimationDataBase", menuName = "Animation/AnimationDataBase")]
public class AnimationDataBase : ScriptableObject
{
    public class PlayerAnimationData
    {
        public Sprite[] up;
        public Sprite[] right;
    }

    public PlayerAnimationData playerAnimationData;

    public AnimationClip[] enemyClips;



}
