using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LASER : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float2 originPos;
    private float2 vlc = new float2();
    private float thetaVlc = 0;
    private float theta = 0;

    private float speed; //�e�ۂ̑��x

    public float2 startPos;//�������̌v�Z���n�߂� x ���W�i�������̌��_�j
    public float lastTan;
    public float initialCalculateX;

    [SerializeField] private float[] poly = new float[0];

    public float2 xyScale;
    public float timeCarry;

    private float length = 0;
    private float width = 0;
    private LASERvertex[] verts = new LASERvertex[0];
    private Vector3[] vs = new Vector3[0];
    public NativeList<float2> vertsSet;
    private int[] tris = new int[0];
    private int maxCount = 0;
    private int nowCount = 0;
    private const float dt = 0.03f;
    private float life = 10f;
    private float4 color = new float4(1, 1, 1, 1);
    private bool isClearing = false;
    private float clearElapsed = 0f;
    private float clearDuration = 0.45f;
    private float clearFade01 = 1f;
    private Mesh mesh = null;
    private MeshRenderer meshRenderer = null;
    private MaterialPropertyBlock propertyBlock = null;

    private List<List<int>> quadVerts = new List<List<int>>();

    public void AwakeSetting(float2 _pos, float2 _vlc, float _t, float _s, float2 _polar, float _startX, float2 _startPos, float[] _poly, float _len, float _w, float _life, float4 _color, int cellCount)
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        originPos = _pos;
        vlc = _vlc;
        thetaVlc = _t;
        speed = _s;
        theta = _polar.y;
        poly = _poly;

        string s = "";
        for (int i = 0; i < poly.Length; i++) s += poly[i] + " ";
        Debug.Log("LASER Poly: " + s);

        initialCalculateX = _startX;
        startPos = _startPos;
        timeCarry = 0;
        length = _len;
        width = _w;
        life = _life;
        color = _color;
        ApplyColor();

        float x = _startX;
        lastTan = GetPolynomialSlope(poly, x);

        float f = math.sqrt(speed * speed + (startPos.y * startPos.y + startPos.x * startPos.x) * thetaVlc * thetaVlc);
        maxCount = f > 0f ? System.Convert.ToInt32(length / (f * dt)) : 0;
        verts = new LASERvertex[math.max(maxCount, 1)];
        vs = new Vector3[math.max(maxCount * 2, 1)];
        int maxTriLength = maxCount > 1 ? (maxCount - 1) * 6 : 0;
        tris = new int[maxTriLength];
        int ti = 0;
        for (int i = 0; i < maxCount - 1; i++)
        {
            int bsIndex = i * 2;
            tris[ti++] = bsIndex;
            tris[ti++] = bsIndex + 2;
            tris[ti++] = bsIndex + 1;
            tris[ti++] = bsIndex + 1;
            tris[ti++] = bsIndex + 2;
            tris[ti++] = bsIndex + 3;
        }
        nowCount = 0;

        if (vertsSet.IsCreated) vertsSet.Dispose();
        vertsSet = new NativeList<float2>(math.max(maxCount * 2, 1), Allocator.Persistent);

        quadVerts.Clear();
        for (int i = 0; i < cellCount; i++) quadVerts.Add(new List<int>());
    }

    private void ApplyColor()
    {
        if (meshRenderer == null) return;

        Color c = new Color(color.x, color.y, color.z, color.w);
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, c);
        propertyBlock.SetColor(ColorId, c);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyColor(float alphaMultiplier)
    {
        if (meshRenderer == null) return;

        float alpha = math.saturate(alphaMultiplier);
        Color c = new Color(color.x, color.y, color.z, color.w * alpha);
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, c);
        propertyBlock.SetColor(ColorId, c);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    public bool IsClearing => isClearing;

    public void BeginFadeOut(float duration = 0.45f)
    {
        isClearing = true;
        clearElapsed = 0f;
        clearDuration = duration > 0f ? duration : 0.0001f;
        clearFade01 = 1f;
    }

    private void OnDestroy()
    {
        if (vertsSet.IsCreated) vertsSet.Dispose();
    }

    public bool UpdateSet(float deltaT)
    {
        if (isClearing)
        {
            clearElapsed += deltaT;
            clearFade01 = math.saturate(1f - clearElapsed / clearDuration);
            ApplyColor(clearFade01);
            GetVerts();

            if (clearFade01 <= 0f)
            {
                if (vertsSet.IsCreated) vertsSet.Clear();
                mesh.Clear();
            }

            return clearElapsed >= clearDuration;
        }

        timeCarry += deltaT;
        int proc = 0;
        while (timeCarry > dt)
        {
            proc++;
            timeCarry -= dt;
        }

        Procede(proc);
        GetVerts();
        life -= deltaT;

        GManager.Control.QOrder.UpdateLASERVerts(vertsSet, ref quadVerts);
        return life < 0;
    }

    private float GetPolynomialValue(float[] p, float x)
    {
        float y = 0f;
        float xPower = x;
        for (int i = 0; i < p.Length; i++)
        {
            y += p[i] * xPower;
            xPower *= x;
        }
        return y;
    }

    private float GetPolynomialSlope(float[] p, float x)
    {
        float slope = 0f;
        float xPower = 1f;
        for (int i = 0; i < p.Length; i++)
        {
            int power = i + 1;
            slope += power * p[i] * xPower;
            xPower *= x;
        }
        return slope;
    }

    private void Procede(int proc)
    {
        if (proc == 0) return;
        nowCount += proc;
        if (nowCount > maxCount) nowCount = maxCount;
        if (nowCount < 0) return;

        int insertCount = math.min(proc, nowCount);
        if (insertCount <= 0) return;

        for (int i = nowCount - 1; i >= insertCount; i--) verts[i] = verts[i - insertCount];

        for (int i = 0; i < insertCount; i++)
        {
            originPos += vlc * dt;
            theta += thetaVlc * dt;

            float mag = math.sqrt(1 + lastTan * lastTan);
            float x = initialCalculateX + speed * dt / mag;
            initialCalculateX = x;
            lastTan = GetPolynomialSlope(poly, x);
            float2 point = new float2(x, GetPolynomialValue(poly, x));
            float2 dis = point - startPos;
            float2 laserP = new float2(originPos.x + (dis.x * math.cos(theta) - dis.y * math.sin(theta)), originPos.y + (dis.x * math.sin(theta) + dis.y * math.cos(theta)));

            if (insertCount + 1 - i < nowCount) verts[insertCount - i].nutral = laserP - verts[insertCount + 1 - i].point;
            verts[insertCount - 1 - i] = new LASERvertex(laserP, 0);
        }
    }

    private void GetVerts()
    {
        if (nowCount < 2)
        {
            if (vertsSet.IsCreated) vertsSet.Clear();
            mesh.Clear();
            return;
        }

        int activeVertCount = nowCount * 2;
        if (vs.Length < activeVertCount) System.Array.Resize(ref vs, activeVertCount);
        if (!vertsSet.IsCreated) vertsSet = new NativeList<float2>(math.max(vs.Length, 1), Allocator.Persistent);
        vertsSet.Clear();

        for (int i = 0; i < nowCount; i++)
        {
            float scale = 1;
            int n = 2 * i;

            if (i < 6) scale *= (i + 1) / 6f;
            else if (i > nowCount - 7) scale *= (nowCount - i) / 6f;

            float2 dis = verts[i].point;// - startPos;
            float widthScale = isClearing ? clearFade01 : 1f;
            float2 widthVec = width * widthScale * scale * new float2(-verts[i].nutral.y, verts[i].nutral.x) / verts[i].magnitude;

            vs[n + 1] = new Vector3(dis.x + widthVec.x, dis.y + widthVec.y, 0);
            vs[n] = new Vector3(dis.x - widthVec.x, dis.y - widthVec.y, 0);

            vertsSet.Add(verts[i].point - widthVec);
            vertsSet.Add(verts[i].point + widthVec);
        }

        int targetTriLength = (nowCount - 1) * 6;
        if (tris.Length < targetTriLength)
        {
            int tiIndx = tris.Length;
            int k = tiIndx / 6;
            System.Array.Resize(ref tris, targetTriLength);

            for (int i = k; i < nowCount - 1; i++)
            {
                int bsIndex = i * 2;

                tris[tiIndx++] = bsIndex;
                tris[tiIndx++] = bsIndex + 2;
                tris[tiIndx++] = bsIndex + 1;
                tris[tiIndx++] = bsIndex + 1;
                tris[tiIndx++] = bsIndex + 2;
                tris[tiIndx++] = bsIndex + 3;
            }
        }

        mesh.Clear();
        mesh.SetVertices(vs, 0, activeVertCount);
        mesh.SetIndices(tris, 0, targetTriLength, MeshTopology.Triangles, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));
    }

    public NativeArray<LASERCell> GetQuadVerts(int index)
    {
        if (isClearing) return new NativeArray<LASERCell>(0, Allocator.TempJob);
        if (index < 0 || index >= quadVerts.Count) return new NativeArray<LASERCell>(0, Allocator.TempJob);
        if (!vertsSet.IsCreated || vertsSet.Length < 4 || nowCount < 2) return new NativeArray<LASERCell>(0, Allocator.TempJob);

        List<int> ints = quadVerts[index];
        if (ints.Count == 0) return new NativeArray<LASERCell>(0, Allocator.TempJob);

        int minLine = int.MaxValue;
        int maxLine = int.MinValue;
        for (int i = 0; i < ints.Count; i++)
        {
            int vertIndex = ints[i];
            if (vertIndex < 0 || vertIndex >= vertsSet.Length) continue;

            int line = vertIndex / 2;
            if (line < minLine) minLine = line;
            if (line > maxLine) maxLine = line;
        }

        if (minLine == int.MaxValue) return new NativeArray<LASERCell>(0, Allocator.TempJob);

        int startLine = Mathf.Max(minLine - 1, 0);
        int endLine = Mathf.Min(maxLine + 1, nowCount - 1);
        int cellLineCount = endLine - startLine;
        if (cellLineCount <= 0) return new NativeArray<LASERCell>(0, Allocator.TempJob);

        NativeArray<LASERCell> result = new NativeArray<LASERCell>(cellLineCount * 2, Allocator.TempJob);
        int writeIndex = 0;

        for (int line = startLine; line < endLine; line++)
        {
            int i0 = line * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;
            if (i3 >= vertsSet.Length) break;

            result[writeIndex++] = new LASERCell(vertsSet[i0], vertsSet[i1], vertsSet[i2]);
            result[writeIndex++] = new LASERCell(vertsSet[i1], vertsSet[i3], vertsSet[i2]);
        }

        if (writeIndex == result.Length) return result;

        NativeArray<LASERCell> trimmed = new NativeArray<LASERCell>(writeIndex, Allocator.TempJob);
        if (writeIndex > 0) NativeArray<LASERCell>.Copy(result, trimmed, writeIndex);
        result.Dispose();
        return trimmed;
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }
}
