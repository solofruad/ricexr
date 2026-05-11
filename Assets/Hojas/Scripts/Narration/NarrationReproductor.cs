using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NarrationReproductor : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Fuente de audio donde sonarán los clips")]
    [SerializeField] private AudioSource audioSource;
    
    [Tooltip("Ruta dentro de 'Resources' donde están los audios")]
    [SerializeField] private string folderPath = "Audio/Narration/Spanish";

    private Dictionary<string, AudioClip> _audioClips;
    private Queue<AudioClip> _audioQueue = new Queue<AudioClip>();
    private Coroutine _playCoroutine;

    private void Awake()
    {
        LoadAudios();
    }

    private void LoadAudios()
    {
        _audioClips = new Dictionary<string, AudioClip>();
        AudioClip[] loadedClips = Resources.LoadAll<AudioClip>(folderPath);

        foreach (AudioClip clip in loadedClips)
        {
            if (!_audioClips.ContainsKey(clip.name))
            {
                _audioClips.Add(clip.name, clip);
            }
        }
        Debug.Log($"[NarrationReproductor] Cargados {_audioClips.Count} audios desde Resources/{folderPath}");
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
    public void Speak(string id)
    {
        Stop();

        if (_audioClips.TryGetValue(id, out AudioClip clip))
        {
            _audioQueue.Enqueue(clip);
            _playCoroutine = StartCoroutine(ProcessQueue());
        }
        else
        {
            Debug.LogError($"[NarrationReproductor] Audio no encontrado: {id}");
        }
    }

    /// <summary>
    /// Añade el audio a la cola para que se reproduzca cuando termine el actual.
    /// </summary>
    public void SpeakQueued(string id)
    {
        if (_audioClips.TryGetValue(id, out AudioClip clip))
        {
            _audioQueue.Enqueue(clip);
            
            // Si la corrutina no está corriendo, la iniciamos
            if (_playCoroutine == null)
            {
                _playCoroutine = StartCoroutine(ProcessQueue());
            }
        }
        else
        {
            Debug.LogError($"[NarrationReproductor] Audio no encontrado para encolar: {id}");
        }
    }

    private IEnumerator ProcessQueue()
    {
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