using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class BurnLeafRenderer : MonoBehaviour
{
    [Header("Textura propia de este objeto")]
    public Texture2D albedo;

    [Header("Burn")]
    [Range(0f, 1f)] public float burnProgress = 0f;

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    static readonly int PropAlbedo   = Shader.PropertyToID("_BaseMap");
    static readonly int PropProgress = Shader.PropertyToID("_BurnProgress");

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        Apply();
    }

    public void SetBurn(float progress)
    {
        burnProgress = progress;
        Apply();
    }

    void Apply()
    {
        _renderer.GetPropertyBlock(_mpb);
        if (albedo != null)
            _mpb.SetTexture(PropAlbedo, albedo);
        _mpb.SetFloat(PropProgress, burnProgress);
        _renderer.SetPropertyBlock(_mpb);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        _renderer = GetComponent<Renderer>();
        _mpb ??= new MaterialPropertyBlock();
        Apply();
    }
#endif
}