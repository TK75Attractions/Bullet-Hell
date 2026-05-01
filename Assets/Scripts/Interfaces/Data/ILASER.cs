using Unity.Collections;
using Unity.Mathematics;

public interface ILASER
{

    float speed { get; } //eۂ̑x
    float acccel { get; }//eۂ̉̃^Cv

    float2 startPos { get; }//̌vZn߂ x Wǐ_j
    float lastTan { get; }
    float initialCalculateX { get; }


    float2 xyScale { get; }
    float timeCarry { get; }

    NativeList<float2> vertsSet { get; }

    void AwakeSetting(float2 _pos, float2 _vlc, float _t, float _s, float _acc, float2 _polar, float _start, float[] _poly, float _len, float _w, float2 _xy, int cellCount);


    bool UpdateSet(float deltaT);


    NativeArray<LASERCell> GetQuadVerts(int index);

    void Destroy();
}