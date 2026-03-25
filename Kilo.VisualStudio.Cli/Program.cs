using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Kilo.VisualStudio.App.Services;

static void PrintUsage()
{
    Console.WriteLine("Kilo Visual Studio CLI - kilo-vs");
    Console.WriteLine("Usage:");
    Console.WriteLine("  kilo-vs status [<workspace>]                 - show mode/server status");
    Console.WriteLine("  kilo-vs mode list [<workspace>]              - list available agent modes");
    Console.WriteLine("  kilo-vs mode set <mode> [<workspace>]        - set active mode");
    Console.WriteLine("  kilo-vs mcp list [<workspace>]               - list known MCP servers");
    Console.WriteLine("  kilo-vs mcp query <url> [<workspace>]        - fetch community MCP servers from URL");
    Console.WriteLine("  kilo-vs sln info <path>                      - parse .sln for project list");
    Console.WriteLine("  kilo-vs proj info <path>                     - parse project file for target frameworks");
    Console.WriteLine("  kilo-vs headless <workspace> --run <script>  - run a script in CI mode (stub)");
    Console.WriteLine("  kilo-vs help                                 - show this help text");
}

static string ResolveWorkspaceRoot(string? arg)
{
    return string.IsNullOrWhiteSpace(arg) ? Directory.GetCurrentDirectory() : Path.GetFullPath(arg);
}

var jsonOutput = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
var argv = args.Where(a => !string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)).ToArray();

if (argv.Length == 0 || argv[0] == "help" || argv[0] == "--help" || argv[0] == "-h")
{
    PrintUsage();
    return 0;
}

var cmd = argv[0].ToLowerInvariant();

try
{
    switch (cmd)
    {
        case "status":
        {
            var root = ResolveWorkspaceRoot(argv.Length > 1 ? argv[1] : null);
            var mode = new AgentModeService(root);
            using var mcp = new McpHubService(root);

            if (jsonOutput)
            {
                var response = new
                {
                    Workspace = root,
                    CurrentMode = mode.CurrentMode.ToString(),
                    CurrentModeName = mode.CurrentModeDefinition.Name,
                    McpServers = mcp.Servers.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Description,
                        s.Status,
                        s.Score,
                        s.Tags,
                        s.DocumentationUrl
                    }).ToArray()
                };
                Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine($"Workspace: {root}");
            Console.WriteLine($"Current mode: {mode.CurrentMode} ({mode.CurrentModeDefinition.Name})");
            Console.WriteLine("MCP servers:");
            foreach (var server in mcp.Servers)
            {
                Console.WriteLine($"  {server.Id} - {server.Name} - {server.Status} - {server.Score:F1} [{string.Join(',', server.Tags)}]");
            }
            break;
        }

        case "mode":
        {
            if (argv.Length < 2)
            {
                Console.WriteLine("mode requires subcommand: list|set");
                return 1;
            }

            var sub = argv[1].ToLowerInvariant();
            var root = ResolveWorkspaceRoot(argv.Length > 2 ? argv[2] : null);
            var mode = new AgentModeService(root);

            if (sub == "list")
            {
                foreach (var option in mode.GetAvailableModes())
                {
                    var def = mode.GetModeDefinition(option);
                    Console.WriteLine($"{option} - {def.Name} {def.Icon}");
                }
                return 0;
            }

            if (sub == "set")
            {
                if (argv.Length < 3)
                {
                    Console.WriteLine("mode set requires a mode name");
                    return 1;
                }
                var modeName = argv[2];
                mode.SetMode(modeName);
                Console.WriteLine($"Mode set to: {mode.CurrentMode} ({mode.CurrentModeDefinition.Name})");
                return 0;
            }

            Console.WriteLine("Unknown mode subcommand");
            return 1;
        }

        case "mcp":
        {
            if (argv.Length < 2)
            {
                Console.WriteLine("mcp requires subcommand: list|query");
                return 1;
            }
            var sub = argv[1].ToLowerInvariant();
            var root = ResolveWorkspaceRoot(argv.Length > 2 ? argv[2] : null);
            using var hub = new McpHubService(root);

            if (sub == "list")
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(hub.Servers.OrderBy(x => x.Name), new JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }

                foreach (var s in hub.Servers.OrderBy(x => x.Name))
                {
                    Console.WriteLine($"{s.Id}\t{s.Name}\t{s.Description}\t{s.Status}\t{s.Score:F1}");
                }
                return 0;
            }

            if (sub == "query")
            {
                if (argv.Length < 3)
                {
                    Console.WriteLine("mcp query requires a URL");
                    return 1;
                }
                var url = argv[2];
                var remote = hub.QueryRemoteCommunityServersAsync(url).GetAwaiter().GetResult();

                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { url, imported = remote.Count }, new JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }

                Console.WriteLine($"Imported {remote.Count} servers from {url}");
                return 0;
            }

            Console.WriteLine("Unknown mcp subcommand");
            return 1;
        }

        case "sln":
        {
            if (argv.Length < 2 || argv[1].ToLowerInvariant() != "info" || argv.Length < 3)
            {
                Console.WriteLine("sln info <path>");
                return 1;
            }
            var sln = argv[2];
            if (!File.Exists(sln))
            {
                Console.WriteLine($"Solution not found: {sln}");
                return 2;
            }

            var lines = File.ReadAllLines(sln);
            foreach (var line in lines)
            {
                if (line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                    {
                        var project = parts[1].Trim();
                        Console.WriteLine(project);
                    }
                }
            }
            return 0;
        }

        case "proj":
        {
            if (argv.Length < 2 || argv[1].ToLowerInvariant() != "info" || argv.Length < 3)
            {
                Console.WriteLine("proj info <path>");
                return 1;
            }
            var proj = argv[2];
            if (!File.Exists(proj))
            {
                Console.WriteLine($"Project not found: {proj}");
                return 2;
            }
            var doc = XDocument.Load(proj);
            var targetFramework = doc.Root?.Elements("PropertyGroup").Elements("TargetFramework").FirstOrDefault()?.Value;
            var tfms = doc.Root?.Elements("PropertyGroup").Elements("TargetFrameworks").FirstOrDefault()?.Value;

            Console.WriteLine($"Project: {proj}");
            Console.WriteLine($"TargetFramework: {targetFramework}");
            Console.WriteLine($"TargetFrameworks: {tfms}");
            return 0;
        }

        case "headless":
        {
            var root = ResolveWorkspaceRoot(argv.Length > 1 ? argv[1] : null);
            var scriptArg = argv.FirstOrDefault(a => a == "--run");
            if (scriptArg == null)
            {
                Console.WriteLine("headless requires --run <script>");
                return 1;
            }
            var scriptIndex = Array.IndexOf(argv, "--run");
            if (scriptIndex < 0 || scriptIndex + 1 >= argv.Length)
            {
                Console.WriteLine("headless requires --run <script>");
                return 1;
            }
            var script = argv[scriptIndex + 1];
            Console.WriteLine($"Running headless script '{script}' at {root}");
            // stubbed behavior for CI pipeline stub
            await Task.Delay(100);
            Console.WriteLine("Done.");
            return 0;
        }

        default:
            Console.WriteLine($"Unknown command {cmd}");
            PrintUsage();
            return 1;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 99;
}

return 0;
