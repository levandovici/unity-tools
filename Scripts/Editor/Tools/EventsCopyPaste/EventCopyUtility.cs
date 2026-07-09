using UnityEngine;
using UnityEditor;
using System;

namespace Michitai.Tools
{
    /// <summary>
    /// Handles the actual copying of persistent listeners between UnityEvents.
    /// Supports both Replace and Append modes, with full Undo support.
    /// </summary>
    public class EventCopyUtility
    {
        /// <summary>
        /// Copy mode: Replace existing listeners or append to them
        /// </summary>
        public enum CopyMode
        {
            Replace,  // Clear target listeners before copying
            Append    // Add source listeners to existing target listeners
        }

        /// <summary>
        /// Copies persistent listeners from source event to target event
        /// </summary>
        /// <param name="sourceEvent">The source EventInfo</param>
        /// <param name="targetEvent">The target EventInfo</param>
        /// <param name="mode">Whether to replace or append listeners</param>
        /// <param name="registerUndo">Whether to register an undo operation</param>
        public static void CopyEventListeners(EventInfo sourceEvent, EventInfo targetEvent, CopyMode mode, bool registerUndo = true)
        {
            if (sourceEvent == null || targetEvent == null)
                return;

            if (sourceEvent.SerializedProperty == null || targetEvent.SerializedProperty == null)
                return;

            SerializedProperty sourceProperty = sourceEvent.SerializedProperty;
            SerializedProperty targetProperty = targetEvent.SerializedProperty;

            if (registerUndo)
            {
                // Register undo for the target object
                Undo.RegisterCompleteObjectUndo(targetEvent.TargetBehaviour, "Copy UnityEvent Listeners");
            }

            // Apply the copy based on mode
            if (mode == CopyMode.Replace)
            {
                ReplaceListeners(sourceProperty, targetProperty);
            }
            else
            {
                AppendListeners(sourceProperty, targetProperty);
            }

            // Mark the target object as dirty
            EditorUtility.SetDirty(targetEvent.TargetBehaviour);

            // Apply changes
            if (targetProperty.serializedObject != null)
            {
                targetProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Replaces all listeners in target with listeners from source
        /// </summary>
        private static void ReplaceListeners(SerializedProperty source, SerializedProperty target)
        {
            if (source == null || target == null)
                return;

            // UnityEvent -> m_PersistentCalls -> m_Calls
            SerializedProperty sourceCalls = source.FindPropertyRelative("m_PersistentCalls.m_Calls");
            SerializedProperty targetCalls = target.FindPropertyRelative("m_PersistentCalls.m_Calls");

            if (sourceCalls == null || targetCalls == null ||
                !sourceCalls.isArray || !targetCalls.isArray)
                return;

            // Clear target listeners
            targetCalls.ClearArray();

            // Copy each listener
            for (int i = 0; i < sourceCalls.arraySize; i++)
            {
                SerializedProperty sourceListener = sourceCalls.GetArrayElementAtIndex(i);

                targetCalls.InsertArrayElementAtIndex(targetCalls.arraySize);

                SerializedProperty targetListener = targetCalls.GetArrayElementAtIndex(targetCalls.arraySize - 1);

                CopyListenerProperties(sourceListener, targetListener);
            }
        }

        /// <summary>
        /// Appends listeners from source to target (keeping existing target listeners)
        /// </summary>
        private static void AppendListeners(SerializedProperty source, SerializedProperty target)
        {
            if (source == null || target == null)
                return;

            // UnityEvent -> m_PersistentCalls -> m_Calls
            SerializedProperty sourceCalls = source.FindPropertyRelative("m_PersistentCalls.m_Calls");
            SerializedProperty targetCalls = target.FindPropertyRelative("m_PersistentCalls.m_Calls");

            if (sourceCalls == null || targetCalls == null ||
                !sourceCalls.isArray || !targetCalls.isArray)
                return;

            // Append each listener from source to target
            for (int i = 0; i < sourceCalls.arraySize; i++)
            {
                SerializedProperty sourceListener = sourceCalls.GetArrayElementAtIndex(i);

                targetCalls.InsertArrayElementAtIndex(targetCalls.arraySize);
                SerializedProperty targetListener = targetCalls.GetArrayElementAtIndex(targetCalls.arraySize - 1);

                CopyListenerProperties(sourceListener, targetListener);
            }
        }

        /// <summary>
        /// Copies all properties of a single listener from source to target
        /// </summary>
        private static void CopyListenerProperties(SerializedProperty sourceListener, SerializedProperty targetListener)
        {
            // Copy m_Mode (PersistentListenerMode)
            SerializedProperty sourceMode = sourceListener.FindPropertyRelative("m_Mode");
            SerializedProperty targetMode = targetListener.FindPropertyRelative("m_Mode");
            if (sourceMode != null && targetMode != null)
            {
                targetMode.intValue = sourceMode.intValue;
            }

            // Copy m_Target (the target object)
            SerializedProperty sourceTarget = sourceListener.FindPropertyRelative("m_Target");
            SerializedProperty targetTarget = targetListener.FindPropertyRelative("m_Target");
            if (sourceTarget != null && targetTarget != null)
            {
                targetTarget.objectReferenceValue = sourceTarget.objectReferenceValue;
            }

            // Copy m_MethodName (the method to call)
            SerializedProperty sourceMethodName = sourceListener.FindPropertyRelative("m_MethodName");
            SerializedProperty targetMethodName = targetListener.FindPropertyRelative("m_MethodName");
            if (sourceMethodName != null && targetMethodName != null)
            {
                targetMethodName.stringValue = sourceMethodName.stringValue;
            }

            // Copy m_Arguments (the arguments for the method call)
            SerializedProperty sourceArguments = sourceListener.FindPropertyRelative("m_Arguments");
            SerializedProperty targetArguments = targetListener.FindPropertyRelative("m_Arguments");
            if (sourceArguments != null && targetArguments != null)
            {
                CopyArguments(sourceArguments, targetArguments);
            }

            // Copy m_CallState (whether the listener is enabled)
            SerializedProperty sourceCallState = sourceListener.FindPropertyRelative("m_CallState");
            SerializedProperty targetCallState = targetListener.FindPropertyRelative("m_CallState");
            if (sourceCallState != null && targetCallState != null)
            {
                targetCallState.intValue = sourceCallState.intValue;
            }
        }

        /// <summary>
        /// Copies the arguments (m_Arguments) from source to target
        /// </summary>
        private static void CopyArguments(SerializedProperty sourceArguments, SerializedProperty targetArguments)
        {
            // Copy argument type
            SerializedProperty sourceArgumentType = sourceArguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
            SerializedProperty targetArgumentType = targetArguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
            if (sourceArgumentType != null && targetArgumentType != null)
            {
                targetArgumentType.stringValue = sourceArgumentType.stringValue;
            }

            // Copy object argument
            SerializedProperty sourceObjectArg = sourceArguments.FindPropertyRelative("m_ObjectArgument");
            SerializedProperty targetObjectArg = targetArguments.FindPropertyRelative("m_ObjectArgument");
            if (sourceObjectArg != null && targetObjectArg != null)
            {
                targetObjectArg.objectReferenceValue = sourceObjectArg.objectReferenceValue;
            }

            // Copy int argument
            SerializedProperty sourceIntArg = sourceArguments.FindPropertyRelative("m_IntArgument");
            SerializedProperty targetIntArg = targetArguments.FindPropertyRelative("m_IntArgument");
            if (sourceIntArg != null && targetIntArg != null)
            {
                targetIntArg.intValue = sourceIntArg.intValue;
            }

            // Copy float argument
            SerializedProperty sourceFloatArg = sourceArguments.FindPropertyRelative("m_FloatArgument");
            SerializedProperty targetFloatArg = targetArguments.FindPropertyRelative("m_FloatArgument");
            if (sourceFloatArg != null && targetFloatArg != null)
            {
                targetFloatArg.floatValue = sourceFloatArg.floatValue;
            }

            // Copy string argument
            SerializedProperty sourceStringArg = sourceArguments.FindPropertyRelative("m_StringArgument");
            SerializedProperty targetStringArg = targetArguments.FindPropertyRelative("m_StringArgument");
            if (sourceStringArg != null && targetStringArg != null)
            {
                targetStringArg.stringValue = sourceStringArg.stringValue;
            }

            // Copy bool argument
            SerializedProperty sourceBoolArg = sourceArguments.FindPropertyRelative("m_BoolArgument");
            SerializedProperty targetBoolArg = targetArguments.FindPropertyRelative("m_BoolArgument");
            if (sourceBoolArg != null && targetBoolArg != null)
            {
                targetBoolArg.boolValue = sourceBoolArg.boolValue;
            }
        }

        /// <summary>
        /// Copies all matching events from source behaviour to target behaviour
        /// Events are matched by name (case-insensitive)
        /// </summary>
        /// <param name="sourceEvents">List of events from source behaviour</param>
        /// <param name="targetEvents">List of events from target behaviour</param>
        /// <param name="mode">Copy mode (Replace or Append)</param>
        /// <returns>Number of events copied</returns>
        public static int CopyAllMatchingEvents(System.Collections.Generic.List<EventInfo> sourceEvents, 
                                                 System.Collections.Generic.List<EventInfo> targetEvents, 
                                                 CopyMode mode)
        {
            int copyCount = 0;

            foreach (var sourceEvent in sourceEvents)
            {
                // Find matching target event by name
                EventInfo matchingTarget = targetEvents.Find(e => e.MatchesName(sourceEvent));

                if (matchingTarget != null)
                {
                    CopyEventListeners(sourceEvent, matchingTarget, mode);
                    copyCount++;
                }
            }

            return copyCount;
        }

        /// <summary>
        /// Clears all listeners from a UnityEvent
        /// </summary>
        /// <param name="targetEvent">The event to clear</param>
        /// <param name="registerUndo">Whether to register an undo operation</param>
        public static void ClearEventListeners(EventInfo targetEvent, bool registerUndo = true)
        {
            if (targetEvent == null || targetEvent.SerializedProperty == null)
                return;

            if (registerUndo)
            {
                Undo.RegisterCompleteObjectUndo(targetEvent.TargetBehaviour, "Clear UnityEvent Listeners");
            }

            SerializedProperty calls = targetEvent.SerializedProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");

            if (calls != null && calls.isArray)
            {
                calls.ClearArray();
            }

            SerializedObject serializedObject = targetEvent.SerializedProperty.serializedObject;
            if (serializedObject != null)
            {
                serializedObject.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(targetEvent.TargetBehaviour);
        }

        /// <summary>
        /// Validates if a copy operation can be performed
        /// </summary>
        public static bool CanCopy(EventInfo sourceEvent, EventInfo targetEvent)
        {
            if (sourceEvent == null || targetEvent == null)
                return false;

            if (sourceEvent.SerializedProperty == null || targetEvent.SerializedProperty == null)
                return false;

            if (sourceEvent.ListenerCount == 0)
                return false;

            return true;
        }
    }
}
