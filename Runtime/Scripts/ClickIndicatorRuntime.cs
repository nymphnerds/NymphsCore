using UnityEngine;

namespace Nymphs_TDC.Scripts
{
    public sealed class ClickIndicatorRuntime : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private LineRenderer _line;
        private Material _material;
        private Color _baseColor;
        private float _duration;
        private float _startScale;
        private float _endScale;
        private float _time;
        private bool _pulseUntilArrived;
        private float _pulseSpeed;
        private int _pulseRepeatCount;

        public void Initialize(LineRenderer line, Color color, float duration, float startScale, float endScale, bool pulseUntilArrived, float pulseSpeed)
        {
            _line = line != null ? line : GetComponent<LineRenderer>();
            _renderer = GetComponent<MeshRenderer>();
            _baseColor = color;
            _duration = Mathf.Max(0f, duration);
            _startScale = Mathf.Max(0.001f, startScale);
            _endScale = Mathf.Max(0.001f, endScale);
            _pulseUntilArrived = pulseUntilArrived;
            _pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
            _pulseRepeatCount = 0;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _material = new Material(shader);
                _material.color = _baseColor;
            }

            if (_line != null)
            {
                _line.material = _material;
                _line.startColor = _baseColor;
                _line.endColor = _baseColor;
            }
            else if (_renderer != null)
            {
                _renderer.material = _material;
            }

            transform.localScale = new Vector3(_startScale, _startScale, _startScale);
        }

        public void SetPulseRepeatCount(int pulseRepeatCount)
        {
            _pulseRepeatCount = Mathf.Max(0, pulseRepeatCount);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float lifeT = _duration > 0f ? Mathf.Clamp01(_time / _duration) : 1f;

            bool pulseRequested = _pulseSpeed > 0f && (_pulseUntilArrived || _duration > 0f || _pulseRepeatCount > 0);
            float pulseT = 1f;
            if (pulseRequested)
            {
                if (_pulseRepeatCount > 0)
                {
                    float maxTime = _pulseRepeatCount / _pulseSpeed;
                    if (_time < maxTime)
                    {
                        pulseT = 0.5f + 0.5f * Mathf.Sin(_time * _pulseSpeed * Mathf.PI * 2f);
                    }
                    else
                    {
                        pulseRequested = false;
                    }
                }
                else
                {
                    pulseT = 0.5f + 0.5f * Mathf.Sin(_time * _pulseSpeed * Mathf.PI * 2f);
                }
            }

            float scaleT = pulseRequested ? pulseT : (_pulseUntilArrived ? 1f : lifeT);
            float scale = Mathf.Lerp(_startScale, _endScale, scaleT);
            transform.localScale = new Vector3(scale, scale, scale);

            if (_material != null)
            {
                Color c = _baseColor;
                c.a = _pulseUntilArrived ? _baseColor.a : (_duration > 0f ? Mathf.Lerp(_baseColor.a, 0f, lifeT) : _baseColor.a);
                _material.color = c;
                if (_line != null)
                {
                    _line.startColor = c;
                    _line.endColor = c;
                }
            }

            if (!_pulseUntilArrived)
            {
                if (_duration > 0f)
                {
                    if (_time >= _duration) Destroy(gameObject);
                }
                else if (_pulseRepeatCount > 0)
                {
                    float maxTime = _pulseRepeatCount / _pulseSpeed;
                    if (_time >= maxTime) Destroy(gameObject);
                }
            }
        }
    }
}
