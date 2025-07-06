using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace LowerLevel.Achievements
{
    /// <summary>
    /// COMPONENT PURPOSE:
    /// Handles VRChat player events and coordinates with AchievementDataManager
    /// to track player visits and detect achievement unlocks. This is the "controller"
    /// that connects VRChat's networking events to our achievement system.
    /// 
    /// LOWER LEVEL 2.0 INTEGRATION:
    /// Creates the Xbox Live social atmosphere by detecting when friends join
    /// the basement and celebrating their progress through achievement milestones.
    /// 
    /// DEPENDENCIES & REQUIREMENTS:
    /// - AchievementDataManager script must exist in the scene
    /// - Must be attached to a GameObject in the scene
    /// - VRChat networking events (OnPlayerJoined, OnPlayerLeft)
    /// 
    /// SIMPLE ALTERNATIVE CONSIDERED:
    /// Could combine this with DataManager, but separation allows for easier
    /// debugging and potential future features like multiple achievement systems.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AchievementTracker : UdonSharpBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to the AchievementDataManager in the scene")]
        [SerializeField] private AchievementDataManager dataManager;

        [Header("Debug Settings")]
        [Tooltip("Enable detailed logging for troubleshooting tracker events")]
        [SerializeField] private bool enableDebugLogging = true;

        [Header("Achievement Detection")]
        [Tooltip("Check for achievements when players join (recommended: true)")]
        [SerializeField] private bool checkAchievementsOnJoin = true;

        // =================================================================
        // INTERNAL STATE TRACKING
        // Keep track of what we've already processed to avoid duplicates
        // =================================================================

        private bool isInitialized = false;

        // =================================================================
        // INITIALIZATION - Called when world loads
        // =================================================================

        /// <summary>
        /// Initialize component - called automatically by Unity
        /// Sets up references and validates configuration
        /// </summary>
        void Start()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Main initialization routine for the achievement tracker
        /// Validates that all required components are available
        /// </summary>
        protected virtual void InitializeComponent()
        {
            LogDebug("Achievement Tracker initializing...");

            // Validate that we have a data manager reference
            if (dataManager == null)
            {
                LogDebug("ERROR: No AchievementDataManager assigned! Please assign in Inspector.");
                return;
            }

            LogDebug("Achievement Tracker initialized successfully");
            isInitialized = true;
        }

        // =================================================================
        // VRCHAT NETWORKING EVENTS
        // These are automatically called by VRChat when players join/leave
        // =================================================================

        /// <summary>
        /// Called automatically by VRChat when ANY player joins the world
        /// This includes the local player and all remote players
        /// </summary>
        /// <param name="player">The VRCPlayerApi of the player who joined</param>
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            // Make sure we're properly initialized
            if (!isInitialized || dataManager == null)
            {
                LogDebug("Tracker not initialized, skipping player join event");
                return;
            }

            // Get the player's display name
            string playerName = player.displayName;

            if (string.IsNullOrEmpty(playerName))
            {
                LogDebug("Player joined with empty name, skipping");
                return;
            }

            LogDebug($"Player joined: {playerName}");

            // Add the visit to our tracking system
            int newVisitCount = dataManager.AddPlayerVisit(playerName);
            LogDebug($"{playerName} now has {newVisitCount} total visits");

            // Check for any new achievements this player just earned
            if (checkAchievementsOnJoin)
            {
                CheckForNewAchievements(playerName, newVisitCount);
            }
        }

        /// <summary>
        /// Called automatically by VRChat when ANY player leaves the world
        /// Currently just logs for debugging - could be used for future features
        /// </summary>
        /// <param name="player">The VRCPlayerApi of the player who left</param>
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!isInitialized) return;

            string playerName = player.displayName;
            LogDebug($"Player left: {playerName}");

            // NOTE: We don't remove players from tracking when they leave
            // Their visit history persists for future visits
        }

        // =================================================================
        // ACHIEVEMENT DETECTION LOGIC
        // Determines what achievements a player just earned
        // =================================================================

        /// <summary>
        /// Checks if a player just earned any new achievements
        /// Called after their visit count is updated
        /// </summary>
        /// <param name="playerName">Player who just joined</param>
        /// <param name="currentVisits">Their new total visit count</param>
        private void CheckForNewAchievements(string playerName, int currentVisits)
        {
            LogDebug($"Checking achievements for {playerName} with {currentVisits} visits");

            // Check each achievement milestone to see if they just reached it
            int totalAchievements = dataManager.GetTotalAchievementCount();

            for (int achievementLevel = 0; achievementLevel < totalAchievements; achievementLevel++)
            {
                // Check if this player just reached this milestone
                if (IsNewlyEarnedAchievement(playerName, achievementLevel, currentVisits))
                {
                    HandleNewAchievement(playerName, achievementLevel);
                }
            }
        }

        /// <summary>
        /// Determines if a player just earned a specific achievement on this visit
        /// Returns true only if they JUST reached the milestone (not if they already had it)
        /// </summary>
        /// <param name="playerName">Player to check</param>
        /// <param name="achievementLevel">Achievement index to check</param>
        /// <param name="currentVisits">Their current visit count</param>
        /// <returns>True if they just earned this achievement</returns>
        private bool IsNewlyEarnedAchievement(string playerName, int achievementLevel, int currentVisits)
        {
            // Check if they have earned this achievement now
            bool hasEarnedNow = dataManager.HasPlayerEarnedAchievement(playerName, achievementLevel);

            if (!hasEarnedNow)
            {
                // They don't have this achievement yet
                return false;
            }

            // They have the achievement now, but did they just get it?
            // Check if they would have had it with one less visit
            int previousVisits = currentVisits - 1;

            // Get the milestone for this achievement level
            // We need to check if previousVisits was below the threshold
            // but currentVisits meets/exceeds it

            // For now, we'll use a simple approach: check if current visits
            // exactly equals the milestone (meaning they just reached it)
            int[] milestones = { 1, 5, 10, 25, 50, 75, 100, 250 }; // Same as in DataManager

            if (achievementLevel < milestones.Length)
            {
                bool justReached = currentVisits == milestones[achievementLevel];
                LogDebug($"Achievement {achievementLevel}: visits={currentVisits}, milestone={milestones[achievementLevel]}, justReached={justReached}");
                return justReached;
            }

            return false;
        }

        /// <summary>
        /// Handles when a player earns a new achievement
        /// This is where we'll trigger notifications in Phase 2
        /// </summary>
        /// <param name="playerName">Player who earned the achievement</param>
        /// <param name="achievementLevel">Achievement they just earned</param>
        private void HandleNewAchievement(string playerName, int achievementLevel)
        {
            // Get achievement details from data manager
            string achievementTitle = dataManager.GetAchievementTitle(achievementLevel);
            int achievementPoints = dataManager.GetAchievementPoints(achievementLevel);
            int totalPoints = dataManager.GetPlayerTotalPoints(playerName);

            LogDebug($"🏆 NEW ACHIEVEMENT! {playerName} earned: {achievementTitle} ({achievementPoints}G)");
            LogDebug($"   Total points for {playerName}: {totalPoints}G");

            // =================================================================
            // PHASE 2 INTEGRATION POINT
            // This is where we'll trigger Xbox 360 notifications
            // For now, we just log the achievement
            // =================================================================

            // TODO Phase 2: Trigger Xbox notification popup
            // TODO Phase 2: Broadcast achievement to all players
            // TODO Phase 3: Update DOS terminal achievement pages

            LogDebug($"Achievement notification ready for Phase 2 implementation");
        }

        // =================================================================
        // PUBLIC API METHODS
        // Methods that other scripts can call for testing or integration
        // =================================================================

        /// <summary>
        /// Manual method to simulate a player joining (for testing)
        /// Only available when debug logging is enabled
        /// </summary>
        /// <param name="testPlayerName">Name of fake player to simulate</param>
        public void SimulatePlayerJoin(string testPlayerName)
        {
            if (!enableDebugLogging)
            {
                LogDebug("Player simulation only available in debug mode");
                return;
            }

            LogDebug($"SIMULATING player join: {testPlayerName}");

            // Manually add a visit and check achievements
            if (dataManager != null)
            {
                int newVisitCount = dataManager.AddPlayerVisit(testPlayerName);
                LogDebug($"Simulated visit result: {testPlayerName} = {newVisitCount} visits");

                if (checkAchievementsOnJoin)
                {
                    CheckForNewAchievements(testPlayerName, newVisitCount);
                }
            }
        }

        /// <summary>
        /// Gets current status of the achievement tracker
        /// Useful for debugging and status displays
        /// </summary>
        /// <returns>Status string with current tracker information</returns>
        public string GetTrackerStatus()
        {
            if (!isInitialized)
                return "Achievement Tracker: Not Initialized";

            if (dataManager == null)
                return "Achievement Tracker: No Data Manager";

            string[] allPlayers = dataManager.GetAllPlayerNames();
            return $"Achievement Tracker: Active, tracking {allPlayers.Length} players";
        }

        // =================================================================
        // DEBUG AND UTILITY METHODS
        // Helper methods for troubleshooting and development
        // =================================================================

        /// <summary>
        /// Centralized debug logging system
        /// Only logs when debug mode is enabled to avoid console spam
        /// Includes component name for easy identification
        /// </summary>
        /// <param name="message">Debug message to log</param>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[{GetType().Name}] {message}");
            }
        }

        /// <summary>
        /// Development method to test achievement detection
        /// Creates several test players with different visit counts
        /// </summary>
        [ContextMenu("Test Achievement System")]
        public void TestAchievementSystem()
        {
            if (!enableDebugLogging)
            {
                LogDebug("Testing only available in debug mode");
                return;
            }

            LogDebug("=== TESTING ACHIEVEMENT SYSTEM ===");

            // Test various achievement milestones
            SimulatePlayerJoin("TestPlayer1"); // 1 visit = First Time Visitor
            SimulatePlayerJoin("TestPlayer2"); // 1 visit
            SimulatePlayerJoin("TestPlayer2"); // 2 visits
            SimulatePlayerJoin("TestPlayer2"); // 3 visits
            SimulatePlayerJoin("TestPlayer2"); // 4 visits
            SimulatePlayerJoin("TestPlayer2"); // 5 visits = Regular Visitor

            LogDebug("=== TEST COMPLETE ===");
        }
    }
}