using System;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [field:SerializeField] public AudioClip shootSfx { get; private set; }
    [field:SerializeField] public AudioClip reloadSfx { get; private set; }
    [field:SerializeField] public AudioClip doorOpenSfx { get; private set; }
    [field:SerializeField] public AudioClip doorCloseSfx { get; private set; }

    [SerializeField] GameObject audioSourcePrefab;
    [SerializeField] float sfxVolume;
    [SerializeField] float sfxDestroyDelay;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    public void PlaySfx(AudioClip clip, Transform parent)
    {
        GameObject audioSourceObject = Instantiate(audioSourcePrefab, parent);
        AudioSource audioSource = audioSourceObject.GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = sfxVolume;
        audioSource.Play();
        Destroy(audioSourceObject, sfxDestroyDelay);
    }
}
