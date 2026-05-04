using Unity.Mathematics;
using UnityEngine;
namespace BulletHell.Player
{
    public interface IPlayerController
    {
        float2 pos { get; }

        void Init(GameObject playerObj);
        void Start();
        void UpdatePos(float dt);
        void UpShot();
        void DownShot();
    }
}