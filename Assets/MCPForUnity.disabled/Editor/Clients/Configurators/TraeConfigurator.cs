using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class TraeConfigurator : JsonFileMcpConfigurator
    {
        public TraeConfigurator() : base(BuildClient())
        { }

        private static McpClient BuildClient()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Candidate paths on Windows: Trae CN\User\mcp.json (CN distro),
            // Trae\mcp.json (standard). Pick the first that exists; fall back
            // to the standard Trae path so Configure() can create it.
            string windowsPath = SelectExistingPath(
                Path.Combine(appData, "Trae CN", "User", "mcp.json"),
                Path.Combine(appData, "Trae", "User", "mcp.json"),
                Path.Combine(appData, "Trae", "mcp.json")
            );

            string macPath = SelectExistingPath(
                Path.Combine(userProfile, "Library", "Application Support", "Trae CN", "User", "mcp.json"),
                Path.Combine(userProfile, "Library", "Application Support", "Trae", "mcp.json")
            );

            string linuxPath = SelectExistingPath(
                Path.Combine(userProfile, ".config", "Trae CN", "User", "mcp.json"),
                Path.Combine(userProfile, ".config", "Trae", "mcp.json")
            );

            return new McpClient
            {
                name = "Trae",
                windowsConfigPath = windowsPath,
                macConfigPath = macPath,
                linuxConfigPath = linuxPath,
            };
        }

        private static string SelectExistingPath(params string[] candidates)
        {
            foreach (string p in candidates)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    return p;
            }
            return candidates.Length > 0 ? candidates[candidates.Length - 1] : null;
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Open Trae and go to Settings > MCP",
            "Select Add Server > Add Manually",
            "Paste the JSON or point to the mcp.json file\n"+
                "Windows: %AppData%\\Trae CN\\User\\mcp.json (or %AppData%\\Trae\\mcp.json)\n" +
                "macOS: ~/Library/Application Support/Trae/mcp.json\n" +
                "Linux: ~/.config/Trae/mcp.json\n",
            "Save and restart Trae"
        };
    }
}
