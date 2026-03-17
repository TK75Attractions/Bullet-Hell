using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LASER : MonoBehaviour
{
    private float2 originPos;
    private float2 vlc = new float2();
    private float radius = 1;
    private float thetaVlc = 0;
    private float theta = 0;

    public float speed; //�e�ۂ̑��x
    public float acccel;//�e�ۂ̉����̃^�C�v

    public float2 startPos;//�������̌v�Z���n�߂� x ���W�i�������̌��_�j
    public float lastTan;
    public float initialCalculateX;

    private float[] poly = new float[0];
    private float[] differed = new float[0];

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
    private Mesh mesh = null;

    private List<List<int>> quadVerts = new List<List<int>>();

    public void AwakeSetting(float2 _pos, float2 _vlc, float _t, float _s, float _acc, float2 _polar, float _start, float[] _poly, float _len, float _w, float2 _xy, int cellCount)
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        originPos = _pos;
        vlc = _vlc;
        thetaVlc = _t;
        speed = _s;
        acccel = _acc;
        radius = _polar.x;
        theta = _polar.y;
        poly = _poly;
        initialCalculateX = _start;
        xyScale = _xy;
        timeCarry = 0;
        length = _len;
        width = _w;

        float x = _start;
        float y = GetValue(poly, x);
        startPos = new float2(x, y);

        float[] temp = new float[Mathf.Max(poly.Length - 1, 0)];
        for (int i = 1; i < poly.Length; i++) temp[i - 1] = i * poly[i];
        differed = temp;

        float tan = GetValue(differed, x);
        float magnitude = math.sqrt(1 + tan * tan);
        lastTan = tan / magnitude * speed;

        maxCount = speed > 0f ? System.Convert.ToInt32(length / (speed * dt)) : 0;
        verts = new LASERvertex[0];
        tris = new int[0];
        nowCount = 0;

        if (vertsSet.IsCreated) vertsSet.Dispose();
        vertsSet = new NativeList<float2>(math.max(maxCount * 2, 1), Allocator.Persistent);

        quadVerts.Clear();
        for (int i = 0; i < cellCount; i++) quadVerts.Add(new List<int>());
    }

    private void OnDestroy()
    {
        if (vertsSet.IsCreated) vertsSet.Dispose();
    }

    public bool UpdateSet(float deltaT, float2 pPos, out bool hit)
    {
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
        hit = false;

        GManager.Control.QOrder.UpdateLASERVerts(vertsSet, ref quadVerts);
        return (life < 0);
    }

    private float GetValue(float[] p, float x)
    {
        float y = 0f;
        for (int i = p.Length - 1; i >= 0; i--) y = y * x + p[i];
        return y;
    }

    private void Procede(int proc)
    {
        if (proc == 0) return;
        nowCount += proc;
        //Debug.Log(proc);
        if (nowCount > maxCount) nowCount = maxCount;
        if (nowCount < 0) return;

        LASERvertex[] add = new LASERvertex[nowCount];
        for (int i = 0; i < nowCount - proc; i++) add[proc + i] = verts[i];

        for (int i = 0; i < proc; i++)
        {
            originPos += vlc * dt;
            theta += thetaVlc * dt;

            float mag = math.sqrt(1 + lastTan * lastTan);
            float x = initialCalculateX + speed * dt / mag;
            initialCalculateX = x;
            lastTan = GetValue(differed, x);
            float2 point = new float2(x, GetValue(poly, x));
            float2 dis = point - startPos;
            float2 laserP = new float2(originPos.x + (dis.x * math.cos(theta) - dis.y * math.sin(theta)), originPos.y + (dis.x * math.sin(theta) + dis.y * math.cos(theta)));

            if (proc + 1 - i < add.Length) add[proc - i].nutral = laserP - add[proc + 1 - i].point;
            add[proc - 1 - i] = new LASERvertex(laserP, 0);
        }

        verts = add;
    }

    private void GetVerts()
    {
        if (nowCount < 2)
        {
            vs = new Vector3[0];
            if (vertsSet.IsCreated) vertsSet.Clear();
            if (tris.Length != 0) System.Array.Resize(ref tris, 0);
            mesh.Clear();
            return;
        }

        Vector3[] vts = new Vector3[nowCount * 2];
        if (!vertsSet.IsCreated) vertsSet = new NativeList<float2>(math.max(vts.Length, 1), Allocator.Persistent);
        vertsSet.Clear();

        for (int i = 0; i < nowCount; i++)
        {
            float scale = 1;
            int n = 2 * i;

            if (i < 6) scale *= (i + 1) / 6f;
            else if (i > nowCount - 7) scale *= (nowCount - i) / 6f;

            float2 dis = verts[i].point;// - startPos;
            float2 widthVec = width * scale * new float2(-verts[i].nutral.y, verts[i].nutral.x) / verts[i].magnitude;

            vts[n + 1] = new Vector3(dis.x + widthVec.x, dis.y + widthVec.y, 0);
            vts[n] = new Vector3(dis.x - widthVec.x, dis.y - widthVec.y, 0);

            vertsSet.Add(verts[i].point - widthVec);
            vertsSet.Add(verts[i].point + widthVec);
        }
        vs = vts;

        int targetTriLength = (nowCount - 1) * 6;
        if (tris.Length != targetTriLength)
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
        mesh.vertices = vs;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public NativeArray<LASERCell> GetQuadVerts(int index)
    {
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
}