using UnityEditor.SceneManagement;
using UnityEngine;
using System.Threading.Tasks;

public class StageReader : MonoBehaviour
{
    private StageData stageData;
    private float time = 0f;
    private int count = 0;
    private bool isReady = false;

    public async void Init(StageData data)
    {
        stageData = data;
        time = 0f;
        count = 0;
        if (GManager.Control.AManager != null && GManager.Control.BManager != null)
        {
            AudioSource bgmSource = await GManager.Control.AManager.PlayBGM(stageData.audioClip);
            await Task.Delay(5000); // Wait a moment to ensure the BGM starts playing
            bgmSource.Play();
            GManager.Control.beatTime -= stageData.delayTime; // Adjust beat time by the delay time
            GManager.Control.BManager.SetBeat(stageData.MusicEvents);
            GManager.Control.musicOn = true;
        }
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