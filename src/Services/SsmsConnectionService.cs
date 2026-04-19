using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Shell;
using SsmsRestoreDrop.Logging;

namespace SsmsRestoreDrop.Services
{
    /// <summary>
    /// Resolves the active SQL Server connection from the SSMS Object Explorer
    /// using runtime reflection so the project compiles without SqlWorkbench.Interfaces.dll.
    /// </summary>
    public sealed class SsmsConnectionService
    {
        private readonly IServiceProvider _serviceProvider;

        public SsmsConnectionService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Returns connection info for the node currently selected in Object Explorer,
        /// or null if nothing is selected / the service is unavailable.
        /// </summary>
        public SqlConnectionStringBuilder? GetActiveConnection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var oeService = ResolveObjectExplorerService();
                if (oeService == null) return null;

                var serviceType = oeService.GetType();

                // GetSelectedNodes(out int nodeCount, out INodeInformation[] nodes)
                var getNodes = serviceType.GetMethod("GetSelectedNodes",
                    BindingFlags.Instance | BindingFlags.Public);
                if (getNodes == null) return null;

                object?[] args = { null, null };
                getNodes.Invoke(oeService, args);

                int nodeCount = args[0] is int c ? c : 0;
                if (nodeCount == 0) return null;

                var nodes = args[1] as Array;
                if (nodes == null || nodes.Length == 0) return null;

                var node = nodes.GetValue(0);
                return ExtractConnectionInfo(node);
            }
            catch (Exception ex)
            {
                Logger.Warn($"GetActiveConnection failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns all server connections currently open in Object Explorer.
        /// </summary>
        public IReadOnlyList<SqlConnectionStringBuilder> GetAllObjectExplorerConnections()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<SqlConnectionStringBuilder>();
            try
            {
                var oeService = ResolveObjectExplorerService();
                if (oeService == null) return result;

                // GetConnectedServers() is available on IObjectExplorerService in SSMS 18+
                var getServers = oeService.GetType().GetMethod("GetConnectedServers",
                    BindingFlags.Instance | BindingFlags.Public);
                if (getServers != null)
                {
                    var servers = getServers.Invoke(oeService, null) as System.Collections.IEnumerable;
                    if (servers != null)
                    {
                        foreach (var srv in servers)
                        {
                            var connInfo = ExtractConnectionInfo(srv);
                            if (connInfo != null) result.Add(connInfo);
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"GetAllObjectExplorerConnections failed: {ex.Message}");
            }
            return result;
        }

        private object? ResolveObjectExplorerService()
        {
            // Look for SqlWorkbench.Interfaces assembly in the current AppDomain
            var sqlWbAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Equals(
                    "SqlWorkbench.Interfaces", StringComparison.OrdinalIgnoreCase) == true);

            if (sqlWbAsm == null)
            {
                Logger.Warn("SqlWorkbench.Interfaces assembly not found - running outside SSMS?");
                return null;
            }

            var serviceType = sqlWbAsm.GetType(
                "Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.IObjectExplorerService");
            if (serviceType == null) return null;

            return _serviceProvider.GetService(serviceType);
        }

        private static SqlConnectionStringBuilder? ExtractConnectionInfo(object? node)
        {
            if (node == null) return null;
            try
            {
                // INodeInformation.Connection is a UIConnectionInfo / SqlOlapConnectionInfoBase
                var connProp = node.GetType().GetProperty("Connection",
                    BindingFlags.Instance | BindingFlags.Public);
                var connObj = connProp?.GetValue(node);
                if (connObj == null) return null;

                return BuildConnectionString(connObj);
            }
            catch (Exception ex)
            {
                Logger.Warn($"ExtractConnectionInfo failed: {ex.Message}");
                return null;
            }
        }

        private static SqlConnectionStringBuilder? BuildConnectionString(object connObj)
        {
            var t = connObj.GetType();

            string? serverName = GetString(connObj, t, "ServerName", "DataSource", "ServerInstance");
            if (string.IsNullOrEmpty(serverName)) return null;

            var csb = new SqlConnectionStringBuilder
            {
                DataSource          = serverName,
                InitialCatalog      = "master",
                ApplicationName     = "SsmsQuickRestore",
                ConnectTimeout      = 30
            };

            bool useIntegrated = GetBool(connObj, t, "UseIntegratedSecurity", "IntegratedSecurity") ?? true;
            if (useIntegrated)
            {
                csb.IntegratedSecurity = true;
            }
            else
            {
                csb.IntegratedSecurity = false;
                csb.UserID             = GetString(connObj, t, "UserName", "UserId") ?? "";
                csb.Password           = GetString(connObj, t, "Password")           ?? "";
            }

            return csb;
        }

        private static string? GetString(object obj, Type t, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (prop != null) return prop.GetValue(obj)?.ToString();
            }
            return null;
        }

        private static bool? GetBool(object obj, Type t, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (prop?.GetValue(obj) is bool b) return b;
            }
            return null;
        }
    }
}
