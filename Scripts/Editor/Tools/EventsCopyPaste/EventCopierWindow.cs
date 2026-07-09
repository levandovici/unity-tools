using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Michitai.Tools
{
    /// <summary>
    /// Editor Window for copying UnityEvent listeners between MonoBehaviours.
    /// Features drag & drop support, recursive event scanning, search/filter, and batch operations.
    /// Now supports Unity UI components (Button, Toggle, Slider, etc.).
    /// </summary>
    public class EventCopierWindow : EditorWindow
    {
        [MenuItem("Tools/Events Copy Paste")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventCopierWindow>("Events Copy Paste");
            window.minSize = new Vector2(500, 800);
        }

        // Source and target gameobjects/behaviours
        private GameObject sourceGameObject;
        private GameObject targetGameObject;

        // Scanned events
        private List<EventInfo> sourceEvents = new List<EventInfo>();
        private List<EventInfo> targetEvents = new List<EventInfo>();

        // UI state
        private int sourceEventIndex = 0;
        private int targetEventIndex = 0;
        private string[] sourceEventNames;
        private string[] targetEventNames;

        // Copy mode
        private EventCopyUtility.CopyMode copyMode = EventCopyUtility.CopyMode.Replace;

        // Scroll positions
        private Vector2 sourceScrollPosition;
        private Vector2 targetScrollPosition;

        // UI state flags
        private bool showAdvancedOptions = false;

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Events Copy Paste", titleStyle);
            EditorGUILayout.Space(10);

            // Source Behaviour Section
            DrawBehaviourSection(ref sourceGameObject, "Source Behaviour", ref sourceScrollPosition, 
                                sourceEvents, ref sourceEventIndex, ref sourceEventNames, true);

            EditorGUILayout.Space(5);

            // Clear source button
            EditorGUI.BeginDisabledGroup(sourceGameObject == null);
            if (GUILayout.Button("Clear Source"))
            {
                sourceGameObject = null;
                sourceEvents.Clear();
                sourceEventNames = new string[0];
                sourceEventIndex = 0;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(15);

            // Target Behaviour Section
            DrawBehaviourSection(ref targetGameObject, "Target Behaviour", ref targetScrollPosition, 
                                targetEvents, ref targetEventIndex, ref targetEventNames, false);

            EditorGUILayout.Space(5);

            // Clear target button
            EditorGUI.BeginDisabledGroup(targetGameObject == null);
            if (GUILayout.Button("Clear Target"))
            {
                targetGameObject = null;
                targetEvents.Clear();
                targetEventNames = new string[0];
                targetEventIndex = 0;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(15);

            // Copy Mode Selection
            DrawCopyModeSection();

            EditorGUILayout.Space(15);

            // Action Buttons
            DrawActionButtons();

            EditorGUILayout.Space(10);

            // Advanced Options
            DrawAdvancedOptions();

            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// Draws a behaviour section (source or target) with drag & drop and event list
        /// </summary>
        private void DrawBehaviourSection(ref GameObject gameObject, string label, 
                                          ref Vector2 scrollPosition, List<EventInfo> events,
                                          ref int eventIndex, ref string[] eventNames, bool isSource)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Label
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Drag & Drop Area
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop GameObject or Component here", EditorStyles.textArea);

            HandleDragAndDrop(dropArea, ref gameObject, isSource);

            // Display current selection
            if (gameObject != null)
            {
                int componentCount = gameObject.GetComponents<MonoBehaviour>().Length;
                EditorGUILayout.LabelField($"Selected: {gameObject.name} ({componentCount} component{(componentCount != 1 ? "s" : "")})", 
                                         EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

            // Event dropdown
            if (gameObject != null && events.Count > 0)
            {
                EditorGUILayout.LabelField("Events:", EditorStyles.miniBoldLabel);
                
                if (eventNames == null)
                {
                    UpdateEventNames(events, ref eventNames);
                }

                int newIndex = EditorGUILayout.Popup(eventIndex, eventNames);
                if (newIndex != eventIndex)
                {
                    eventIndex = newIndex;
                }

                // Show listener count for selected event
                if (eventIndex >= 0 && eventIndex < events.Count)
                {
                    var selectedEvent = events[eventIndex];
                    EditorGUILayout.LabelField($"Listeners: {selectedEvent.ListenerCount}", EditorStyles.miniLabel);
                }
            }
            else if (gameObject != null)
            {
                EditorGUILayout.LabelField($"No UnityEvents found on any component (scanned {events.Count} events total).", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Handles drag and drop for GameObject/Component assignment
        /// </summary>
        private void HandleDragAndDrop(Rect dropArea, ref GameObject gameObject, bool isSource)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                if (dropArea.Contains(currentEvent.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (currentEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is GameObject draggedGameObject)
                            {
                                gameObject = draggedGameObject;
                                ScanGameObject(gameObject, isSource);
                                break;
                            }
                            else if (draggedObject is MonoBehaviour monoBehaviour)
                            {
                                // If a component is dropped, use its GameObject
                                gameObject = monoBehaviour.gameObject;
                                ScanGameObject(gameObject, isSource);
                                break;
                            }
                        }

                        currentEvent.Use();
                    }
                }
            }
        }

        /// <summary>
        /// Scans a GameObject and all its attached MonoBehaviours for UnityEvents
        /// </summary>
        private void ScanGameObject(GameObject gameObject, bool isSource)
        {
            if (gameObject == null)
                return;

            Debug.Log($"Scanning GameObject: {gameObject.name} for {(isSource ? "source" : "target")}");

            // Get all MonoBehaviours on the GameObject
            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            List<EventInfo> allEvents = new List<EventInfo>();

            Debug.Log($"Found {components.Length} components on {gameObject.name}");

            // Scan each component for UnityEvents
            foreach (var component in components)
            {
                if (component != null)
                {
                    Debug.Log($"Scanning component: {component.GetType().Name}");
                    List<EventInfo> componentEvents = EventScanner.ScanForEvents(component);
                    Debug.Log($"Found {componentEvents.Count} events on {component.GetType().Name}");
                    allEvents.AddRange(componentEvents);
                }
            }

            Debug.Log($"Total events found: {allEvents.Count}");

            if (isSource)
            {
                sourceEvents = allEvents;
                sourceEventIndex = 0;
                UpdateEventNames(sourceEvents, ref sourceEventNames);
            }
            else
            {
                targetEvents = allEvents;
                targetEventIndex = 0;
                UpdateEventNames(targetEvents, ref targetEventNames);
            }
        }

        /// <summary>
        /// Updates the display names array for dropdown
        /// </summary>
        private void UpdateEventNames(List<EventInfo> events, ref string[] eventNames)
        {
            eventNames = events.Select(e => e.GetDisplayString()).ToArray();
        }

        /// <summary>
        /// Draws the copy mode selection section
        /// </summary>
        private void DrawCopyModeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Copy Mode", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            GUIContent modeLabel = new GUIContent("Mode", "Replace: Clear target listeners before copying\nAppend: Add to existing listeners");
            EventCopyUtility.CopyMode newMode = (EventCopyUtility.CopyMode)EditorGUILayout.EnumPopup(
                modeLabel, 
                copyMode
            );

            if (newMode != copyMode)
            {
                copyMode = newMode;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the action buttons section
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(!CanCopySingle());
            if (GUILayout.Button("Copy Selected Event", GUILayout.Height(30)))
            {
                CopySelectedEvent();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws advanced options section
        /// </summary>
        private void DrawAdvancedOptions()
        {
            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options");

            if (showAdvancedOptions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.Space(5);

                // Clear selected target event listeners
                EditorGUI.BeginDisabledGroup(targetGameObject == null || targetEvents.Count == 0);
                if (GUILayout.Button("Clear Selected Target Event Listeners"))
                {
                    if (targetEventIndex >= 0 && targetEventIndex < targetEvents.Count)
                    {
                        EventCopyUtility.ClearEventListeners(targetEvents[targetEventIndex]);
                        // Refresh target events
                        ScanGameObject(targetGameObject, false);
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                // Refresh source button
                EditorGUI.BeginDisabledGroup(sourceGameObject == null);
                if (GUILayout.Button("Refresh Source Events"))
                {
                    ScanGameObject(sourceGameObject, true);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                // Refresh target button
                EditorGUI.BeginDisabledGroup(targetGameObject == null);
                if (GUILayout.Button("Refresh Target Events"))
                {
                    ScanGameObject(targetGameObject, false);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// Checks if a single event copy can be performed
        /// </summary>
        private bool CanCopySingle()
        {
            if (sourceGameObject == null || targetGameObject == null)
                return false;

            if (sourceEvents.Count == 0 || targetEvents.Count == 0)
                return false;

            if (sourceEventIndex < 0 || sourceEventIndex >= sourceEvents.Count)
                return false;

            if (targetEventIndex < 0 || targetEventIndex >= targetEvents.Count)
                return false;

            var sourceEvent = sourceEvents[sourceEventIndex];
            var targetEvent = targetEvents[targetEventIndex];

            return EventCopyUtility.CanCopy(sourceEvent, targetEvent);
        }

        /// <summary>
        /// Copies the selected event from source to target
        /// </summary>
        private void CopySelectedEvent()
        {
            if (!CanCopySingle())
                return;

            var sourceEvent = sourceEvents[sourceEventIndex];
            var targetEvent = targetEvents[targetEventIndex];

            EventCopyUtility.CopyEventListeners(sourceEvent, targetEvent, copyMode);

            Debug.Log($"Copied listeners from '{sourceEvent.ComponentName}.{sourceEvent.EventPath}' to '{targetEvent.ComponentName}.{targetEvent.EventPath}' in {copyMode} mode.");

            // Refresh target events to show updated listener counts
            ScanGameObject(targetGameObject, false);
        }

        /// <summary>
        /// Called when the window is destroyed to clean up resources
        /// </summary>
        private void OnDestroy()
        {
            sourceGameObject = null;
            targetGameObject = null;
            sourceEvents.Clear();
            targetEvents.Clear();
        }
    }
}
