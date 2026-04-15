using System;
using UnityEngine;

namespace Sounds
{
    [Serializable]
    internal struct SfxClip
    {
        public string name;
        public AudioClip clip;
    }
    [RequireComponent(typeof(AudioSource))]
    public class SFXManager : MonoBehaviour
    {
        [SerializeField] private SfxClip[] sfxClips;
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        public void PlaySfx(string sfxName)
        {
            if (string.IsNullOrWhiteSpace(sfxName))
            {
                Debug.LogWarning("SFXManager: SFX name is null or empty.", this);
                return;
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    Debug.LogWarning("SFXManager: Missing AudioSource component.", this);
                    return;
                }
            }

            if (sfxClips == null || sfxClips.Length == 0)
            {
                Debug.LogWarning("SFXManager: No SFX clips configured.", this);
                return;
            }

            SfxClip? clipToPlay = null;
            foreach (var sfxClip in sfxClips)
            {
                if (string.Equals(sfxClip.name, sfxName, StringComparison.OrdinalIgnoreCase))
                {
                    clipToPlay = sfxClip;
                    break;
                }
            }

            if (clipToPlay.HasValue)
            {
                if (clipToPlay.Value.clip == null)
                {
                    Debug.LogWarning($"SFXManager: SFX '{sfxName}' has no AudioClip assigned.", this);
                    return;
                }

                _audioSource.PlayOneShot(clipToPlay.Value.clip);
            }
            else
            {
                Debug.LogWarning($"SFXManager: SFX with name '{sfxName}' not found.", this);
            }
        }
    }
}
