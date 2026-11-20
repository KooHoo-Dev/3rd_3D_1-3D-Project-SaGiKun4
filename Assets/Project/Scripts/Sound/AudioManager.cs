using System.Collections;
using UnityEngine;

namespace Study3D.FpsGame
{
    // AudioManager
    // : 배경음(BGM)과 효과음(SFX)을 재생합니다. 2D 프로젝트의 AudioManager를 3D 공간 음향에 맞게 고친 버전입니다.
    // - PlaySfx(sfx)             : 2D 효과음. 어디서 듣든 같은 크기로 들립니다. (UI, 플레이어 자신의 소리)
    // - PlaySfxAt(sfx, position) : 3D 효과음. position에서 소리가 나서, 멀수록 작아지고 왼쪽/오른쪽이 구분됩니다. (적, 적중, 폭발)
    // - 3D 채널은 채널마다 게임오브젝트가 따로 있습니다.
    //   AudioSource는 자기 Transform 위치에서 소리를 내기 때문에, 2D 버전처럼 한 오브젝트에 몰아 붙이면 모든 소리가 같은 곳에서 납니다.
    // - 같은 효과음은 한 프레임에 한 번만 재생합니다. (발사체 여러 개가 동시에 맞아도 소리가 겹쳐 커지거나 채널이 바닥나지 않도록)
    // - 씬마다 하나씩 둡니다. 보스 씬처럼 배경음이 다른 씬은 그 씬의 AudioManager에서 시작 배경음을 정합니다.
    public class AudioManager : MonoBehaviour
    {
        // 순서가 sfxClips 배열 순서와 같아야 합니다. 실제 넣을 소리에 맞게 이름을 바꿔 쓰세요.
        public enum Sfx
        {
            Spin = 0,
            Click,
            SkeletonDied,
            SkeletonHit1,
            SkeletonHit2,
            SkeletonHit3,
            Explosion,
            Shot1,
            Shot2,
            Shot3,
            Shot4,
            Shot5,
            CardHit,
            EnemyDie,
            PlayerDamaged,
            Pickup
        }

        // 순서가 bgmClips 배열 순서와 같아야 합니다.
        public enum Bgm
        {
            Map = 0,
            Boss
        }

        public static AudioManager Instance { get; private set; }

        [Header("배경음")]
        [SerializeField] private AudioClip[] bgmClips;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
        [Tooltip("씬이 시작되면 startBgm을 재생합니다")]
        [SerializeField] private bool playBgmOnStart = true;
        [SerializeField] private Bgm startBgm = Bgm.Map;

        [Header("효과음")]
        [SerializeField] private AudioClip[] sfxClips;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [Tooltip("2D 효과음을 동시에 재생할 수 있는 수")]
        [SerializeField] private int channels2D = 8;
        [Tooltip("3D 효과음을 동시에 재생할 수 있는 수")]
        [SerializeField] private int channels3D = 16;

        [Header("3D 공간 음향")]
        [Tooltip("소리를 듣는 위치. 보통 카메라에 붙어 있는 AudioListener를 넣습니다")]
        [SerializeField] private AudioListener listener;
        [Tooltip("이 거리 안에서는 가장 크게 들립니다")]
        [SerializeField] private float minDistance = 2f;
        [Tooltip("이 거리에서 소리가 0이 됩니다. 더 먼 소리는 채널을 쓰지 않고 생략합니다")]
        [SerializeField] private float maxDistance = 30f;

        private AudioSource bgmPlayer;
        private AudioSource[] sfxPlayers2D;
        private AudioSource[] sfxPlayers3D;
        private int channelIndex2D;
        private int channelIndex3D;
        private int[] lastPlayedFrames; // 효과음마다 마지막으로 재생한 프레임

        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: AudioManager가 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
                enabled = false;
                return;
            }

