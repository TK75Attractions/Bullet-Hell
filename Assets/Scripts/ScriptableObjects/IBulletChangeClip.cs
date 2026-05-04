namespace BulletHell.Bullets
{
    public interface IBulletChangeClip
    {
        BulletClip GetClip();

        float GetTime();
        float GetInterval();
    }
}