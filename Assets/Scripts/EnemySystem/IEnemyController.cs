namespace BulletHell.Enemies
{
    public interface IEnemyService
    {
        void AddEnemy(IEnemySpawner spawner);
        void UpdateEnemy(float dt);
    }
}