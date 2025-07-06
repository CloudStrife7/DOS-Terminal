using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class DOSTerminalController : UdonSharpBehaviour
{
    [Header("Terminal Display")]
    public TextMeshProUGUI terminalDisplay;
    public TextMeshProUGUI bootDisplay;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip bootBeepSound;
    public AudioClip fanSound;
    public AudioClip keystrokeSound;
    public AudioClip celebrationChime;

    [Header("Auto Cycle Settings")]
    public bool enableAutoCycle = true;
    public float cycleDelaySeconds = 10.0f;

    [Header("Settings")]
    public float bootSequenceDelay = 0.5f;

    // Internal state
    private bool isBooted = false;
    private bool isBlinkingCursor = false;
    private string currentTerminalText = "";
    private int currentPage = 0; // 0=directory, 1=scores, 2=changelog

    // Boot sequence text
    private string[] bootSequence = {
        "BASEMENT OS v2.1",
        "Copyright (c) 2025 Lower Level Systems",
        "",
        "Initializing memory...",
        "Loading system drivers...",
        "Checking network connectivity...",
        "Mounting file systems...",
        "Starting background services...",
        "",
        "Boot sequence complete.",
        "Welcome to BASEMENT OS",
        ""
    };

    void Start()
    {
        // Start boot sequence
        StartBootSequence();
    }

    public void StartBootSequence()
    {
        // Play boot beep
        if (audioSource && bootBeepSound)
        {
            audioSource.PlayOneShot(bootBeepSound);
        }

        // Start fan sound (looping)
        if (audioSource && fanSound)
        {
            audioSource.clip = fanSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Show boot display, hide terminal
        if (bootDisplay) bootDisplay.gameObject.SetActive(true);
        if (terminalDisplay) terminalDisplay.gameObject.SetActive(false);

        // Start boot text sequence
        SendCustomEventDelayedSeconds(nameof(ShowNextBootLine), bootSequenceDelay);
    }

    private int currentBootLine = 0;

    public void ShowNextBootLine()
    {
        if (currentBootLine < bootSequence.Length)
        {
            // Add current line to boot display
            if (bootDisplay)
            {
                if (currentBootLine == 0)
                {
                    bootDisplay.text = bootSequence[currentBootLine];
                }
                else
                {
                    bootDisplay.text += "\n" + bootSequence[currentBootLine];
                }
            }

            // Play keystroke sound
            if (audioSource && keystrokeSound)
            {
                audioSource.PlayOneShot(keystrokeSound);
            }

            currentBootLine++;
            SendCustomEventDelayedSeconds(nameof(ShowNextBootLine), bootSequenceDelay);
        }
        else
        {
            // Boot complete - switch to terminal
            SendCustomEventDelayedSeconds(nameof(CompleteBootSequence), 1.0f);
        }
    }

    public void CompleteBootSequence()
    {
        // Hide boot display, show terminal
        if (bootDisplay) bootDisplay.gameObject.SetActive(false);
        if (terminalDisplay) terminalDisplay.gameObject.SetActive(true);

        // Show directory listing
        ShowDirectoryListing();

        // Start cursor blinking
        StartBlinkingCursor();

        // Set booted flag
        isBooted = true;

        // Start auto-cycle if enabled
        if (enableAutoCycle)
        {
            SendCustomEventDelayedSeconds(nameof(AutoCycleContent), cycleDelaySeconds);
        }
    }

    public void ShowDirectoryListing()
    {
        currentTerminalText = "C:\\BASEMENT> dir\n" +
                             "Volume in drive C is NOSTALGIA\n" +
                             "Directory of C:\\BASEMENT\\PROJECTS\n\n" +
                             "BASEMENT        <DIR>         100% COMPLETE\n" +
                             "UPGRADES        <DIR>         IN PROGRESS\n" +
                             "MAINLEVEL       <DIR>         AWAITING FUNDING\n" +
                             "SNAKE.EXE                     LOADED\n\n" +
                             "===============================================\n" +
                             "Auto-cycling content every " + cycleDelaySeconds + " seconds\n" +
                             "===============================================\n\n" +
                             "C:\\BASEMENT> ";

        if (terminalDisplay)
        {
            terminalDisplay.text = currentTerminalText;
        }

        currentPage = 0;
    }

    public void AutoCycleContent()
    {
        if (!isBooted || !enableAutoCycle) return;

        // Cycle through pages: Directory -> Scores -> Changelog -> Directory
        currentPage = (currentPage + 1) % 3;

        isBlinkingCursor = false;

        switch (currentPage)
        {
            case 0:
                ShowDirectoryListing();
                break;
            case 1:
                ExecuteScoresCommand();
                return; // ExecuteScoresCommand handles the next cycle
            case 2:
                ExecuteChangelogCommand();
                return; // ExecuteChangelogCommand handles the next cycle
        }

        StartBlinkingCursor();

        // Schedule next cycle
        SendCustomEventDelayedSeconds(nameof(AutoCycleContent), cycleDelaySeconds);
    }

    public void StartBlinkingCursor()
    {
        if (!isBlinkingCursor)
        {
            isBlinkingCursor = true;
            SendCustomEventDelayedSeconds(nameof(BlinkCursor), 0.5f);
        }
    }

    public void BlinkCursor()
    {
        if (isBlinkingCursor && terminalDisplay)
        {
            // Toggle cursor
            if (terminalDisplay.text.EndsWith("_"))
            {
                terminalDisplay.text = currentTerminalText;
            }
            else
            {
                terminalDisplay.text = currentTerminalText + "_";
            }

            // Schedule next blink
            SendCustomEventDelayedSeconds(nameof(BlinkCursor), 0.5f);
        }
    }

    // Command buttons call these methods
    public void ExecuteScoresCommand()
    {
        if (!isBooted) return;

        PlayKeystrokeSound();
        isBlinkingCursor = false;

        currentTerminalText = "C:\\BASEMENT> scores\n\n" +
                             "===== BASEMENT ARCADE HIGH SCORES =====\n\n" +
                             "1st    ALEX..........500    BAS\n" +
                             "2nd    PETER.........350    SNK\n" +
                             "3rd    BOBBY..........200    HTB\n" +
                             "Thank you to all supporters!\n\n" +
                             "C:\\BASEMENT> ";

        if (terminalDisplay)
        {
            terminalDisplay.text = currentTerminalText;
        }

        currentPage = 1;
        StartBlinkingCursor();

        // Schedule next cycle if auto-cycle is enabled
        if (enableAutoCycle)
        {
            SendCustomEventDelayedSeconds(nameof(AutoCycleContent), cycleDelaySeconds);
        }
    }

    public void ExecuteChangelogCommand()
    {
        if (!isBooted) return;

        PlayKeystrokeSound();
        isBlinkingCursor = false;

        currentTerminalText = "C:\\BASEMENT> changelog\n\n" +
                             "===============================================\n" +
                             "BASEMENT PROJECT - CHANGELOG v2.1\n" +
                             "===============================================\n\n" +
                             "[2025-07-03] v2.1 - Added DOS terminal w auto-cycle\n" +
                             "[2025-07-03] v2.0 - LL 2.0 Desktop Version Complete\n" +
                             "[2025-03-26] v1.5 - LL 2.0 Dev Hired w Contract\n" +
                             "[2022-02-13] v1.0 - OG Lower Level released\n\n" +
                             "===============================================\n\n" +
                             "C:\\BASEMENT> ";

        if (terminalDisplay)
        {
            terminalDisplay.text = currentTerminalText;
        }

        currentPage = 2;
        StartBlinkingCursor();

        // Schedule next cycle if auto-cycle is enabled
        if (enableAutoCycle)
        {
            SendCustomEventDelayedSeconds(nameof(AutoCycleContent), cycleDelaySeconds);
        }
    }

    public void ExecuteHelpCommand()
    {
        if (!isBooted) return;

        PlayKeystrokeSound();
        isBlinkingCursor = false;

        currentTerminalText = "C:\\BASEMENT> help\n\n" +
                             "Available Commands:\n" +
                             "DIR          - Show directory listing\n" +
                             "SCORES       - Show project supporters\n" +
                             "CHANGELOG    - Show project updates\n" +
                             "HELP         - Show this help\n" +
                             "CLEAR        - Clear screen\n\n" +
                             "Auto-cycle: " + (enableAutoCycle ? "ENABLED" : "DISABLED") + "\n" +
                             "Cycle delay: " + cycleDelaySeconds + " seconds\n\n" +
                             "C:\\BASEMENT> ";

        if (terminalDisplay)
        {
            terminalDisplay.text = currentTerminalText;
        }

        StartBlinkingCursor();
    }

    public void ExecuteDirCommand()
    {
        if (!isBooted) return;

        PlayKeystrokeSound();
        isBlinkingCursor = false;

        ShowDirectoryListing();
        StartBlinkingCursor();
    }

    public void ExecuteClearCommand()
    {
        if (!isBooted) return;

        PlayKeystrokeSound();
        isBlinkingCursor = false;

        currentTerminalText = "C:\\BASEMENT> ";
        if (terminalDisplay)
        {
            terminalDisplay.text = currentTerminalText;
        }

        currentPage = 0;
        StartBlinkingCursor();
    }

    private void PlayKeystrokeSound()
    {
        if (audioSource && keystrokeSound)
        {
            audioSource.PlayOneShot(keystrokeSound);
        }
    }
}