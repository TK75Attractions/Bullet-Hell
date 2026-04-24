using UnityEngine;
using Unity.Mathematics;

public class NewMonoBehaviourScript : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

    }

    // Update is called once per frame
    void Update()
    {

        transform.localScale = Vector3.one * (1 + GManager.Control.BManager.beatValueSin);
        spriteRenderer.color = new Color(1, 1 - GManager.Control.BManager.beatValueSin, 1 - GManager.Control.BManager.beatValueSin, 1);
    }
}
