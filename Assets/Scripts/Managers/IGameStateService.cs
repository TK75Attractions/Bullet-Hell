using BulletHell.Core;

namespace BulletHell.App
{
    public interface IGameStateService
    {
        GameState state { get; set; }
    }
}