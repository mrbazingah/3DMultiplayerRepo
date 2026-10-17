using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [field:SerializeField] public AudioClip shootSfx { get; private set; }
    [field:SerializeField] public AudioClip reloadSfx { get; private set; }
    [field:SerializeField] public AudioClip doorOpenSfx { get; private set; }
    [field:SerializeField] public AudioClip doorCloseSfx { get; private set; }
    [field:SerializeField] public AudioClip propSwapSfx { get; private set; }
    [field:SerializeField] public AudioClip propInvisSfx { get; private set; }
    
    [SerializeField] AudioClip[] footstepsSfx;
    [SerializeField] GameObject audioSourcePrefab;
    [SerializeField] float sfxVolume;
    [SerializeField] float sfxDestroyDelay;

    List<AudioClip> footstepSfxBag = new List<AudioClip>();

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

    public AudioClip GetFootstepSfx()
    {
        if (footstepSfxBag.Count == 0)
        {
            footstepSfxBag.AddRange(footstepsSfx);
        }

        int randomIndex = Random.Range(0, footstepSfxBag.Count);
        AudioClip selectedClip = footstepSfxBag[randomIndex];
        footstepSfxBag.RemoveAt(randomIndex);

        return selectedClip;
    }
}
