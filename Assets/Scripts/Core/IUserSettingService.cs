namespace BulletHell.Core
{
    public interface IUserSettingService
    {
        bool GetMusicOn();
        void TurnMusic(bool isTurnOn);
    }
}