using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using LowerLevel.Notifications;
using LowerLevel.Achievements;

namespace LowerLevel.Integration
{
    /// <summary>
    /// COMPONENT PURPOSE:
    /// Central event hub that connects achievement tracking to notification displays
    /// without modifying existing working components. Acts as a bridge between systems.
    /// 
    /// LOWER LEVEL 2.0 INTEGRATION:
    /// Creates the Xbox Live social atmosphere by coordinating achievement detection
    /// with visual notifications across multiple displays throughout the basement.
    /// 
    /// DEPENDENCIES & REQUIREMENTS:
    /// - AchievementTracker must exist in scene
    /// - At least one XboxNotificationUI display must exist
    /// - Optional: Multiple notification displays for multi-TV setup
    /// - Optional: DOS Terminal for achievement data display
    /// 
    /// ARCHITECTURE PATTERN:
    /// Uses Observer pattern with explicit references to maintain UdonSharp compatibility
    /// All communication uses proven SendCustomEvent patterns with frame delays
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NotificationEventHub : UdonSharpBehaviour
    {
        [Header("Core System References")]
        [Tooltip("Reference to the Achievement Tracker (do not modify)")]
        [SerializeField] private AchievementTracker achievementTracker;

        [Tooltip("Reference to Achievement Data Manager for querying data")]
        [SerializeField] private AchievementDataManager dataManager;

        [Header("Notification Display References")]
        [Tooltip("Primary notification display (usually TV in main room)")]
        [SerializeField] private XboxNotificationUI primaryNotificationDisplay;

        [Tooltip("Additional notification displays for multi-TV setup")]
        [SerializeField] private XboxNotificationUI[] additionalDisplays;

        [Header("DOS Terminal Integration")]
        [Tooltip("Optional: DOS Terminal for real-time achievement display")]
        [SerializeField] private UdonBehaviour dosTerminalController; // Generic reference

        [Header("User Status Configuration")]
        [Tooltip("List of VRChat usernames who are supporters")]
        [SerializeField] private string[] supporterUsernames = new string[0];

        [Tooltip("List of VRChat usernames who are pwnerers (owners)")]
        [SerializeField] private string[] pwnererUsernames = new string[0];

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool logAllEvents = false;

        // Runtime state
        private bool isInitialized = false;
        private XboxNotificationUI[] allDisplays;

        void Start()
        {
            InitializeEventHub();
        }

        /// <summary>
        /// Initialize the event hub and validate all references
        /// </summary>
        private void InitializeEventHub()
        {
            LogDebug("NotificationEventHub initializing...");

            // Validate required components
            if (!ValidateComponents())
            {
                LogDebug("ERROR: Component validation failed - hub disabled");
                enabled = false;
                return;
            }

            // Build display array for easy iteration
            BuildDisplayArray();

            // Register with achievement tracker (if it supports registration)
            // For now, we'll use polling or event interception

            LogDebug($"NotificationEventHub initialized - Managing {allDisplays.Length} displays");
            isInitialized = true;
        }

        /// <summary>
        /// Called by AchievementTracker when a player earns an achievement
        /// This is our main integration point
        /// </summary>
        public void OnAchievementEarned(string playerName, int achievementLevel)
        {
            if (!isInitialized) return;

            // Get achievement details from data manager
            string achievementTitle = dataManager.GetAchievementTitle(achievementLevel);
            int achievementPoints = dataManager.GetAchievementPoints(achievementLevel);

            LogDebug($"Achievement earned: {playerName} - {achievementTitle} ({achievementPoints}G)");

            // Determine user status for special notifications
            NotificationType notificationType = DetermineNotificationType(playerName, true);

            // Queue notification on all displays
            QueueNotificationOnAllDisplays(notificationType, playerName, achievementTitle, achievementPoints);

            // Update DOS terminal if connected
            if (dosTerminalController != null)
            {
                SendCustomEventDelayedFrames(nameof(UpdateDOSTerminalDelayed), 5);
            }
        }

        /// <summary>
        /// Called by AchievementTracker when a player joins
        /// Handles online notifications based on visit count
        /// </summary>
        public void OnPlayerJoinedWorld(string playerName)
        {
            if (!isInitialized) return;

            int visitCount = dataManager.GetPlayerVisits(playerName);
            bool isFirstTime = visitCount == 1;

            LogDebug($"Player joined: {playerName} (Visit #{visitCount})");

            // Determine notification type based on user status
            NotificationType notificationType = DetermineNotificationType(playerName, false);

            // Queue appropriate online notification
            switch (notificationType)
            {
                case NotificationType.Supporter:
                case NotificationType.Pwnerer:
                    // Special users always get achievement-style notifications
                    QueueNotificationOnAllDisplays(notificationType, playerName,
                        isFirstTime ? "First Visit!" : $"Visit #{visitCount}", 0);
                    break;
                default:
                    // Regular users get online notifications
                    QueueOnlineNotificationOnAllDisplays(playerName, isFirstTime);
                    break;
            }
        }

        /// <summary>
        /// Determines the notification type based on user status
        /// </summary>
        private NotificationType DetermineNotificationType(string playerName, bool isAchievement)
        {
            // Check if player is a pwnerer (highest priority)
            if (IsPlayerInList(playerName, pwnererUsernames))
            {
                return NotificationType.Pwnerer;
            }

            // Check if player is a supporter
            if (IsPlayerInList(playerName, supporterUsernames))
            {
                return NotificationType.Supporter;
            }

            // Regular player
            return isAchievement ? NotificationType.Achievement : NotificationType.Online;
        }

        /// <summary>
        /// Helper to check if player is in a status list
        /// </summary>
        private bool IsPlayerInList(string playerName, string[] list)
        {
            if (list == null || list.Length == 0) return false;

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == playerName) return true;
            }
            return false;
        }

        /// <summary>
        /// Queues an achievement notification on all connected displays
        /// </summary>
        private void QueueNotificationOnAllDisplays(NotificationType type,
            string playerName, string achievementTitle, int points)
        {
            if (logAllEvents)
            {
                LogDebug($"Broadcasting {type} notification to {allDisplays.Length} displays");
            }

            // Queue on all displays
            for (int i = 0; i < allDisplays.Length; i++)
            {
                if (allDisplays[i] == null) continue;

                switch (type)
                {
                    case NotificationType.Achievement:
                        allDisplays[i].QueueAchievementNotification(playerName, achievementTitle, points);
                        break;
                    case NotificationType.Supporter:
                        allDisplays[i].QueueSupporterNotification(playerName, achievementTitle, points);
                        break;
                    case NotificationType.Pwnerer:
                        allDisplays[i].QueuePwnererNotification(playerName, achievementTitle, points);
                        break;
                }
            }
        }

        /// <summary>
        /// Queues an online notification on all connected displays
        /// </summary>
        private void QueueOnlineNotificationOnAllDisplays(string playerName, bool isFirstTime)
        {
            if (logAllEvents)
            {
                LogDebug($"Broadcasting online notification to {allDisplays.Length} displays");
            }

            // Queue on all displays
            for (int i = 0; i < allDisplays.Length; i++)
            {
                if (allDisplays[i] == null) continue;
                allDisplays[i].QueueOnlineNotification(playerName, isFirstTime);
            }
        }

        /// <summary>
        /// Updates DOS terminal with latest achievement data
        /// Called via frame delay to avoid timing issues
        /// </summary>
        public void UpdateDOSTerminalDelayed()
        {
            if (dosTerminalController == null) return;

            // DOS terminal update would go here
            // For now, just log that we would update it
            LogDebug("DOS Terminal update triggered (implementation pending)");

            // Example of how to call DOS terminal update:
            // dosTerminalController.SendCustomEvent("RefreshAchievementData");
        }

        /// <summary>
        /// Builds array of all displays for easy iteration
        /// </summary>
        private void BuildDisplayArray()
        {
            int totalDisplays = 1; // Primary display
            if (additionalDisplays != null) totalDisplays += additionalDisplays.Length;

            allDisplays = new XboxNotificationUI[totalDisplays];
            allDisplays[0] = primaryNotificationDisplay;

            if (additionalDisplays != null)
            {
                for (int i = 0; i < additionalDisplays.Length; i++)
                {
                    allDisplays[i + 1] = additionalDisplays[i];
                }
            }

            LogDebug($"Display array built - Total displays: {totalDisplays}");
        }

        /// <summary>
        /// Validates all required components are assigned
        /// </summary>
        private bool ValidateComponents()
        {
            bool isValid = true;

            if (achievementTracker == null)
            {
                LogDebug("ERROR: Achievement Tracker not assigned");
                isValid = false;
            }

            if (dataManager == null)
            {
                LogDebug("ERROR: Achievement Data Manager not assigned");
                isValid = false;
            }

            if (primaryNotificationDisplay == null)
            {
                LogDebug("ERROR: Primary Notification Display not assigned");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Centralized debug logging
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[NotificationEventHub] {message}");
            }
        }

        // =================================================================
        // TEST METHODS
        // For validating the integration without real players
        // =================================================================

        [ContextMenu("Test Regular Achievement")]
        public void TestRegularAchievement()
        {
            OnAchievementEarned("TestPlayer", 3); // 25 visits achievement
        }

        [ContextMenu("Test Supporter Achievement")]
        public void TestSupporterAchievement()
        {
            // Temporarily add test player as supporter
            string[] tempSupporters = new string[supporterUsernames.Length + 1];
            supporterUsernames.CopyTo(tempSupporters, 0);
            tempSupporters[tempSupporters.Length - 1] = "TestSupporter";
            supporterUsernames = tempSupporters;

            OnAchievementEarned("TestSupporter", 4); // 50 visits achievement
        }

        [ContextMenu("Test First Time Join")]
        public void TestFirstTimeJoin()
        {
            // Simulate first time visitor
            OnPlayerJoinedWorld("NewPlayer");
        }

        [ContextMenu("Test All Displays")]
        public void TestAllDisplays()
        {
            LogDebug($"Testing notification on {allDisplays.Length} displays...");
            QueueNotificationOnAllDisplays(NotificationType.Achievement,
                "DisplayTest", "Multi-TV Test", 100);
        }
    }
}