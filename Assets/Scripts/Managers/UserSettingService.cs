namespace BulletHell.Core
{
    public class UserSettingService : IUserSettingService
    {
        private bool musicOn = true;

        public bool GetMusicOn() => musicOn;

        public void TurnMusic(bool isTurnOn)
        {
            musicOn = isTurnOn;
        }
    }
}