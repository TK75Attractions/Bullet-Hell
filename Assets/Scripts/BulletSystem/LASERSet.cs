using System.Collections.Generic;

namespace BulletHell.Bullets
{
    public class LASERSet
    {
        public ILASER laser;
        public List<List<int>> vertIndixes;

        public LASERSet(ILASER laser, int cellCount)
        {
            this.laser = laser;
            this.vertIndixes = new List<List<int>>(cellCount);
            for (int i = 0; i < cellCount; i++) vertIndixes.Add(new List<int>());
        }
    }
}