using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioManager : SingletonBehaviour<AudioManager>
{
    [Header("Sources (optional)")]
    [SerializeField]
    private AudioSource bgmSource;

    [SerializeField]
    private AudioSource sfxSource;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField]
    private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float bgmVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float sfxVolume = 1f;

    [Header("Mute")]
    [SerializeField]
    private bool muteAll = false;

    [SerializeField]
    private bool muteBgm = false;

    [SerializeField]
    private bool muteSfx = false;

    protected override void Awake()
    {
        base.Awake();
        if (!IsPrimaryInstance)
            return;

        EnsureAudioSources();
        ApplyVolume();
    }

    public void InitializeTemplate()
    {
        EnsureAudioSources();
        ApplyVolume();
    }

    public void PlayBgm(AudioClip clip, bool loop = true)
    {
        if (muteAll || muteBgm)
            return;
        if (clip == null)
            return;

        EnsureAudioSources();

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = masterVolume * bgmVolume;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null)
            return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (muteAll || muteSfx)
            return;
        if (clip == null)
            return;

        EnsureAudioSources();
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * sfxVolume * volumeScale));
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = muteAll || muteBgm ? 0f : masterVolume * bgmVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = muteAll || muteSfx ? 0f : masterVolume * sfxVolume;
        }
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = CreateChildSource("BGM Source");
            bgmSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = CreateChildSource("SFX Source");
            sfxSource.loop = false;
        }
    }

    private AudioSource CreateChildSource(string childName)
    {
        Transform existing = transform.Find(childName);
        GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.GetComponent<AudioSource>();
        if (source == null)
        {
            source = child.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;

        return source;
    }
}
