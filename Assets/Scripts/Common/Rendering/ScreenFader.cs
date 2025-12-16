using System;
using System.Collections;
using UnityEngine;

namespace SoloBandStudio.Common.Rendering
{
    /// <summary>
    /// Singleton API for controlling screen fade effects.
    /// Works with ScreenFadeFeature (URP Renderer Feature).
    /// No scene setup required - just add ScreenFadeFeature to your URP Renderer.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        /// <summary>
        /// Gets the singleton instance, creating it if needed.
        /// </summary>
        public static ScreenFader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ScreenFader>();

                    if (_instance == null)
                    {
                        var go = new GameObject("ScreenFader");
                        _instance = go.AddComponent<ScreenFader>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private float defaultFadeDuration = 0.5f;
        [SerializeField] private Color defaultFadeColor = Color.black;

        private Coroutine currentFadeCoroutine;
        private bool isFading;

        public bool IsFading => isFading;
        public float CurrentFadeAmount => ScreenFadePass.FadeAmount;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize fade color
            ScreenFadePass.FadeColor = defaultFadeColor;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                // Reset fade on destroy
                ScreenFadePass.FadeAmount = 0f;
            }
        }

        #region Public API

        /// <summary>
        /// Fade out to color (screen becomes opaque).
        /// </summary>
        public static Coroutine FadeOut(float duration = -1f, Color? color = null)
        {
            return Instance.StartFade(1f, duration, color);
        }

        /// <summary>
        /// Fade in from color (screen becomes transparent).
        /// </summary>
        public static Coroutine FadeIn(float duration = -1f, Color? color = null)
        {
            return Instance.StartFade(0f, duration, color);
        }

        /// <summary>
        /// Fade to a specific amount.
        /// </summary>
        public static Coroutine FadeTo(float targetAmount, float duration = -1f, Color? color = null)
        {
            return Instance.StartFade(targetAmount, duration, color);
        }

        /// <summary>
        /// Immediately set fade amount without animation.
        /// </summary>
        public static void SetFadeImmediate(float amount)
        {
            Instance.StopCurrentFade();
            ScreenFadePass.FadeAmount = Mathf.Clamp01(amount);
        }

        /// <summary>
        /// Set the fade color.
        /// </summary>
        public static void SetFadeColor(Color color)
        {
            ScreenFadePass.FadeColor = color;
        }

        /// <summary>
        /// Execute an action while faded out (fade out -> action -> fade in).
        /// </summary>
        public static Coroutine DoWithFade(Action action, float fadeDuration = -1f)
        {
            return Instance.StartCoroutine(Instance.DoWithFadeCoroutine(action, fadeDuration));
        }

        #endregion

        #region Private Methods

        private Coroutine StartFade(float targetAmount, float duration, Color? color)
        {
            StopCurrentFade();

            if (color.HasValue)
            {
                ScreenFadePass.FadeColor = color.Value;
            }

            float actualDuration = duration >= 0f ? duration : defaultFadeDuration;
            currentFadeCoroutine = StartCoroutine(FadeCoroutine(targetAmount, actualDuration));
            return currentFadeCoroutine;
        }

        private void StopCurrentFade()
        {
            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
                currentFadeCoroutine = null;
            }
            isFading = false;
        }

        private IEnumerator FadeCoroutine(float targetAmount, float duration)
        {
            isFading = true;
            float startAmount = ScreenFadePass.FadeAmount;
            float elapsed = 0f;

            Debug.Log($"[ScreenFader] Starting fade from {startAmount} to {targetAmount}, duration: {duration}");

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled time for pause-safe fading
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth easing
                t = t * t * (3f - 2f * t); // Smoothstep

                ScreenFadePass.FadeAmount = Mathf.Lerp(startAmount, targetAmount, t);
                yield return null;
            }

            ScreenFadePass.FadeAmount = targetAmount;
            Debug.Log($"[ScreenFader] Fade complete, final amount: {ScreenFadePass.FadeAmount}");
            isFading = false;
            currentFadeCoroutine = null;
        }

        private IEnumerator DoWithFadeCoroutine(Action action, float fadeDuration)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : defaultFadeDuration;

            // Fade out
            yield return StartFade(1f, duration, null);

            // Execute action
            action?.Invoke();

            // Small delay while fully faded
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade in
            yield return StartFade(0f, duration, null);
        }

        #endregion
    }
}
