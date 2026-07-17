using System;
using System.Collections;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOVR;

internal sealed class DiagnosticMissionAutoStart : MonoBehaviour
{
    private bool _started;

    private void Start()
    {
        StartCoroutine(StartMissionAfterDelay());
    }

    private IEnumerator StartMissionAfterDelay()
    {
        var config = ModConfiguration.Instance;
        if (!config.DiagnosticMissionAutoStart.Value)
        {
            yield break;
        }

        var delaySeconds = Math.Max(0f, config.DiagnosticMissionAutoStartDelaySeconds.Value);
        Debug.Log($"[NOVR] DiagnosticMissionAutoStart armed query='{config.DiagnosticMissionAutoStartQuery.Value}' delay={delaySeconds:0.00}s.");
        yield return new WaitForSecondsRealtime(delaySeconds);

        var deadline = Time.realtimeSinceStartup + 60f;
        while (!_started && Time.realtimeSinceStartup < deadline)
        {
            if (TryStartMission())
            {
                _started = true;
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        Debug.LogWarning("[NOVR] DiagnosticMissionAutoStart timed out before mission launch.");
    }

    private bool TryStartMission()
    {
        if (NetworkManagerNuclearOption.i == null)
        {
            return false;
        }

        try
        {
            MissionGroup.Init();
            var query = ModConfiguration.Instance.DiagnosticMissionAutoStartQuery.Value ?? "Furball";
            foreach (var selected in MissionSaveLoad.QuickLoadMany(MissionGroup.All.GetMissions()))
            {
                if (!Contains(selected.key.Name, query))
                {
                    continue;
                }

                if (!selected.key.TryLoad(out var mission, out var error))
                {
                    Debug.LogWarning($"[NOVR] DiagnosticMissionAutoStart load failed mission='{selected.key.Name}' error='{error}'.");
                    return false;
                }

                Debug.Log($"[NOVR] DiagnosticMissionAutoStart launching mission='{selected.key.Name}' map='{mission.MapKey}'.");
                MissionManager.SetMission(mission, checkIfSame: false);
                NetworkManagerNuclearOption.i.StartHost(
                    new HostOptions(SocketType.Offline, GameState.SinglePlayer, mission.MapKey));
                return true;
            }

            return false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] DiagnosticMissionAutoStart attempt failed: {exception}");
            return false;
        }
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
