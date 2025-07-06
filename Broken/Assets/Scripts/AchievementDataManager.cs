using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace LowerLevel.Achievements
{
    /// <summary>
    /// COMPONENT PURPOSE:
    /// Core data management system for the Lower Level 2.0 achievement tracking.
    /// Handles all player visit data persistence, achievement milestone checking,
    /// and provides clean API for other systems to query player progress.
    /// 
    /// LOWER LEVEL 2.0 INTEGRATION:
    /// This creates the foundation for community engagement by tracking player
    /// visits and building the social progression system that encourages return visits.
    /// Supports the nostalgic Xbox Live achievement atmosphere.
    /// 
    /// DEPENDENCIES & REQUIREMENTS:
    /// - VRChat SDK 3.8.2+ for persistence system
    /// - Must be attached to a GameObject in the scene
    /// - No external dependencies - self-contained data management
    /// 
    /// SIMPLE ALTERNATIVE CONSIDERED:
    /// Could use simple PlayerPrefs, but VRChat persistence ensures data
    /// survives across world instances and is shared among all players.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AchievementDataManager : UdonSharpBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("Enable detailed logging for troubleshooting achievement system")]
        [SerializeField] private bool enableDebugLogging = true;

        [Header("Achievement Configuration")]
        [Tooltip("Maximum number of players to track (prevents data bloat)")]
        [SerializeField] private int maxPlayersToTrack = 1000;

        // =================================================================
        // PERSISTENCE SYSTEM - VRChat World Persistence
        // Uses VRChat's built-in persistence to store achievement data
        // Data persists across world instances and is shared globally
        // =================================================================

        [UdonSynced]
        private string playerDataJSON = ""; // JSON string containing all player data

        // =================================================================
        // ACHIEVEMENT MILESTONE CONFIGURATION
        // Easy to modify achievement levels and point values
        // These arrays must have matching indices for proper data alignment
        // =================================================================

        private int[] achievementMilestones = { 1, 5, 10, 25, 50, 75, 100, 250 };
        private string[] achievementTitles = {
            "First Time Visitor",
            "Regular Visitor",
            "Frequent Flyer",
            "Basement Dwellers",
            "Lower Level Legends",
            "Founding Members",
            "Century Club",
            "Legendary Status"
        };
        private int[] achievementPoints = { 10, 15, 25, 50, 75, 100, 150, 250 };

        // =================================================================
        // RUNTIME DATA STRUCTURES
        // These store parsed player data in memory for fast access
        // Arrays are kept in sync by index (playerNames[0] matches playerVisitCounts[0])
        // =================================================================

        private string[] playerNames = new string[0];        // VRChat display names
        private int[] playerVisitCounts = new int[0];        // Visit counts per player
        private string[] playerFirstVisitDates = new string[0]; // ISO date strings
        private bool isDataLoaded = false;                   // Initialization flag

        // =================================================================
        // INITIALIZATION - Called when world loads
        // =================================================================

        /// <summary>
        /// Initialize component - called automatically by Unity
        /// Sets up initial state and loads existing achievement data
        /// </summary>
        void Start()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Main initialization routine for the achievement system
        /// Loads existing player data from VRChat persistence
        /// </summary>
        protected virtual void InitializeComponent()
        {
            LogDebug("Achievement Data Manager initializing...");

            // Load any existing achievement data from persistence
            LoadPlayerDataFromPersistence();

            LogDebug($"Initialization complete. Tracking {playerNames.Length} players.");
            isDataLoaded = true;
        }

        // =================================================================
        // PUBLIC API METHODS
        // These methods provide clean interface for other scripts
        // Keep these simple and well-documented for easy integration
        // =================================================================

        /// <summary>
        /// Gets the total number of visits for a specific player
        /// This is the primary method for checking player progress
        /// </summary>
        /// <param name="playerName">VRChat display name of the player</param>
        /// <returns>Number of visits, or 0 if player not found</returns>
        public int GetPlayerVisits(string playerName)
        {
            if (!isDataLoaded) return 0;

            int playerIndex = FindPlayerIndex(playerName);
            return playerIndex >= 0 ? playerVisitCounts[playerIndex] : 0;
        }

        /// <summary>
        /// Increments visit count for a player (creates new player if doesn't exist)
        /// Call this when a player joins the world
        /// </summary>
        /// <param name="playerName">VRChat display name of the player</param>
        /// <returns>New total visit count for this player</returns>
        public int AddPlayerVisit(string playerName)
        {
            if (!isDataLoaded)
            {
                LogDebug("Data not loaded yet, skipping visit addition");
                return 0;
            }

            // Validate input
            if (string.IsNullOrEmpty(playerName))
            {
                LogDebug("Invalid player name provided");
                return 0;
            }

            int playerIndex = FindPlayerIndex(playerName);

            if (playerIndex >= 0)
            {
                // Existing player - increment visit count
                playerVisitCounts[playerIndex]++;
                LogDebug($"Updated {playerName}: {playerVisitCounts[playerIndex]} visits");
            }
            else
            {
                // New player - add to tracking system
                AddNewPlayerToSystem(playerName);
                LogDebug($"New player added: {playerName}");
            }

            // Save updated data to persistence
            SavePlayerDataToPersistence();

            return GetPlayerVisits(playerName);
        }

        /// <summary>
        /// Checks if a player has earned a specific achievement level
        /// Used by notification systems to determine what to display
        /// </summary>
        /// <param name="playerName">VRChat display name</param>
        /// <param name="achievementLevel">Achievement index (0=first visit, 1=5 visits, etc.)</param>
        /// <returns>True if player has earned this achievement</returns>
        public bool HasPlayerEarnedAchievement(string playerName, int achievementLevel)
        {
            // Validate achievement level
            if (achievementLevel < 0 || achievementLevel >= achievementMilestones.Length)
            {
                LogDebug($"Invalid achievement level: {achievementLevel}");
                return false;
            }

            int playerVisits = GetPlayerVisits(playerName);
            bool hasEarned = playerVisits >= achievementMilestones[achievementLevel];

            LogDebug($"{playerName} achievement {achievementLevel}: {hasEarned} ({playerVisits} visits)");
            return hasEarned;
        }

        /// <summary>
        /// Gets the display title for a specific achievement level
        /// Used by UI systems to show achievement names
        /// </summary>
        /// <param name="achievementLevel">Achievement index</param>
        /// <returns>Human-readable achievement title</returns>
        public string GetAchievementTitle(int achievementLevel)
        {
            if (achievementLevel < 0 || achievementLevel >= achievementTitles.Length)
                return "Unknown Achievement";
            return achievementTitles[achievementLevel];
        }

        /// <summary>
        /// Gets the point value for a specific achievement level
        /// Used by leaderboard and scoring systems
        /// </summary>
        /// <param name="achievementLevel">Achievement index</param>
        /// <returns>Point value for this achievement</returns>
        public int GetAchievementPoints(int achievementLevel)
        {
            if (achievementLevel < 0 || achievementLevel >= achievementPoints.Length)
                return 0;
            return achievementPoints[achievementLevel];
        }

        /// <summary>
        /// Calculates total achievement points earned by a player
        /// Used for leaderboard rankings and player status
        /// </summary>
        /// <param name="playerName">VRChat display name</param>
        /// <returns>Total points earned across all achievements</returns>
        public int GetPlayerTotalPoints(string playerName)
        {
            int totalPoints = 0;

            for (int i = 0; i < achievementMilestones.Length; i++)
            {
                if (HasPlayerEarnedAchievement(playerName, i))
                {
                    totalPoints += achievementPoints[i];
                }
            }

            LogDebug($"{playerName} total points: {totalPoints}");
            return totalPoints;
        }

        /// <summary>
        /// Gets the total number of achievements available
        /// Used by UI systems to show completion progress
        /// </summary>
        /// <returns>Total number of achievement levels</returns>
        public int GetTotalAchievementCount()
        {
            return achievementMilestones.Length;
        }

        /// <summary>
        /// Gets all player names currently being tracked
        /// Used by leaderboard and admin systems
        /// </summary>
        /// <returns>Array of player names (copy, not reference)</returns>
        public string[] GetAllPlayerNames()
        {
            // Return a copy to prevent external modification
            string[] copy = new string[playerNames.Length];
            for (int i = 0; i < playerNames.Length; i++)
            {
                copy[i] = playerNames[i];
            }
            return copy;
        }

        // =================================================================
        // NETWORKING CALLBACKS
        // Handle VRChat persistence synchronization
        // Note: OnDeserialization handles sync updates automatically
        // =================================================================

        /// <summary>
        /// Called when networked data is received from other clients
        /// Automatically handles player data synchronization
        /// </summary>
        public override void OnDeserialization()
        {
            LogDebug("Received updated player data from network");
            LoadPlayerDataFromPersistence();
        }

        // =================================================================
        // INTERNAL DATA MANAGEMENT
        // Private methods that handle the complex data operations
        // =================================================================

        /// <summary>
        /// Finds the array index for a player name
        /// Returns -1 if player not found in tracking system
        /// </summary>
        /// <param name="playerName">Player name to search for</param>
        /// <returns>Array index or -1 if not found</returns>
        private int FindPlayerIndex(string playerName)
        {
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (playerNames[i] == playerName)
                    return i;
            }
            return -1; // Player not found
        }

        /// <summary>
        /// Adds a completely new player to the tracking system
        /// Expands all tracking arrays to accommodate the new player
        /// </summary>
        /// <param name="playerName">Name of new player to add</param>
        private void AddNewPlayerToSystem(string playerName)
        {
            // Check if we're at capacity
            if (playerNames.Length >= maxPlayersToTrack)
            {
                LogDebug($"Player tracking at capacity ({maxPlayersToTrack}), skipping new player");
                return;
            }

            int oldSize = playerNames.Length;
            int newSize = oldSize + 1;

            // Create expanded arrays
            string[] newPlayerNames = new string[newSize];
            int[] newVisitCounts = new int[newSize];
            string[] newFirstVisitDates = new string[newSize];

            // Copy existing data
            for (int i = 0; i < oldSize; i++)
            {
                newPlayerNames[i] = playerNames[i];
                newVisitCounts[i] = playerVisitCounts[i];
                newFirstVisitDates[i] = playerFirstVisitDates[i];
            }

            // Add new player data
            newPlayerNames[oldSize] = playerName;
            newVisitCounts[oldSize] = 1; // First visit
            newFirstVisitDates[oldSize] = System.DateTime.Now.ToString("yyyy/MM/dd");

            // Replace arrays with expanded versions
            playerNames = newPlayerNames;
            playerVisitCounts = newVisitCounts;
            playerFirstVisitDates = newFirstVisitDates;

            LogDebug($"Added new player: {playerName} on {newFirstVisitDates[oldSize]}");
        }

        // =================================================================
        // PERSISTENCE OPERATIONS
        // Handle saving/loading data to/from VRChat persistence system
        // =================================================================

        /// <summary>
        /// Loads player achievement data from VRChat persistence
        /// Parses JSON string back into usable arrays
        /// </summary>
        private void LoadPlayerDataFromPersistence()
        {
            if (string.IsNullOrEmpty(playerDataJSON))
            {
                // No existing data - initialize empty tracking
                InitializeEmptyPlayerData();
                LogDebug("No existing achievement data found, starting fresh");
            }
            else
            {
                // UdonSharp doesn't support try/catch, so we'll do basic validation
                if (playerDataJSON.Contains("players:"))
                {
                    ParsePlayerDataFromJSON(playerDataJSON);
                    LogDebug($"Loaded achievement data for {playerNames.Length} players");
                }
                else
                {
                    LogDebug("Invalid player data format, starting fresh");
                    InitializeEmptyPlayerData();
                }
            }
        }

        /// <summary>
        /// Saves current player data to VRChat persistence
        /// Converts arrays to JSON string for storage
        /// </summary>
        private void SavePlayerDataToPersistence()
        {
            // UdonSharp doesn't support try/catch, so we'll do basic validation
            string newData = ConvertPlayerDataToJSON();

            if (!string.IsNullOrEmpty(newData))
            {
                playerDataJSON = newData;

                // Request network sync to other clients
                RequestSerialization();

                LogDebug($"Saved achievement data for {playerNames.Length} players");
            }
            else
            {
                LogDebug("Failed to convert player data to JSON");
            }
        }

        /// <summary>
        /// Initializes empty player tracking arrays
        /// Used when no persistence data exists
        /// </summary>
        private void InitializeEmptyPlayerData()
        {
            playerNames = new string[0];
            playerVisitCounts = new int[0];
            playerFirstVisitDates = new string[0];
        }

        /// <summary>
        /// Converts stored JSON string back to usable arrays
        /// Simple parsing implementation for Phase 1
        /// TODO: Implement proper JSON parsing for production
        /// </summary>
        /// <param name="jsonData">JSON string from persistence</param>
        private void ParsePlayerDataFromJSON(string jsonData)
        {
            // =================================================================
            // PHASE 1 SIMPLE IMPLEMENTATION
            // For now, we'll use basic string parsing to get the system working
            // This can be upgraded to proper JSON parsing in later phases
            // =================================================================

            LogDebug("Parsing player data (Phase 1 simple implementation)");

            // For Phase 1, initialize empty data
            // TODO: Implement actual JSON parsing
            InitializeEmptyPlayerData();
        }

        /// <summary>
        /// Converts current player arrays to JSON string for persistence
        /// Simple serialization implementation for Phase 1
        /// TODO: Implement proper JSON serialization for production
        /// </summary>
        /// <returns>JSON string representation of player data</returns>
        private string ConvertPlayerDataToJSON()
        {
            // =================================================================
            // PHASE 1 SIMPLE IMPLEMENTATION
            // For now, we'll use basic string formatting to get the system working
            // This can be upgraded to proper JSON serialization in later phases
            // =================================================================

            LogDebug("Converting player data to JSON (Phase 1 simple implementation)");

            // For Phase 1, return simple string representation
            // TODO: Implement actual JSON serialization
            string result = $"players:{playerNames.Length}";

            return result;
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
        /// Development method to manually clear all achievement data
        /// Only available when debug logging is enabled
        /// WARNING: This will permanently delete all player data
        /// </summary>
        [ContextMenu("Clear All Achievement Data")]
        public void ClearAllAchievementData()
        {
            if (!enableDebugLogging)
            {
                LogDebug("Data clearing only available in debug mode");
                return;
            }

            InitializeEmptyPlayerData();
            playerDataJSON = "";
            RequestSerialization();

            LogDebug("All achievement data cleared");
        }
    }
}