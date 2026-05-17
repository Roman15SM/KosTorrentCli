using System;
using System.Linq;
using NetFwTypeLib;

namespace KosTorrentCli.Server
{
    public static class TcpRule
    {
        private static readonly string RuleName = "KosTorrentCli";

        public static void AddTcpRule()
        {
            var path = $@"{AppContext.BaseDirectory}KosTorrentCli.exe";

            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(
                Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            INetFwRule firewallRule = firewallPolicy
                .Rules
                .OfType<INetFwRule>()?
                .Where(x => x.Name == RuleName).FirstOrDefault();

            if (firewallRule == null)
            {
                AddRule(firewallPolicy, NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE, NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN, path);
                AddRule(firewallPolicy, NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC, NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN, path);
            }
        }

        private static void AddRule(INetFwPolicy2 firewallPolicy, NET_FW_PROFILE_TYPE2_ profile, NET_FW_RULE_DIRECTION_ direction, string path)
        {
            INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            firewallRule.Name = RuleName;
            firewallRule.Description = "KosTorrentCli inbound TCP rule";
            firewallRule.ApplicationName = path;
            firewallRule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            firewallRule.LocalPorts = "*";
            firewallRule.Direction = direction;
            firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            firewallRule.Enabled = true;
            firewallRule.EdgeTraversal = false;
            firewallRule.RemoteAddresses = "*";
            firewallRule.RemotePorts = "*";
            firewallRule.Profiles = (int)profile;
            firewallRule.InterfaceTypes = "All";

            firewallPolicy.Rules.Add(firewallRule);
        }
    }
}
