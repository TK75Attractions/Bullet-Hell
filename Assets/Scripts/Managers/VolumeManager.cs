using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class VolumeManager : MonoBehaviour
{
    [SerializeField] private Volume volume;
    [SerializeField, Range(0f, 1f)] private float defaultWeight = 1f;

    private bool isReady;
    private Coroutine blendCoroutine;

    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        if (volume == null)
        {
            volume = GetComponent<Volume>();
        }

        if (volume == null)
        {
            Debug.LogError("VolumeManager: Volume component was not found.", this);
            isReady = false;
            return;
        }

        volume.weight = Mathf.Clamp01(defaultWeight);
        isReady = true;
    }

    public void SetVolumeActive(bool active)
    {
        if (!EnsureReady()) return;
        volume.enabled = active;
    }

    public void SetWeight(float weight)
    {
        if (!EnsureReady()) return;
        volume.weight = Mathf.Clamp01(weight);
    }

    public float GetWeight()
    {
        if (!EnsureReady()) return 0f;
        return volume.weight;
    }

    public void SetProfile(VolumeProfile profile)
    {
        if (!EnsureReady()) return;
        volume.profile = profile;
    }

    public bool TryGetOverride<T>(out T component) where T : VolumeComponent
    {
        component = null;
        if (!EnsureReady()) return false;
        if (volume.profile == null) return false;

        return volume.profile.TryGet(out component);
    }

    public void BlendWeight(float targetWeight, float duration)
    {
        if (!EnsureReady()) return;

        if (blendCoroutine != null)
        {
            StopCoroutine(blendCoroutine);
        }

        blendCoroutine = StartCoroutine(BlendWeightRoutine(Mathf.Clamp01(targetWeight), duration));
    }

    private IEnumerator BlendWeightRoutine(float targetWeight, float duration)
    {
        float start = volume.weight;

        if (duration <= 0f)
        {
            volume.weight = targetWeight;
            blendCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            volume.weight = Mathf.Lerp(start, targetWeight, t);
            yield return null;
        }

        volume.weight = targetWeight;
        blendCoroutine = null;
    }

    private bool EnsureReady()
    {
        if (isReady) return true;

        Init();
        return isReady;
    }
}
