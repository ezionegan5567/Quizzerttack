using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    [Header("Configuración de Audio")]
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource extrasAudioSource;

    [SerializeField] private string extraVolume = "ExtraVolume";
    [SerializeField] private string musicVolume = "MusicVolume";
    [SerializeField] private string sfxVolume = "SFXVolume";

    [Header("UI Slider")]
    [SerializeField] private Slider extrasSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Music Tracks")]
    [SerializeField] private List<AudioClip> musicTracks = new List<AudioClip>();

    [Header("SFX Clips")]
    [SerializeField] private List<AudioClip> sfxClips = new List<AudioClip>();

    [Header("Extras")]
    [SerializeField] private List<AudioClip> extraClips = new List<AudioClip>();

    public void PlayMusicTrack(int index)
    {
        if (musicAudioSource == null) return;

        if (index >= 0 && index < musicTracks.Count && musicTracks[index] != null)
        {
            if (musicAudioSource.clip == musicTracks[index] && musicAudioSource.isPlaying)
            {
                return;
            }

            musicAudioSource.clip = musicTracks[index];
            musicAudioSource.loop = true;
            musicAudioSource.Play();
        }
    }

    public void PlaySFX(int index)
    {
        if (sfxAudioSource == null) return;

        if (index >= 0 && index < sfxClips.Count && sfxClips[index] != null)
        {
            sfxAudioSource.PlayOneShot(sfxClips[index]);
        }
    }
}