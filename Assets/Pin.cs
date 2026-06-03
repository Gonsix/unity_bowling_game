using UnityEngine;

public class Pin : MonoBehaviour
{
    [Header("Fall Feedback")]
    [SerializeField] private bool playFallEffect = true;
    [SerializeField] private Color fallEffectColor = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField] private int fallEffectParticleCount = 24;
    [SerializeField] private float fallEffectLifetime = 1.2f;

    [Header("Fall Sound")]
    [SerializeField] private bool playFallSound = true;
    [SerializeField] private float fallSoundVolume = 0.6f;

    private static AudioClip generatedFallClip;
    private AudioSource audioSource;
    private bool wasStanding;
    private bool feedbackPlayed;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = fallSoundVolume;
    }

    private void Start()
    {
        wasStanding = IsStanding();
    }

    private void Update()
    {
        bool isStanding = IsStanding();

        if (!feedbackPlayed && wasStanding && !isStanding)
        {
            PlayFallFeedback();
            feedbackPlayed = true;
        }

        wasStanding = isStanding;
    }

    public bool IsStanding()
    {
        // ピンが立っているかどうかを判定するロジックをここに実装
        // 立っている状態と、現在の状態の内積をとる。　現在の状態が立っていれば内積は１に近い値になる。　
        // ピンがぐらつくことだけの場合もあるから、閾値は多分必要　コサイン45度=0.707くらいでいいと思う
        return Vector3.Dot(transform.up, Vector3.up) > 0.707f;
    }

    private void PlayFallFeedback()
    {
        if (playFallEffect)
        {
            CreateFallEffect();
        }

        if (playFallSound)
        {
            PlayFallSound();
        }
    }

    private void PlayFallSound()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        audioSource.volume = fallSoundVolume;
        audioSource.PlayOneShot(GetFallSoundClip(), fallSoundVolume);
    }

    private void CreateFallEffect()
    {
        GameObject effectObject = new GameObject("Pin Fall Effect");
        effectObject.transform.position = transform.position + Vector3.up * 0.35f;

        ParticleSystem particleSystem = effectObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = fallEffectColor;
        main.gravityModifier = 1.2f;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial();

        particleSystem.Emit(fallEffectParticleCount);
        Destroy(effectObject, fallEffectLifetime);
    }

    private Material CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = fallEffectColor;
        return material;
    }

    private AudioClip GetFallSoundClip()
    {
        if (generatedFallClip != null)
        {
            return generatedFallClip;
        }

        const int sampleRate = 44100;
        const float duration = 0.18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(1729);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float fade = Mathf.Exp(-t * 18f);
            float metallicTone = Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.55f;
            float lowKnock = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.3f;
            float noise = ((float)random.NextDouble() * 2f - 1f) * 0.25f;

            samples[i] = (metallicTone + lowKnock + noise) * fade;
        }

        generatedFallClip = AudioClip.Create("Generated Pin Fall", sampleCount, 1, sampleRate, false);
        generatedFallClip.SetData(samples, 0);
        return generatedFallClip;
    }
}
