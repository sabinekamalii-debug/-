using System;
using System.IO;
using MCPForUnity.Editor.Helpers;
using UnityEngine;

namespace MCPForUnity.Editor.Services.Server
{
    /// <summary>
    /// Launches commands in platform-specific terminal windows.
    /// Supports macOS Terminal, Windows cmd, and Linux terminal emulators.
    /// </summary>
    public class TerminalLauncher : ITerminalLauncher
    {
        /// <inheritdoc/>
        public string GetProjectRootPath()
        {
            try
            {
                // Application.dataPath is ".../<Project>/Assets"
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch
            {
                return Application.dataPath;
            }
        }

        /// <inheritdoc/>
        public System.Diagnostics.ProcessStartInfo CreateTerminalProcessStartInfo(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            command = command.Replace("\r", "").Replace("\n", "");

#if UNITY_EDITOR_OSX
            // macOS: Avoid AppleScript (automation permission prompts). Use a .command script and open it.
            string scriptsDir = Path.Combine(GetProjectRootPath(), "Library", "MCPForUnity", "TerminalScripts");
            Directory.CreateDirectory(scriptsDir);
            string scriptPath = Path.Combine(scriptsDir, "mcp-terminal.command");
            File.WriteAllText(
                scriptPath,
                "#!/bin/bash\n" +
                "set -e\n" +
                "clear\n" +
                $"{command}\n");
            ExecPath.TryRun("/bin/chmod", $"+x \"{scriptPath}\"", Application.dataPath, out _, out _, 3000);
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                Arguments = $"-a Terminal \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
#elif UNITY_EDITOR_WIN
            // Windows: 使用更可靠的方式启动终端窗口
            string scriptsDir = Path.Combine(GetProjectRootPath(), "Library", "MCPForUnity", "TerminalScripts");
            Directory.CreateDirectory(scriptsDir);
            string scriptPath = Path.Combine(scriptsDir, "mcp-terminal.cmd");
            // 确保RunState目录存在
            string runStateDir = Path.Combine(GetProjectRootPath(), "Library", "MCPForUnity", "RunState");
            Directory.CreateDirectory(runStateDir);
            File.WriteAllText(
                scriptPath,
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +  // 设置UTF-8代码页以支持中文路径
                "cls\r\n" +
                command + "\r\n" +
                "if errorlevel 1 (\r\n" +
                "    echo.\r\n" +
                "    echo Command failed with error level: %errorlevel%\r\n" +
                ")\r\n" +
                "echo.\r\n" +
                "echo Press any key to exit...\r\n" +
                "pause >nul\r\n");
            // 使用cmd.exe /k来启动，确保工作目录正确
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{scriptPath}\"",
                WorkingDirectory = GetProjectRootPath(),
                UseShellExecute = true,
                CreateNoWindow = false
            };
#else
            // Linux: Try common terminal emulators
            // We use bash -c to execute the command, so we must properly quote/escape for bash
            // Escape single quotes for the inner bash string
            string escapedCommandLinux = command.Replace("'", "'\\''");
            // Wrap the command in single quotes for bash -c
            string script = $"'{escapedCommandLinux}; exec bash'";
            // Escape double quotes for the outer Process argument string
            string escapedScriptForArg = script.Replace("\"", "\\\"");
            string bashCmdArgs = $"bash -c \"{escapedScriptForArg}\"";

            string[] terminals = { "gnome-terminal", "xterm", "konsole", "xfce4-terminal" };
            string terminalCmd = null;

            foreach (var term in terminals)
            {
                try
                {
                    var which = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = term,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    which.WaitForExit(5000); // Wait for up to 5 seconds, the command is typically instantaneous
                    if (which.ExitCode == 0)
                    {
                        terminalCmd = term;
                        break;
                    }
                }
                catch { }
            }

            if (terminalCmd == null)
            {
                terminalCmd = "xterm"; // Fallback
            }

            // Different terminals have different argument formats
            string args;
            if (terminalCmd == "gnome-terminal")
            {
                args = $"-- {bashCmdArgs}";
            }
            else if (terminalCmd == "konsole")
            {
                args = $"-e {bashCmdArgs}";
            }
            else if (terminalCmd == "xfce4-terminal")
            {
                // xfce4-terminal expects -e "command string" or -e command arg
                args = $"--hold -e \"{bashCmdArgs.Replace("\"", "\\\"")}\"";
            }
            else // xterm and others
            {
                args = $"-hold -e {bashCmdArgs}";
            }

            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = terminalCmd,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#endif
        }
    }
}
