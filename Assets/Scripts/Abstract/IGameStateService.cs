using BulletHell.Core;

namespace BulletHell.Core
{
    public interface IGameStateService
    {
        GameState state { get; set; }
    }
}