using UnityEditor.SceneManagement;
using UnityEngine;

public class StageReader : MonoBehaviour
{
    private StageData stageData;
    private float time = 0f;
    private int count = 0;
    private bool isReady = false;

    public void Init(StageData data)
    {
        stageData = data;
        time = 0f;
        count = 0;
        if (GManager.Control.AManager != null) GManager.Control.AManager.PlayBGM(stageData.audioClip);
        if (GManager.Control.BManager != null) GManager.Control.BManager.SetBeat(stageData.MusicEvents);
        isReady = true;
    }

    public void UpdateStage(float dt)
    {
        if (stageData == null || !isReady) return;
        time += dt;

        if (stageData.enemySpawners.Count > count)
        {
            if (stageData.enemySpawners[count].next <= time)
            {
                EnemySpawner spawner = stageData.enemySpawners[count];
                GManager.Control.QOrder.AddEnemy(spawner);
                Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
                count++;
                time = 0;
                if (count >= stageData.enemySpawners.Count) isReady = false;
            }
        }
    }



}
