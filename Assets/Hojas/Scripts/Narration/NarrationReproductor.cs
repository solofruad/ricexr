using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reproductor de las voces del juego.
/// Mantiene una cola para las secuencias y controla el AudioSource que las reproduce.
/// </summary>
public class NarrationReproductor : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Fuente de audio donde sonarán los clips")]
    [SerializeField] private AudioSource audioSource;

    private Queue<AudioClip> _audioQueue = new Queue<AudioClip>();
    private Coroutine _playCoroutine;

    /// <summary>Indica si las voces están habilitadas.</summary>
    public bool VoicesEnabled { get; private set; } = true;

    /// <summary>Velocidad actual aplicada al AudioSource.</summary>
    public float VoicePitch { get; private set; } = 1f;

    private void Awake()
    {
        VoicesEnabled = GameOptions.VoicesEnabled;
        VoicePitch = GameOptions.VoicePitch;
        ApplyAudioSettings();
    }

    /// <summary>
    /// Activa o desactiva las voces. Al desactivarlas se detiene el audio actual
    /// y se descartan las frases que estaban esperando en la cola.
    /// </summary>
    public void SetVoicesEnabled(bool enabled)
    {
        VoicesEnabled = enabled;
        if (!enabled)
            Stop();
    }

    /// <summary>Actualiza la velocidad de reproducción de las voces.</summary>
    public void SetVoicePitch(float pitch)
    {
        VoicePitch = Mathf.Clamp(pitch, 0.5f, 3f);
        ApplyAudioSettings();
    }

    public void Stop()
    {
        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }
        
        _audioQueue.Clear();
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Detiene lo que esté sonando y reproduce el nuevo audio inmediatamente.
    /// </summary>
    public void Speak(AudioClip clip)
    {
        Stop();
        EnqueueClip(clip, true);
    }

    /// <summary>
    /// Añade el audio a la cola para que se reproduzca cuando termine el actual.
    /// </summary>
    public void SpeakQueued(AudioClip clip)
    {
        EnqueueClip(clip, false);
    }

    private void EnqueueClip(AudioClip clip, bool startImmediately)
    {
        if (!VoicesEnabled) return;

        if (clip == null)
        {
            Debug.LogWarning("[NarrationReproductor] Audio nulo.");
            return;
        }

        _audioQueue.Enqueue(clip);

        if (_playCoroutine == null || startImmediately)
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);

            _playCoroutine = StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("[NarrationReproductor] AudioSource no asignado.");
            _audioQueue.Clear();
            _playCoroutine = null;
            yield break;
        }

        while (_audioQueue.Count > 0)
        {
            AudioClip nextClip = _audioQueue.Dequeue();
            audioSource.clip = nextClip;
            audioSource.Play();
            
            // El pitch cambia la duración real del clip, por eso la espera también se ajusta.
            float playbackSpeed = Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch));
            yield return new WaitForSeconds(nextClip.length / playbackSpeed);
        }
        
        _playCoroutine = null;
    }

    private void ApplyAudioSettings()
    {
        if (audioSource != null)
            audioSource.pitch = VoicePitch;
    }
}
