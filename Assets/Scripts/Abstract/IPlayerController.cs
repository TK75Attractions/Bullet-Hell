using Unity.Mathematics;
using UnityEngine;
namespace BulletHell.Player
{
    public interface IPlayerController
    {
        float2 pos { get; }

        void Init(GameObject playerObj);
        void Start();
        void UpShot();
        void DownShot();
    }
}