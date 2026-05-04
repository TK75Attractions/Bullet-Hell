using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine;

namespace BulletHell.Bullets
{
    public class LaserCollisionService : IDisposable
    {
        private NativeArray<int> collisionHitFlag;
        private NativeList<int> laserVertCellIndices;
        private LaserEmitter laserEmitter;
        public List<LASER> allLASERs = new();
        
        [SerializeField] private bool drawLaserCollisionGizmos = true;

        [SerializeField] private Color laserCollisionGizmoColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private float laserCollisionGizmoZ = 100f;

        private IQuadGrid grid;

        public LaserCollisionService(LaserEmitter emitter, IQuadGrid quadGrid)
        {
            laserEmitter = emitter;
            grid = quadGrid;
        }

        public void Init(List<BulletClip> clips)
        {
            if (!collisionHitFlag.IsCreated)
            {
                collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
            }
            if (!laserVertCellIndices.IsCreated)
            {
                laserVertCellIndices = new NativeList<int>(256, Allocator.Persistent);
            }

            LaserAddRange(clips[0], new float2(0, 0));
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
                cellSize = grid.cellSize,
                cellCount = grid.cellCount
            };

            JobHandle handle = job.Schedule(vertsSet.Length, 64);
            handle.Complete();

            for (int i = 0; i < laserVertCellIndices.Length; i++)
            {
                int n = laserVertCellIndices[i];
                if (n >= 0 && n < quadVerts.Count) quadVerts[n].Add(i);
            }
        }

        public void CheckCollisionWithLASER(float _dt, float2 pPos)
        {
            for (int i = 0; i < allLASERs.Count; i++)
            {
                if (allLASERs[i].UpdateSet(_dt))
                {
                    allLASERs[i].Destroy();
                    allLASERs.RemoveAt(i);
                    i--;
                }
            }
            
            int pCell = grid.GetTreeNum(pPos);
            if (pCell == -1) return;

            for (int i = 0; i < allLASERs.Count; i++)
            {
                LASER laser = allLASERs[i];
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
                    break;
                }
            }
        }

        public void LaserAddRange(List<BulletData> bullets, float2 pos)
        {
            List<LASER> lasers = laserEmitter.EmitLASER(bullets, pos);
            for (int i = 0; i < lasers.Count; i++)
            {
                lasers[i].SetCollisionService(this);
            }
            allLASERs.AddRange(lasers);
        }

        private void LaserAddRange(BulletClip clip, float2 pos)
        {
            List<LASER> lasers = laserEmitter.EmitLASER(clip, pos);
            for (int i = 0; i < lasers.Count; i++)
            {
                lasers[i].SetCollisionService(this);
            }
            allLASERs.AddRange(lasers);
        }
        public void Dispose()
        {
            if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
            if (laserVertCellIndices.IsCreated) laserVertCellIndices.Dispose();
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

            for (int cellIndex = 0; cellIndex < grid.cellCount; cellIndex++)
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
}
