using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NarrationReproductor : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Fuente de audio donde sonarán los clips")]
    [SerializeField] private AudioSource audioSource;

    private Queue<AudioClip> _audioQueue = new Queue<AudioClip>();
    private Coroutine _playCoroutine;

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
            
            // Esperamos la duración exacta del clip antes de pasar al siguiente en la cola
            yield return new WaitForSeconds(nextClip.length);
        }
        
        _playCoroutine = null;
    }
}