using UnityEngine;
using Unity.Mathematics;

using BulletHell.App;
using BulletHell.Audio;

public class NewMonoBehaviourScript : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private BeatManager BManager;

    void Init(BeatManager BManager)
    {
        this.BManager = BManager;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

    }

    // Update is called once per frame
    void Update()
    {
        if (true) return;

        transform.localScale = Vector3.one * (1 + BManager.beatValueSin);
        spriteRenderer.color = new Color(1, 1 - BManager.beatValueSin, 1 - BManager.beatValueSin, 1);
    }
}
