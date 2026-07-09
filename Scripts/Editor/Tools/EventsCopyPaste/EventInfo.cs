using UnityEngine;
using UnityEditor;
using System;

namespace Michitai.Tools
{
    /// <summary>
    /// Data model representing information about a UnityEvent found during scanning.
    /// Stores the path, serialized property, and listener count for each discovered event.
    /// </summary>
    [Serializable]
    public class EventInfo
    {
        /// <summary>
        /// The display path to this event (e.g., "OnTriggerEnter" or "NestedClass/OnEvent")
        /// </summary>
        public string EventPath { get; set; }

        /// <summary>
        /// The SerializedProperty that represents this UnityEvent
        /// </summary>
        public SerializedProperty SerializedProperty { get; set; }

        /// <summary>
        /// The number of persistent listeners attached to this event
        /// </summary>
        public int ListenerCount { get; set; }

        /// <summary>
        /// The target MonoBehaviour that owns this event
        /// </summary>
        public MonoBehaviour TargetBehaviour { get; set; }

        /// <summary>
        /// The component name for display purposes (helps identify which component owns the event)
        /// </summary>
        public string ComponentName { get; set; }

        /// <summary>
        /// Constructor for EventInfo
        /// </summary>
        public EventInfo(string eventPath, SerializedProperty property, MonoBehaviour target)
        {
            EventPath = eventPath;
            SerializedProperty = property;
            TargetBehaviour = target;
            ComponentName = target != null ? target.GetType().Name : "Unknown";
            ListenerCount = CountPersistentListeners(property);
        }

        /// <summary>
        /// Counts the number of persistent listeners in a UnityEvent SerializedProperty
        /// </summary>
        private int CountPersistentListeners(SerializedProperty property)
        {
            if (property == null)
                return 0;

            // UnityEvent -> m_PersistentCalls -> m_Calls
            SerializedProperty calls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");

            if (calls == null || !calls.isArray)
                return 0;

            int count = 0;

            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty listener = calls.GetArrayElementAtIndex(i);
                if (listener == null)
                    continue;

                // Ignore empty listeners
                SerializedProperty target = listener.FindPropertyRelative("m_Target");
                SerializedProperty methodName = listener.FindPropertyRelative("m_MethodName");

                if (target != null &&
                    target.objectReferenceValue != null &&
                    methodName != null &&
                    !string.IsNullOrEmpty(methodName.stringValue))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Returns a display string showing the event path and listener count
        /// </summary>
        public string GetDisplayString()
        {
            return $"{ComponentName}.{EventPath} ({ListenerCount} listener{(ListenerCount != 1 ? "s" : "")})";
        }

        /// <summary>
        /// Returns whether this event has any listeners
        /// </summary>
        public bool HasListeners()
        {
            return ListenerCount > 0;
        }

        /// <summary>
        /// Checks if this event matches another event by name (ignoring listener count)
        /// </summary>
        public bool MatchesName(EventInfo other)
        {
            if (other == null)
                return false;
            
            // Compare just the event name (last part of path)
            string thisName = GetEventName();
            string otherName = other.GetEventName();
            
            return string.Equals(thisName, otherName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts just the event name from the full path
        /// </summary>
        private string GetEventName()
        {
            if (string.IsNullOrEmpty(EventPath))
                return "";
            
            int lastSlash = EventPath.LastIndexOf('/');
            return lastSlash >= 0 ? EventPath.Substring(lastSlash + 1) : EventPath;
        }
    }
}
