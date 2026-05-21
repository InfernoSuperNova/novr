using System;
using System.Diagnostics.CodeAnalysis;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_ANALYTICS && ENABLE_CLOUD_SERVICES_ANALYTICS
using UnityEngine.Analytics;
#endif

namespace UnityEditor.XR.OpenXR.Analytics
{
    internal static class AdditiveActionsAnalytics
    {
        private const string kVendorKey = "unity.xr.openxr";
        private const string kEventName = "xr_additive_playmode_usage";
        private const int kVersion = 1;
        private const int kMaxPerHour = 1000;
        private const int kMaxItems = 1000;

        [Serializable]
        private struct Payload
#if UNITY_2023_2_OR_NEWER && ENABLE_CLOUD_SERVICES_ANALYTICS && UNITY_ANALYTICS
            : IAnalytic.IData
#endif
        {
            public string additive_profile_name;
            public string[] augmented_profiles;
            public string[] additive_action_names;
        }

#if UNITY_2023_2_OR_NEWER && ENABLE_CLOUD_SERVICES_ANALYTICS && UNITY_ANALYTICS
        [AnalyticInfo(eventName: kEventName,
                      vendorKey: kVendorKey,
                      version: kVersion,
                      maxEventsPerHour: kMaxPerHour,
                      maxNumberOfElements: kMaxItems)]
        private class Event : IAnalytic
        {
            private Payload? m_Data;
            public Event(Payload data) { m_Data = data; }

            public bool TryGatherData(out IAnalytic.IData data, [NotNullWhen(false)] out Exception error)
            {
                error = null;
                data = m_Data.GetValueOrDefault();
                return m_Data.HasValue;
            }
        }
#endif

        public static void Send(string additiveProfileName, string[] augmentedProfiles, string[] additiveActionNames)
        {
#if UNITY_EDITOR && UNITY_ANALYTICS && ENABLE_CLOUD_SERVICES_ANALYTICS

            var payload = new Payload
            {
                additive_profile_name = additiveProfileName,
                augmented_profiles = augmentedProfiles,
                additive_action_names = additiveActionNames
            };

#if UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendAnalytic(new Event(payload));
#else
            EditorAnalytics.RegisterEventWithLimit(kEventName, kMaxPerHour, kMaxItems, kVendorKey);
            EditorAnalytics.SendEventWithLimit(kEventName, payload, kVersion);
#endif  // UNITY_2023_2_OR_NEWER
#endif  // UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS && UNITY_ANALYTICS

        }
    }
}
