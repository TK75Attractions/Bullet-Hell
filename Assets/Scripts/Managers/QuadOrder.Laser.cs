using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// S7 QuadOrder split (delegation-only): laser vertex bucketing, laser/player
// collision, and the editor collision gizmos. The laser lists (allLASERs,
// laserVertsIndex) and NativeLists remain owned by the core QuadOrder partial.
public partial class QuadOrder
{
    private class LASERSet
    {
        public LASER laser;
        public List<List<int>> vertIndixes;

        public LASERSet(LASER laser, int cellCount)
        {
            this.laser = laser;
            this.vertIndixes = new List<List<int>>(cellCount);
            for (int i = 0; i < cellCount; i++) vertIndixes.Add(new List<int>());
        }
    }

    public void UpdateLASERVerts(NativeList<float2> vertsSet, ref List<List<int>> quadVerts)
    {
        foreach (List<int> list in quadVerts) list.Clear();

        if (!vertsSet.IsCreated || vertsSet.Length == 0) return;

        if (!laserVertCellIndices.IsCreated)
        {
            laserVertCellIndices = new NativeList<int>(math.max(vertsSet.Length, 1), Allocator.Persistent);
        }
        if (laserVertCellIndices.Capacity < vertsSet.Length)
        {
            laserVertCellIndices.Capacity = vertsSet.Length;
        }
        laserVertCellIndices.ResizeUninitialized(vertsSet.Length);

        LASERQuadJob job = new LASERQuadJob()
        {
            vertsSet = vertsSet.AsArray(),
            vertCellIndices = laserVertCellIndices.AsArray(),
            cellSize = cellSize,
            cellCount = cells.Length
        };

        JobHandle handle = job.Schedule(vertsSet.Length, 64);
        handle.Complete();

        for (int i = 0; i < laserVertCellIndices.Length; i++)
        {
            int n = laserVertCellIndices[i];
            if (n >= 0 && n < quadVerts.Count) quadVerts[n].Add(i);
        }
    }

    public void CheckCollisionWithLASER(float2 pPos)
    {
        PlayerController player = GManager.Control?.PController;
        if (player == null || player.invincible) return;

        int pCell = GetTreeNum(pPos);
        if (pCell == -1) return;

        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null || laser.IsClearing) continue;
            NativeArray<LASERCell> float2sets = laser.GetQuadVerts(pCell);
            if (float2sets.Length == 0)
            {
                float2sets.Dispose();
                continue;
            }
            collisionHitFlag[0] = 0;

            LASERCollisionJob collisionJob = new LASERCollisionJob()
            {
                pPos = pPos,
                laserCells = float2sets,
                isCollided = collisionHitFlag
            };

            JobHandle handle = collisionJob.Schedule(float2sets.Length, 64);
            handle.Complete();

            float2sets.Dispose();
            if (collisionHitFlag[0] != 0)
            {
                NotifyPlayerHit("Laser");
                break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawLaserCollisionGizmos) return;
        if (!Application.isPlaying) return;
        if (allLASERs == null || allLASERs.Count == 0) return;
        DrawLaserCollisionGizmos();
    }

    private void DrawLaserCollisionGizmos()
    {
        Color prevColor = Gizmos.color;
        Gizmos.color = laserCollisionGizmoColor;

        IterateLaserCollisionTriangles((v0, v1, v2) =>
        {
            Gizmos.DrawLine(v0, v1);
            Gizmos.DrawLine(v1, v2);
            Gizmos.DrawLine(v2, v0);
        });

        Gizmos.color = prevColor;
    }

    private void IterateLaserCollisionTriangles(Action<Vector3, Vector3, Vector3> drawTriangle)
    {
        if (allLASERs == null || allLASERs.Count == 0) return;

        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null) continue;

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                NativeArray<LASERCell> cells = laser.GetQuadVerts(cellIndex);
                for (int k = 0; k < cells.Length; k++)
                {
                    LASERCell cell = cells[k];
                    Vector3 v0 = new Vector3(cell.vert0.x, cell.vert0.y, laserCollisionGizmoZ);
                    Vector3 v1 = new Vector3(cell.vert1.x, cell.vert1.y, laserCollisionGizmoZ);
                    Vector3 v2 = new Vector3(cell.vert2.x, cell.vert2.y, laserCollisionGizmoZ);
                    drawTriangle(v0, v1, v2);
                }
                cells.Dispose();
            }
        }
    }
}
