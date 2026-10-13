using System.Diagnostics;

namespace Todo.Tui.Input;

public enum AltKeyMode
{
    Auto,
    On,
    Off
}

public enum AltKeyStatus
{
    Confirmed,
    Unconfirmed
}

/// <summary>
/// Decides whether Alt+letter shortcuts can be relied on. macOS terminals often capture Option instead of
/// sending it as Alt/Meta, so when that cannot be confirmed the UI advertises Ctrl-based alternatives.
/// Logic follows the checkOptionasMetaKey snippet in ~/.agents/snippets.
/// </summary>
public static class AltKeySupport
{
    public static AltKeyStatus Evaluate(
        AltKeyMode configured,
        bool isMacOs,
        string? termProgram,
        Func<bool?> readAppleTerminalOptionAsMeta)
    {
        switch (configured)
        {
            case AltKeyMode.On:
                return AltKeyStatus.Confirmed;
            case AltKeyMode.Off:
                return AltKeyStatus.Unconfirmed;
        }

        if (!isMacOs)
            return AltKeyStatus.Confirmed;

        // Only Apple's Terminal exposes its Option-as-Meta setting in a readable preference store; other macOS
        // terminals (iTerm2, kitty, Ghostty, ...) cannot be probed safely, so they stay unconfirmed.
        return termProgram == "Apple_Terminal" && readAppleTerminalOptionAsMeta() == true
            ? AltKeyStatus.Confirmed
            : AltKeyStatus.Unconfirmed;
    }

    public static AltKeyStatus Detect(AltKeyMode configured) => Evaluate(
        configured,
        OperatingSystem.IsMacOS(),
        Environment.GetEnvironmentVariable("TERM_PROGRAM"),
        ReadAppleTerminalOptionAsMeta);

    /// <summary>
    /// Reads useOptionAsMetaKey for Terminal.app's default profile. Returns null when it cannot be determined.
    /// The profile of the current tab is not queried: that needs AppleScript and triggers a permission prompt.
    /// </summary>
    internal static bool? ReadAppleTerminalOptionAsMeta()
    {
        try
        {
            var profile = Run("defaults", ["read", "com.apple.Terminal", "Default Window Settings"], null)?.Trim();
            if (string.IsNullOrEmpty(profile))
                return null;

            var windowSettings = Run("defaults", ["read", "com.apple.Terminal", "Window Settings"], null);
            if (string.IsNullOrEmpty(windowSettings))
                return null;

            // plutil key paths use '.' as a separator, so dots inside the profile name must be escaped.
            var keyPath = profile.Replace(".", "\\.") + ".useOptionAsMetaKey";
            var value = Run("plutil", ["-extract", keyPath, "raw", "-"], windowSettings)?.Trim();
            return value switch
            {
                "1" or "true" => true,
                "0" or "false" => false,
                _ => null
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static string? Run(string fileName, string[] arguments, string? standardInput)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            return null;

        var output = process.StandardOutput.ReadToEndAsync();
        process.StandardError.ReadToEndAsync();
        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        if (!process.WaitForExit(2000))
        {
            process.Kill();
            return null;
        }

        return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
    }
}
