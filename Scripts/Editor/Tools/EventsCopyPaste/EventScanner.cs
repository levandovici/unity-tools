// Updated EventScanner.cs - Improved detection for Button.OnClick, private events, and Inspector-visible events only
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Reflection;

namespace Michitai.Tools
{
    /// <summary>
    /// Improved scanner that only finds UnityEvents visible in the Inspector.
    /// Better support for private [SerializeField] events and UI components like Button.m_OnClick.
    /// </summary>
    public class EventScanner
    {
        public static List<EventInfo> ScanForEvents(MonoBehaviour behaviour)
        {
            return ScanForEventsWithReflection(behaviour);
        }

        private static bool IsInspectorVisibleField(FieldInfo field)
        {
            if (field == null) return false;
            if (field.IsStatic) return false;

            // Must be serialized to appear in Inspector
            bool isPublic = field.IsPublic;
            bool hasSerializeField = Attribute.IsDefined(field, typeof(SerializeField));
            bool hasHideInInspector = Attribute.IsDefined(field, typeof(HideInInspector));

            return (isPublic || hasSerializeField) && !hasHideInInspector;
        }

        private static void ScanAllFields(Type type, MonoBehaviour behaviour, SerializedObject serializedObject,
                                        string path, List<EventInfo> events)
        {
            if (type == null ||
                type == typeof(MonoBehaviour) ||
                type == typeof(Behaviour) ||
                type == typeof(Component) ||
                type == typeof(UnityEngine.Object))
                return;

            // Don't stop at UIBehaviour - we want its events
            var fields = type.GetFields(BindingFlags.Public |
                                       BindingFlags.NonPublic |
                                       BindingFlags.Instance |
                                       BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (!IsInspectorVisibleField(field)) continue;

                string fieldPath = string.IsNullOrEmpty(path) ? field.Name : path + "/" + field.Name;

                if (IsUnityEventType(field.FieldType))
                {
                    SerializedProperty prop = serializedObject.FindProperty(fieldPath);

                    if (prop != null)
                    {
                        events.Add(new EventInfo(fieldPath, prop, behaviour));
                    }
                }
            }

            ScanAllFields(type.BaseType, behaviour, serializedObject, path, events);
        }

        private static bool IsUnityEventType(Type type)
        {
            if (type == null) return false;

            if (type == typeof(UnityEventBase) || type.IsSubclassOf(typeof(UnityEventBase)))
            {
                return true;
            }

            return false;
        }

        private static void RemoveDuplicateEvents(List<EventInfo> events)
        {
            var seen = new HashSet<string>();
            var unique = new List<EventInfo>();

            foreach (var e in events)
            {
                if (string.IsNullOrEmpty(e.EventPath)) continue;
                if (seen.Add(e.EventPath))
                    unique.Add(e);
            }

            events.Clear();
            events.AddRange(unique);
        }

        public static List<EventInfo> ScanForEventsWithReflection(MonoBehaviour behaviour)
        {
            var events = new List<EventInfo>();
            if (behaviour == null) return events;

            var serializedObject = new SerializedObject(behaviour);

            ScanAllFields(behaviour.GetType(), behaviour, serializedObject, "", events);

            //RemoveDuplicateEvents(events);
            return events;
        }

        public static string GetBehaviourDisplayName(MonoBehaviour behaviour)
        {
            if (behaviour == null) return "None";
            return $"{behaviour.name} ({behaviour.GetType().Name})";
        }
    }
}
