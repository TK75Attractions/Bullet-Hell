namespace BulletHell.Audio
{
    public interface ISoundEffectDB<T>
    {
        public void Init();
        public int GetSEData(string seName);
        public T GetSEData(int index);
    }
}