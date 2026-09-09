using UnityEngine;

namespace NOVR.VrUi
{
    public class OneEuroFilter
    {
        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _dcCutoff;
        private float _prevValue;
        private float _prevDx;
        private bool _initialized;

        public OneEuroFilter(float minCutoff = 0.8f, float beta = 0.1f, float dcCutoff = 1f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dcCutoff = dcCutoff;
        }

        public void Reset(float value)
        {
            _prevValue = value;
            _prevDx = 0f;
            _initialized = true;
        }

        public float Filter(float x, float dt)
        {
            if (!_initialized || dt <= 0f)
            {
                _prevValue = x;
                _prevDx = 0f;
                _initialized = true;
                return x;
            }

            float dx = (x - _prevValue) / dt;
            float ad = SmoothingFactor(dt, _dcCutoff);
            float dxHat = ad * dx + (1f - ad) * _prevDx;
            float cutoff = _minCutoff + _beta * Mathf.Abs(dxHat);
            float a = SmoothingFactor(dt, cutoff);
            float xHat = a * x + (1f - a) * _prevValue;

            _prevValue = xHat;
            _prevDx = dxHat;
            return xHat;
        }

        private static float SmoothingFactor(float dt, float cutoff)
        {
            float r = 2f * Mathf.PI * cutoff * dt;
            return r / (r + 1f);
        }
    }

    public class OneEuroVector3Filter
    {
        private readonly OneEuroFilter _xF;
        private readonly OneEuroFilter _yF;
        private readonly OneEuroFilter _zF;

        public OneEuroVector3Filter(float minCutoff = 0.8f, float beta = 0.1f, float dcCutoff = 1f)
        {
            _xF = new OneEuroFilter(minCutoff, beta, dcCutoff);
            _yF = new OneEuroFilter(minCutoff, beta, dcCutoff);
            _zF = new OneEuroFilter(minCutoff, beta, dcCutoff);
        }

        public void Reset(Vector3 value)
        {
            _xF.Reset(value.x);
            _yF.Reset(value.y);
            _zF.Reset(value.z);
        }

        public Vector3 Filter(Vector3 v, float dt)
        {
            return new Vector3(
                _xF.Filter(v.x, dt),
                _yF.Filter(v.y, dt),
                _zF.Filter(v.z, dt)
            );
        }
    }

    public class OneEuroQuaternionFilter
    {
        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _dcCutoff;
        private Quaternion _prevValue;
        private float _prevDx;
        private bool _initialized;

        public OneEuroQuaternionFilter(float minCutoff = 0.8f, float beta = 0.1f, float dcCutoff = 1f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dcCutoff = dcCutoff;
        }

        public void Reset(Quaternion value)
        {
            _prevValue = value;
            _prevDx = 0f;
            _initialized = true;
        }

        public Quaternion Filter(Quaternion q, float dt)
        {
            if (!_initialized || dt <= 0f)
            {
                _prevValue = q;
                _prevDx = 0f;
                _initialized = true;
                return q;
            }

            float angleDeg = Quaternion.Angle(_prevValue, q);
            float angularSpeed = angleDeg * Mathf.Deg2Rad / dt;

            float ad = SmoothingFactor(dt, _dcCutoff);
            float dxHat = ad * angularSpeed + (1f - ad) * _prevDx;

            float cutoff = _minCutoff + _beta * dxHat;
            float a = SmoothingFactor(dt, cutoff);

            _prevValue = Quaternion.Slerp(_prevValue, q, a);
            _prevDx = dxHat;
            return _prevValue;
        }

        private static float SmoothingFactor(float dt, float cutoff)
        {
            float r = 2f * Mathf.PI * cutoff * dt;
            return r / (r + 1f);
        }
    }
}
