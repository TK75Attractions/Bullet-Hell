namespace BulletHell.Core.Services
{
    public interface IInputService : IUpdatable
    {
        public bool isDebugMode { get; }
        public bool buttonPressed { get; }
        public bool buttonPressedThisFrame { get; }
        public bool upPressed { get; }
        public bool downPressed { get; }
        public bool leftPressed { get; }
        public bool rightPressed { get; }
        public bool upPressedThisFrame { get; }
        public bool downPressedThisFrame { get; }
        public bool leftPressedThisFrame { get; }
        public bool rightPressedThisFrame { get; }

        public void Init();
    }
}