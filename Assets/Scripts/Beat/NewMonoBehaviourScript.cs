using UnityEngine;

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
        if (GManager.Control == null || GManager.Control.BManager == null)
        {
            return;
        }

        float beat = GManager.Control.BManager.BeatValueSin;
        transform.localScale = Vector3.one * (1 + beat);
        spriteRenderer.color = new Color(1, 1 - beat, 1 - beat, 1);
    }
}
