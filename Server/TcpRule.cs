using System;
using System.Runtime.Versioning;

namespace KosTorrentCli.Server
{
    /// <summary>
    /// Windows Firewall rule management via late-bound COM (HNetCfg.FwPolicy2).
    /// `dynamic` is used instead of NetFwTypeLib COM reference, because COM references
    /// can't be built by the dotnet CLI (VS Code, CI, Linux).
    /// </summary>
    public static class TcpRule
    {
        private static readonly string RuleName = "KosTorrentCli";

        //values of NET_FW_* enums from NetFwTypeLib
        private const int ProfilePrivate = 2;
        private const int ProfilePublic = 4;
        private const int DirectionIn = 1;
        private const int ProtocolTcp = 6;
        private const int ActionAllow = 1;

        /// <summary>
        /// Inbound rule is needed only for incoming peer connections (seeding).
        /// Adding a firewall rule requires administrator rights, so download must not fail without it.
        /// </summary>
        public static void AddTcpRule()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                AddTcpRuleInternal();
            }
            catch (Exception e)
            {
                Log.Warning($"Firewall rule was not added (run as administrator to add it): {e.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void AddTcpRuleInternal()
        {
            var path = $@"{AppContext.BaseDirectory}KosTorrentCli.exe";

            dynamic firewallPolicy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            foreach (dynamic rule in firewallPolicy.Rules)
            {
                if (rule.Name == RuleName)
                    return;
            }

            AddRule(firewallPolicy, ProfilePrivate, DirectionIn, path);
            AddRule(firewallPolicy, ProfilePublic, DirectionIn, path);
        }

        [SupportedOSPlatform("windows")]
        private static void AddRule(dynamic firewallPolicy, int profile, int direction, string path)
        {
            dynamic firewallRule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            firewallRule.Name = RuleName;
            firewallRule.Description = "KosTorrentCli inbound TCP rule";
            firewallRule.ApplicationName = path;
            firewallRule.Protocol = ProtocolTcp;
            firewallRule.LocalPorts = "*";
            firewallRule.Direction = direction;
            firewallRule.Action = ActionAllow;
            firewallRule.Enabled = true;
            firewallRule.EdgeTraversal = false;
            firewallRule.RemoteAddresses = "*";
            firewallRule.RemotePorts = "*";
            firewallRule.Profiles = profile;
            firewallRule.InterfaceTypes = "All";

            firewallPolicy.Rules.Add(firewallRule);
        }
    }
}
