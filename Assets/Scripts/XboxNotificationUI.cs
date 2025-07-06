using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace LowerLevel.Notifications
{
    // Enums must be outside the class for UdonSharp compatibility
    public enum NotificationType
    {
        Achievement,
        Online,
        FirstTime,
        Supporter,
        Pwnerer
    }

    public enum NotificationState
    {
        Inactive,
        FadingIn,
        Blinking,
        FadingOut,
        QueueDelay
    }

    /// <summary>
    /// COMPONENT PURPOSE:
    /// Xbox 360 style achievement notification system with queue management for multiple simultaneous notifications
    /// Uses frame-based timing for reliable execution and smooth queue processing
    /// 
    /// LOWER LEVEL 2.0 INTEGRATION:
    /// Handles achievement unlocks, online notifications, and supporter recognition with authentic Xbox styling
    /// Queues multiple notifications to avoid overlap and ensures all achievements are properly displayed
    /// 
    /// DEPENDENCIES & REQUIREMENTS:
    /// - UI Canvas in World Space mode with proper VRC_UIShape component
    /// - Xbox and Trophy icon sprites positioned identically (overlapping)
    /// - TextMeshPro components for notification text
    /// - CanvasGroup component on NotificationBackground for fade effects
    /// - Audio clips for different notification types (achievement, online, supporter)
    /// 
    /// SIMPLIFIED DESIGN:
    /// - Handles up to 10 queued notifications (configurable)
    /// - Automatic priority system (Pwnerer > Supporter > Regular > Online)
    /// - Smooth transitions between queued notifications
    /// - Array-based storage for UdonSharp compatibility
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class XboxNotificationUI : UdonSharpBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject notificationBackground;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject xboxIcon;
        [SerializeField] private GameObject trophyIcon;
        [SerializeField] private TextMeshProUGUI mainText;
        [SerializeField] private TextMeshProUGUI subText;

        [Header("Audio System")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip achievementSound;
        [SerializeField] private AudioClip onlineSound;
        [SerializeField] private AudioClip supporterSound;
        [SerializeField] private AudioClip pwnererSound;

        [Header("Queue Configuration")]
        [SerializeField] private int maxQueueSize = 10;
        [SerializeField] private float displayDuration = 4.0f;
        [SerializeField] private float blinkInterval = 0.5f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float queueDelay = 0.2f;

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool enableQueueDebug = true;
        [SerializeField] private bool enableVerboseLogging = true;
        [SerializeField] private bool enableTimingDebug = true;
        [SerializeField] private bool enableStateDebug = true;

        // Queue Management - Using separate arrays for UdonSharp compatibility
        private NotificationType[] queueTypes;
        private string[] queuePlayerNames;
        private string[] queueAchievementTitles;
        private int[] queuePoints;
        private int[] queuePriorities;
        private float[] queueTimestamps;
        private int queueCount = 0;

        // Current Notification State
        private bool isDisplayingNotification = false;
        private bool isBlinking = false;
        private bool showingXboxIcon = true;
        private NotificationState currentState = NotificationState.Inactive;

        // Timing Variables
        private float stateTimer = 0f;
        private float blinkTimer = 0f;
        private float currentAlpha = 0f;

        // Component References
        private bool isInitialized = false;
        
        void Start()
        {
            InitializeComponent();

            // DEBUG: immediately queue a test notification
            QueueAchievementNotification("DEBUG_PLAYER", "Debug Unlock", 10);
        }
        void InitializeComponent()
        {
            // Initialize queue arrays
            queueTypes = new NotificationType[maxQueueSize];
            queuePlayerNames = new string[maxQueueSize];
            queueAchievementTitles = new string[maxQueueSize];
            queuePoints = new int[maxQueueSize];
            queuePriorities = new int[maxQueueSize];
            queueTimestamps = new float[maxQueueSize];

            // Validate required components
            if (!ValidateComponents())
            {
                LogDebug("Component validation failed - notification system disabled");
                enabled = false;
                return;
            }

            // Initialize UI state
            SetNotificationVisible(false);
            currentState = NotificationState.Inactive;

            LogDebug($"Xbox Notification Queue initialized - Max queue: {maxQueueSize}");
            isInitialized = true;
        }

        void Update()
        {
            if (!isInitialized)
            {
                LogVerbose("Update() called but not initialized - skipping");
                return;
            }

            // Add periodic queue status check
            if (Time.frameCount % 60 == 0) // Every 60 frames
            {
                LogDebug($"PERIODIC CHECK: Queue count: {queueCount}, isDisplayingNotification: {isDisplayingNotification}, currentState: {currentState}");
                if (queueCount > 0)
                {
                    LogDebug($"PERIODIC CHECK: Next item in queue: {queueTypes[0]} for {queuePlayerNames[0]}");
                }
            }

            LogTiming($"Update() - Frame: {Time.frameCount}, Time: {Time.time:F3}, Queue: {queueCount}, Active: {isDisplayingNotification}");

            // Process queue if not currently displaying and queue has items
            if (!isDisplayingNotification && queueCount > 0)
            {
                LogDebug($"QUEUE PROCESSING TRIGGERED: Queue count: {queueCount}, isDisplayingNotification: {isDisplayingNotification}");
                LogVerbose($"QUEUE PROCESSING: Ready to start next notification - Queue count: {queueCount}");
                LogVerbose($"QUEUE PROCESSING: isDisplayingNotification = {isDisplayingNotification}");
                LogVerbose($"QUEUE PROCESSING: currentState = {currentState}");
                StartNextNotification();
            }

            // Handle current notification state
            if (isDisplayingNotification)
            {
                LogState($"Updating notification - State: {currentState}, Timer: {stateTimer:F3}");
                UpdateCurrentNotification();
            }
            else if (queueCount > 0)
            {
                LogDebug($"QUEUE ISSUE: Have {queueCount} items in queue but not displaying. isDisplayingNotification = {isDisplayingNotification}");
            }
            else
            {
                LogTiming("No active notification - Update() idle");
            }
        }

        public void QueueAchievementNotification(string playerName, string achievementTitle, int points)
        {
            AddToQueue(NotificationType.Achievement, playerName, achievementTitle, points, 50);
            LogDebug($"Queued achievement: {playerName} - {achievementTitle} - {points}G");
        }

        public void QueueOnlineNotification(string playerName, bool isFirstTime)
        {
            var type = isFirstTime ? NotificationType.FirstTime : NotificationType.Online;
            var priority = isFirstTime ? 40 : 25;
            AddToQueue(type, playerName, "", 0, priority);
            LogDebug($"Queued online notification: {playerName} (First time: {isFirstTime})");
        }

        public void QueueSupporterNotification(string playerName, string achievementTitle, int points)
        {
            AddToQueue(NotificationType.Supporter, playerName, achievementTitle, points, 75);
            LogDebug($"Queued supporter notification: {playerName} - {achievementTitle} - {points}G");
        }

        public void QueuePwnererNotification(string playerName, string achievementTitle, int points)
        {
            AddToQueue(NotificationType.Pwnerer, playerName, achievementTitle, points, 100);
            LogDebug($"Queued pwnerer notification: {playerName} - {achievementTitle} - {points}G");
        }

        private void AddToQueue(NotificationType type, string playerName, string achievementTitle, int points, int priority)
        {
            LogVerbose($"AddToQueue called - Type: {type}, Player: {playerName}, Priority: {priority}");
            LogDebug($"QUEUE DEBUG: Current queue count BEFORE adding: {queueCount}");

            if (queueCount >= maxQueueSize)
            {
                LogDebug("Queue full! Dropping oldest low-priority notification");
                RemoveLowestPriorityNotification();
            }

            // Add to queue
            queueTypes[queueCount] = type;
            queuePlayerNames[queueCount] = playerName;
            queueAchievementTitles[queueCount] = achievementTitle;
            queuePoints[queueCount] = points;
            queuePriorities[queueCount] = priority;
            queueTimestamps[queueCount] = Time.time;
            queueCount++;

            LogDebug($"QUEUE DEBUG: Item added at index {queueCount - 1}");
            LogDebug($"QUEUE DEBUG: Queue count AFTER adding: {queueCount}");
            LogVerbose($"QUEUE DEBUG: Added item details - Type: {queueTypes[queueCount - 1]}, Player: {queuePlayerNames[queueCount - 1]}");

            // Sort queue by priority
            LogVerbose("QUEUE DEBUG: Starting sort...");
            SortQueue();
            LogDebug($"QUEUE DEBUG: Queue count AFTER sorting: {queueCount}");

            if (enableQueueDebug)
            {
                LogDebug($"Queue updated - Count: {queueCount}");
                // Verify queue contents
                for (int i = 0; i < queueCount; i++)
                {
                    LogVerbose($"QUEUE CONTENTS[{i}]: {queueTypes[i]} - {queuePlayerNames[i]} - Priority: {queuePriorities[i]}");
                }
            }

            LogDebug($"QUEUE DEBUG: AddToQueue complete - Final count: {queueCount}");
        }

        private void SortQueue()
        {
            LogDebug($"SORT DEBUG: Starting sort with {queueCount} items");

            for (int i = 0; i < queueCount - 1; i++)
            {
                for (int j = 0; j < queueCount - i - 1; j++)
                {
                    // Sort by priority (descending)
                    if (queuePriorities[j] < queuePriorities[j + 1])
                    {
                        LogVerbose($"SORT DEBUG: Swapping items {j} and {j + 1} (priorities {queuePriorities[j]} < {queuePriorities[j + 1]})");
                        // Swap all arrays
                        SwapQueueItems(j, j + 1);
                    }
                }
            }

            LogDebug($"SORT DEBUG: Sort complete - Queue count is still: {queueCount}");
        }

        private void SwapQueueItems(int index1, int index2)
        {
            // Swap types
            NotificationType tempType = queueTypes[index1];
            queueTypes[index1] = queueTypes[index2];
            queueTypes[index2] = tempType;

            // Swap player names
            string tempPlayer = queuePlayerNames[index1];
            queuePlayerNames[index1] = queuePlayerNames[index2];
            queuePlayerNames[index2] = tempPlayer;

            // Swap achievement titles
            string tempTitle = queueAchievementTitles[index1];
            queueAchievementTitles[index1] = queueAchievementTitles[index2];
            queueAchievementTitles[index2] = tempTitle;

            // Swap points
            int tempPoints = queuePoints[index1];
            queuePoints[index1] = queuePoints[index2];
            queuePoints[index2] = tempPoints;

            // Swap priorities
            int tempPriority = queuePriorities[index1];
            queuePriorities[index1] = queuePriorities[index2];
            queuePriorities[index2] = tempPriority;

            // Swap timestamps
            float tempTimestamp = queueTimestamps[index1];
            queueTimestamps[index1] = queueTimestamps[index2];
            queueTimestamps[index2] = tempTimestamp;
        }

        private void RemoveLowestPriorityNotification()
        {
            if (queueCount == 0) return;
            queueCount--;
        }

        private void StartNextNotification()
        {
            if (queueCount == 0)
            {
                LogVerbose("StartNextNotification called but queue is empty");
                return;
            }

            LogDebug($"STARTING NOTIFICATION: Processing {queueTypes[0]} for {queuePlayerNames[0]}");
            LogVerbose($"STARTING NOTIFICATION: Queue index 0 data check:");
            LogVerbose($"  Type: {queueTypes[0]}");
            LogVerbose($"  Player: {queuePlayerNames[0]}");
            LogVerbose($"  Title: {queueAchievementTitles[0]}");
            LogVerbose($"  Points: {queuePoints[0]}");

            // Setup notification display BEFORE removing from queue
            SetupNotificationDisplay(0);

            // Remove from queue and shift remaining items
            LogVerbose("Removing item from queue...");
            RemoveFromQueue(0);
            LogVerbose($"Queue count after removal: {queueCount}");

            // Start fade-in animation
            isDisplayingNotification = true;
            currentState = NotificationState.FadingIn;
            stateTimer = 0f;
            currentAlpha = 0f;

            LogDebug($"NOTIFICATION STARTED: isDisplayingNotification = {isDisplayingNotification}, currentState = {currentState}");
            LogVerbose($"Animation variables set - stateTimer: {stateTimer}, currentAlpha: {currentAlpha}");
        }

        private void SetupNotificationDisplay(int queueIndex)
        {
            LogDebug($"SETUP DISPLAY: Starting setup for index {queueIndex}");
            LogVerbose($"SetupNotificationDisplay - Type: {queueTypes[queueIndex]}");

            // Validate components before setup
            if (mainText == null)
            {
                LogDebug("ERROR: mainText is null!");
                return;
            }
            if (subText == null)
            {
                LogDebug("ERROR: subText is null!");
                return;
            }
            if (notificationBackground == null)
            {
                LogDebug("ERROR: notificationBackground is null!");
                return;
            }

            string mainTextContent = "";
            string subTextContent = "";

            switch (queueTypes[queueIndex])
            {
                case NotificationType.Achievement:
                    mainTextContent = "Achievement Unlocked";
                    subTextContent = $"{queuePlayerNames[queueIndex]} - {queueAchievementTitles[queueIndex]} - {queuePoints[queueIndex]}G";
                    break;

                case NotificationType.FirstTime:
                    mainTextContent = "Welcome!";
                    subTextContent = $"{queuePlayerNames[queueIndex]} joined the party for the first time!";
                    break;

                case NotificationType.Online:
                    mainTextContent = "Online";
                    subTextContent = $"{queuePlayerNames[queueIndex]} is now online - Welcome back to the basement!";
                    break;

                case NotificationType.Supporter:
                    mainTextContent = "Supporter Achievement Unlocked";
                    subTextContent = $"{queuePlayerNames[queueIndex]} - {queueAchievementTitles[queueIndex]} - {queuePoints[queueIndex]}G";
                    break;

                case NotificationType.Pwnerer:
                    mainTextContent = "Pwnerer Achievement Unlocked";
                    subTextContent = $"{queuePlayerNames[queueIndex]} - {queueAchievementTitles[queueIndex]} - {queuePoints[queueIndex]}G";
                    break;
            }

            LogDebug($"SETUP DISPLAY: Setting text - Main: '{mainTextContent}', Sub: '{subTextContent}'");
            mainText.text = mainTextContent;
            subText.text = subTextContent;
            LogDebug($"SETUP DISPLAY: Text assignment completed");

            // Setup icons for blinking
            LogDebug("SETUP DISPLAY: Setting up icons...");
            if (xboxIcon != null && trophyIcon != null)
            {
                xboxIcon.SetActive(true);
                trophyIcon.SetActive(false);
                showingXboxIcon = true;
                blinkTimer = 0f;
                LogDebug($"SETUP DISPLAY: Icons set - Xbox: {xboxIcon.activeSelf}, Trophy: {trophyIcon.activeSelf}");
            }
            else
            {
                LogDebug("ERROR: Xbox or Trophy icon is null!");
            }

            // Play appropriate sound
            LogDebug("SETUP DISPLAY: Playing sound...");
            PlayNotificationSound(queueTypes[queueIndex]);

            LogDebug("SETUP DISPLAY: Setup complete");
        }

        private void PlayNotificationSound(NotificationType type)
        {
            if (audioSource == null) return;

            AudioClip soundToPlay = null;

            switch (type)
            {
                case NotificationType.Achievement:
                case NotificationType.FirstTime:
                    soundToPlay = achievementSound;
                    break;
                case NotificationType.Online:
                    soundToPlay = onlineSound;
                    break;
                case NotificationType.Supporter:
                    soundToPlay = supporterSound;
                    break;
                case NotificationType.Pwnerer:
                    soundToPlay = pwnererSound;
                    break;
            }

            if (soundToPlay != null)
            {
                audioSource.PlayOneShot(soundToPlay);
                LogDebug($"Playing sound for type: {type}");
            }
        }

        private void UpdateCurrentNotification()
        {
            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case NotificationState.FadingIn:
                    UpdateFadeIn();
                    break;

                case NotificationState.Blinking:
                    UpdateBlinking();
                    break;

                case NotificationState.FadingOut:
                    UpdateFadeOut();
                    break;

                case NotificationState.QueueDelay:
                    UpdateQueueDelay();
                    break;
            }
        }

        private void UpdateFadeIn()
        {
            LogState($"UpdateFadeIn - Timer: {stateTimer:F3}, Target: {fadeInDuration:F3}");

            currentAlpha = Mathf.Clamp01(stateTimer / fadeInDuration);
            canvasGroup.alpha = currentAlpha;

            if (stateTimer >= fadeInDuration)
            {
                currentState = NotificationState.Blinking;
                stateTimer = 0f;
                isBlinking = true;
                SetNotificationVisible(true);
                LogDebug("Notification fade-in complete, starting blinking");
            }
        }

        private void UpdateBlinking()
        {
            LogState($"UpdateBlinking - StateTimer: {stateTimer:F3}, BlinkTimer: {blinkTimer:F3}");

            blinkTimer += Time.deltaTime;

            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                showingXboxIcon = !showingXboxIcon;
                xboxIcon.SetActive(showingXboxIcon);
                trophyIcon.SetActive(!showingXboxIcon);

                LogVerbose($"Icon blink - Xbox: {showingXboxIcon}, Trophy: {!showingXboxIcon}");
            }

            if (stateTimer >= displayDuration)
            {
                currentState = NotificationState.FadingOut;
                stateTimer = 0f;
                isBlinking = false;
                LogDebug("Notification display complete, starting fade-out");
            }
        }

        private void UpdateFadeOut()
        {
            LogState($"UpdateFadeOut - Timer: {stateTimer:F3}, Target: {fadeOutDuration:F3}");

            currentAlpha = 1f - Mathf.Clamp01(stateTimer / fadeOutDuration);
            canvasGroup.alpha = currentAlpha;

            if (stateTimer >= fadeOutDuration)
            {
                SetNotificationVisible(false);
                currentState = NotificationState.QueueDelay;
                stateTimer = 0f;
                LogDebug("Notification fade-out complete");
            }
        }

        private void UpdateQueueDelay()
        {
            LogState($"UpdateQueueDelay - Timer: {stateTimer:F3}, Target: {queueDelay:F3}");

            if (stateTimer >= queueDelay)
            {
                isDisplayingNotification = false;
                currentState = NotificationState.Inactive;
                stateTimer = 0f;
                LogDebug("Queue delay complete, ready for next notification");
            }
        }

        private void RemoveFromQueue(int index)
        {
            if (index < 0 || index >= queueCount) return;

            for (int i = index; i < queueCount - 1; i++)
            {
                queueTypes[i] = queueTypes[i + 1];
                queuePlayerNames[i] = queuePlayerNames[i + 1];
                queueAchievementTitles[i] = queueAchievementTitles[i + 1];
                queuePoints[i] = queuePoints[i + 1];
                queuePriorities[i] = queuePriorities[i + 1];
                queueTimestamps[i] = queueTimestamps[i + 1];
            }

            queueCount--;
        }

        private void SetNotificationVisible(bool visible)
        {
            LogVerbose($"SetNotificationVisible called - Visible: {visible}");

            // Always keep the GameObject active—only adjust alpha:
            //notificationBackground.SetActive(true);

            canvasGroup.alpha = visible ? 1f : 0f;

            if (!visible)
            {
                // Hide icons immediately when alpha=0
                xboxIcon.SetActive(false);
                trophyIcon.SetActive(false);
            }
        }

        private bool ValidateComponents()
        {
            bool isValid = true;

            if (notificationBackground == null) { LogDebug("Missing: Notification Background"); isValid = false; }
            if (canvasGroup == null) { LogDebug("Missing: Canvas Group"); isValid = false; }
            if (xboxIcon == null) { LogDebug("Missing: Xbox Icon"); isValid = false; }
            if (trophyIcon == null) { LogDebug("Missing: Trophy Icon"); isValid = false; }
            if (mainText == null) { LogDebug("Missing: Main Text"); isValid = false; }
            if (subText == null) { LogDebug("Missing: Sub Text"); isValid = false; }

            return isValid;
        }

        public void ClearQueue()
        {
            queueCount = 0;
            isDisplayingNotification = false;
            currentState = NotificationState.Inactive;
            SetNotificationVisible(false);
            LogDebug("Queue cleared");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XboxNotificationQueue] {message}");
            }
        }

        private void LogVerbose(string message)
        {
            if (enableVerboseLogging)
            {
                Debug.Log($"[XboxQueue-VERBOSE] {message}");
            }
        }

        private void LogTiming(string message)
        {
            if (enableTimingDebug && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[XboxQueue-TIMING] {message}");
            }
        }

        private void LogState(string message)
        {
            if (enableStateDebug)
            {
                Debug.Log($"[XboxQueue-STATE] {message}");
            }
        }

        [ContextMenu("Test Achievement Notification")]
        public void TestAchievementNotification()
        {
            QueueAchievementNotification("TestPlayer", "Basement Dweller", 50);
        }

        [ContextMenu("Enable All Debug Logging")]
        public void EnableAllDebugLogging()
        {
            enableDebugLogging = true;
            enableQueueDebug = true;
            enableVerboseLogging = true;
            enableTimingDebug = true;
            enableStateDebug = true;
            LogDebug("All debug logging enabled");
        }

        [ContextMenu("Clear Queue")]
        public void TestClearQueue()
        {
            ClearQueue();
        }
    }
}