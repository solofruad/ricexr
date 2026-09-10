using System;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Renderer))]
public class BurnLeafRenderer : MonoBehaviour
{
    [Header("Textura propia de este objeto")]
    public Texture2D albedo;

    [Header("Burn")]
    [Range(0f, 1f)] public float burnProgress = 0f;

    [Tooltip("Duracion del efecto en segundos")]
    public float burnDuration = 1.5f;
    [Tooltip("Tiempo de espera antes de empezar a quemarse")]
    public float delayBeforeBurn = 1.0f;

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    static readonly int PropAlbedo   = Shader.PropertyToID("_BaseMap");
    static readonly int PropProgress = Shader.PropertyToID("_BurnProgress");

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        Apply();
    }

    // -------------------------------------------------------------------------
    // API pública
    // -------------------------------------------------------------------------

    private Coroutine _burnCoroutine;
    private Tween _burnTween;

    /// <summary>
    /// Anima el efecto de quemado de 0 a 1 usando DOTween.
    /// Llama a <paramref name="onComplete"/> al terminar.
    /// Leaf (u otro sistema) llama a este método para arrancar el burn.
    /// </summary>
    public void Burn(Action onComplete = null)
    {
        // Resetear al inicio
        SetBurn(0f);

        if (_burnCoroutine != null) StopCoroutine(_burnCoroutine);
        _burnCoroutine = StartCoroutine(BurnRoutine(onComplete));
    }

    private System.Collections.IEnumerator BurnRoutine(Action onComplete)
    {
        yield return new WaitForSeconds(delayBeforeBurn);

        _burnTween?.Kill();
        _burnTween = DOTween.To(
            () => burnProgress,
            x  => SetBurn(x),
            1f,
            burnDuration
        ).SetLink(gameObject).OnComplete(() => onComplete?.Invoke());
    }

    private void OnDisable()
    {
        if (_burnCoroutine != null)
        {
            StopCoroutine(_burnCoroutine);
            _burnCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        _burnTween?.Kill();
    }

    /// <summary>
    /// Establece el progreso de quemado manualmente (útil para debug o control externo).
    /// </summary>
    public void SetBurn(float progress)
    {
        burnProgress = progress;
        Apply();
    }

    // -------------------------------------------------------------------------
    // Interno
    // -------------------------------------------------------------------------

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