            Instance = this;
            CreateBgmPlayer();
            sfxPlayers2D = Create2DChannels();
            sfxPlayers3D = Create3DChannels();
            CreateFrameRecords();
        }

        private void Start()
        {
            if (playBgmOnStart)
            {
                PlayBgm(startBgm);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------- 만들기 ----------

        // 배경음은 위치와 상관없이 들려야 하므로 2D로 둡니다. (spatialBlend 기본값 0 = 2D)
        private void CreateBgmPlayer()
        {
            GameObject bgmObject = new GameObject("BgmPlayer");
            bgmObject.transform.SetParent(transform, false);
            bgmPlayer = bgmObject.AddComponent<AudioSource>();
            bgmPlayer.playOnAwake = false;
            bgmPlayer.loop = true;
            bgmPlayer.volume = bgmVolume;
        }

        // 2D 채널은 위치가 필요 없으므로 2D 버전처럼 한 오브젝트에 모아 붙입니다.
        private AudioSource[] Create2DChannels()
        {
            GameObject sfxObject = new GameObject("Sfx2DPlayers");
            sfxObject.transform.SetParent(transform, false);

            AudioSource[] players = new AudioSource[channels2D];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = sfxObject.AddComponent<AudioSource>();
                players[i].playOnAwake = false;
                players[i].volume = sfxVolume;
            }
            return players;
        }

        // 3D 채널은 소리 날 위치로 옮겨야 하므로 채널마다 오브젝트를 따로 만듭니다.
        private AudioSource[] Create3DChannels()
        {
            GameObject root = new GameObject("Sfx3DPlayers");
            root.transform.SetParent(transform, false);

            AudioSource[] players = new AudioSource[channels3D];
            for (int i = 0; i < players.Length; i++)
            {
                GameObject channel = new GameObject($"Sfx3D_{i}");
                channel.transform.SetParent(root.transform, false);

                players[i] = channel.AddComponent<AudioSource>();
                players[i].playOnAwake = false;
                players[i].volume = sfxVolume;
                Setup3D(players[i]);
            }
            return players;
        }

        // AudioSource를 공간 음향으로 설정합니다.
        private void Setup3D(AudioSource player)
        {
            // 0 = 2D (위치 무시), 1 = 완전한 3D (거리, 방향 반영)
            player.spatialBlend = 1f;
            // Linear : 거리에 따라 일정하게 작아지다가 maxDistance에서 0이 됩니다.
            // (기본값 Logarithmic은 maxDistance에서도 소리가 남아 있어서, 먼 소리를 생략하는 기준과 맞지 않습니다)
            player.rolloffMode = AudioRolloffMode.Linear;
            player.minDistance = minDistance;
            player.maxDistance = maxDistance;
            // 도플러 효과를 끕니다. 카메라가 빠르게 돌거나 움직일 때 효과음의 음높이가 흔들리지 않도록
            player.dopplerLevel = 0f;
        }

        // 효과음 종류마다 "마지막으로 재생한 프레임"을 기록할 칸을 만듭니다.
        private void CreateFrameRecords()
        {
            int sfxCount = System.Enum.GetValues(typeof(Sfx)).Length;
            lastPlayedFrames = new int[sfxCount];
            for (int i = 0; i < lastPlayedFrames.Length; i++)
            {
                lastPlayedFrames[i] = -1;
            }
        }

        // ---------- 배경음 ----------

        public void PlayBgm(Bgm bgm)
        {
            int index = (int)bgm;
            if (index >= bgmClips.Length || bgmClips[index] == null)
            {
                Debug.LogWarning($"{bgm} 배경음 클립이 없습니다");
                return;
            }

            bgmPlayer.clip = bgmClips[index];
            bgmPlayer.Play();
        }

        public void StopBgm()
        {
            bgmPlayer.Stop();
        }

        // ---------- 효과음 ----------

        // 2D 효과음: 어디서 듣든 같은 크기로 재생합니다.
        public void PlaySfx(Sfx sfx, float pitch = 1f)
        {
            if (CanPlay(sfx) == false)
            {
                return;
            }

            int index = FindIdleChannel(sfxPlayers2D, channelIndex2D);
            if (index < 0)
            {
                return; // 빈 채널이 없으면 생략합니다.
            }

            channelIndex2D = index;
            PlayOn(sfxPlayers2D[index], sfx, pitch);
        }

        // 3D 효과음: position에서 소리가 납니다. 듣는 위치에서 maxDistance보다 멀면 재생하지 않습니다.
        public void PlaySfxAt(Sfx sfx, Vector3 position, float pitch = 1f)
        {
            if (IsTooFar(position))
            {
                return;
            }

            if (CanPlay(sfx) == false)
            {
                return;
            }

            int index = FindIdleChannel(sfxPlayers3D, channelIndex3D);
            if (index < 0)
            {
                return; // 빈 채널이 없으면 생략합니다.
            }

            channelIndex3D = index;
            AudioSource player = sfxPlayers3D[index];
            player.transform.position = position;
            PlayOn(player, sfx, pitch);
        }

        // 클립이 있고, 이번 프레임에 아직 재생하지 않은 효과음이면 재생 기록을 남기고 true를 반환합니다.
        private bool CanPlay(Sfx sfx)
        {
            int id = (int)sfx;
            if (id >= sfxClips.Length || sfxClips[id] == null)
            {
                Debug.LogWarning($"{sfx} 효과음 클립이 없습니다");
                return false;
            }

            if (lastPlayedFrames[id] == Time.frameCount)
            {
                return false;
            }

            lastPlayedFrames[id] = Time.frameCount;
            return true;
        }

        // 듣는 위치에서 maxDistance보다 먼지. 어차피 들리지 않는 소리에 채널을 쓰지 않으려고 미리 거릅니다.
        private bool IsTooFar(Vector3 position)
        {
            if (listener == null)
            {
                return false;
            }

            Vector3 offset = position - listener.transform.position;
            return offset.sqrMagnitude > maxDistance * maxDistance;
        }

        // startIndex부터 한 바퀴 돌면서 재생 중이 아닌 채널을 찾습니다. 없으면 -1 입니다.
        private int FindIdleChannel(AudioSource[] players, int startIndex)
        {
            for (int i = 0; i < players.Length; i++)
            {
                int loopIndex = (i + startIndex) % players.Length;
                if (players[loopIndex].isPlaying)
                {
                    continue;
                }

                return loopIndex;
            }
            return -1;
        }

        private void PlayOn(AudioSource player, Sfx sfx, float pitch)
        {
            player.clip = sfxClips[(int)sfx];
            player.pitch = pitch;
            player.volume = sfxVolume; // 페이드아웃으로 줄어든 볼륨이 남지 않도록 다시 맞춥니다.
            player.Play();
        }

        // sfx를 재생 중인 채널(2D, 3D)을 모두 멈춥니다.
        // fadeTime 동안 소리를 줄인 뒤 멈춥니다. 소리를 중간에 바로 끊으면 '뚝' 하는 잡음이 나기 때문입니다.
        public void StopSfx(Sfx sfx, float fadeTime = 0f)
        {
            int id = (int)sfx;
            if (id >= sfxClips.Length || sfxClips[id] == null)
            {
                return;
            }

            StopClip(sfxPlayers2D, sfxClips[id], fadeTime);
            StopClip(sfxPlayers3D, sfxClips[id], fadeTime);
        }

        private void StopClip(AudioSource[] players, AudioClip clip, float fadeTime)
        {
            for (int i = 0; i < players.Length; i++)
            {
                AudioSource player = players[i];
                if (player.isPlaying == false || player.clip != clip)
                {
                    continue;
                }

                if (fadeTime <= 0f)
                {
                    player.Stop();
                    continue;
                }

                StartCoroutine(FadeOutAndStop(player, fadeTime));
            }
        }

        // fadeTime 동안 볼륨을 0까지 줄이고 멈춘 뒤, 다음 재생을 위해 볼륨을 되돌립니다.
        // 줄이는 동안에도 채널은 재생 중이라서, 다른 효과음이 이 채널을 가져가지 않습니다.
        private IEnumerator FadeOutAndStop(AudioSource player, float fadeTime)
        {
            float startVolume = player.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                player.volume = startVolume * (1f - elapsed / fadeTime);
                yield return null;
            }

            player.Stop();
            player.volume = sfxVolume;
        }

        // ---------- 볼륨 (옵션 UI) ----------

        public void SetBgmVolume(float volume)
        {
            bgmVolume = volume;
            bgmPlayer.volume = volume;
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = volume;
            SetVolume(sfxPlayers2D, volume);
            SetVolume(sfxPlayers3D, volume);
        }

        private void SetVolume(AudioSource[] players, float volume)
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].volume = volume;
            }
        }
    }
}
