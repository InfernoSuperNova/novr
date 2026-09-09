using UnityEngine;

namespace NOVR.VrUi.Native;

public sealed class MenuEnvironmentHangarDoorAnimator : MonoBehaviour
{
    private Vector3 _closedLocalPosition;
    private Quaternion _closedLocalRotation;
    private Vector3 _openLocalOffset;
    private float _openDelaySeconds;
    private float _openDurationSeconds = 1f;
    private float _elapsedSeconds;
    private bool _hasClosedTransform;
    private bool _complete;

    public void Configure(Vector3 openLocalOffset, float openDelaySeconds, float openDurationSeconds)
    {
        if (!_hasClosedTransform)
        {
            CaptureClosedTransform();
        }

        _openLocalOffset = openLocalOffset;
        _openDelaySeconds = Mathf.Max(0f, openDelaySeconds);
        _openDurationSeconds = Mathf.Max(0.01f, openDurationSeconds);
        Restart();
    }

    private void Awake()
    {
        CaptureClosedTransform();
    }

    private void OnEnable()
    {
        Restart();
    }

    private void Update()
    {
        if (_complete) return;

        _elapsedSeconds += Time.deltaTime;

        var openTime = _elapsedSeconds - _openDelaySeconds;
        var openAmount = Mathf.Clamp01(openTime / _openDurationSeconds);
        var easedOpenAmount = openAmount * openAmount * (3f - 2f * openAmount);

        transform.localPosition = Vector3.Lerp(_closedLocalPosition, _closedLocalPosition + _openLocalOffset, easedOpenAmount);
        transform.localRotation = _closedLocalRotation;

        if (openAmount >= 1f)
        {
            _complete = true;
        }
    }

    private void CaptureClosedTransform()
    {
        _closedLocalPosition = transform.localPosition;
        _closedLocalRotation = transform.localRotation;
        _hasClosedTransform = true;
    }

    private void Restart()
    {
        if (!_hasClosedTransform)
        {
            CaptureClosedTransform();
        }

        _elapsedSeconds = 0f;
        _complete = false;
        transform.localPosition = _closedLocalPosition;
        transform.localRotation = _closedLocalRotation;
    }
}
