using System.Text.Json;
using System.Text.Json.Nodes;

var projectRoot = Path.GetFullPath(
    Environment.GetEnvironmentVariable("UNITY_PROJECT_ROOT")
    ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
    ?? Directory.GetCurrentDirectory());

var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

var SafeWriteExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".cs", ".json", ".txt", ".md", ".asmdef", ".shader", ".uxml", ".uss"
};

string SafePath(string relativePath)
{
    var combined = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    if (!combined.StartsWith(Path.GetFullPath(projectRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        && combined != Path.GetFullPath(projectRoot))
        throw new InvalidOperationException($"Path escapes project root: {relativePath}");
    return combined;
}

object MakeTextResult(object? id, string text) => new
{
    jsonrpc = "2.0",
    id,
    result = new
    {
        content = new object[] { new { type = "text", text } }
    }
};

object Success(object? id, object result) => new { jsonrpc = "2.0", id, result };

object Error(object? id, int code, string message) => new
{
    jsonrpc = "2.0",
    id,
    error = new { code, message }
};

void Send(object payload) => Console.WriteLine(JsonSerializer.Serialize(payload, jsonOptions));

while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) continue;

    try
    {
        var msg = JsonNode.Parse(line)?.AsObject();
        if (msg is null) continue;

        var idNode = msg["id"];
        object? id = idNode is null ? null : JsonSerializer.Deserialize<object>(idNode.ToJsonString());
        var method = msg["method"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(method))
        {
            Send(Error(id, -32600, "Missing method"));
            continue;
        }

        switch (method)
        {
            case "initialize":
            {
                Send(Success(id, new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "unity-mcp-server", version = "0.2.0" }
                }));
                break;
            }

            case "notifications/initialized":
                break;

            case "tools/list":
            {
                Send(Success(id, new
                {
                    tools = new object[]
                    {
                        new
                        {
                            name = "get_project_root",
                            description = "Returns the detected Unity project root path.",
                            inputSchema = new { type = "object", properties = new { } }
                        },
                        new
                        {
                            name = "list_files",
                            description = "List files under the Unity project root. Optional subpath, fileExtension (e.g. .cs), and maxResults filters.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    subpath = new { type = "string", description = "Folder relative to the project root, e.g. Assets or Packages." },
                                    fileExtension = new { type = "string", description = "Filter by extension, e.g. .cs or .unity" },
                                    maxResults = new { type = "integer", description = "Max number of results (default 500)." }
                                }
                            }
                        },
                        new
                        {
                            name = "read_file",
                            description = "Read a text file from the Unity project.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    path = new { type = "string", description = "Relative path, e.g. Assets/Scripts/PlayerController.cs" }
                                },
                                required = new[] { "path" }
                            }
                        },
                        new
                        {
                            name = "write_file",
                            description = $"Write text to a file in the project. Allowed extensions: {string.Join(", ", SafeWriteExtensions)}. Creates or overwrites the file.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    path = new { type = "string", description = "Relative path to write, e.g. Assets/Scripts/Foo.cs" },
                                    content = new { type = "string", description = "Text content to write." }
                                },
                                required = new[] { "path", "content" }
                            }
                        },
                        new
                        {
                            name = "edit_file",
                            description = "Replace exact text in a file with new text. Fails if oldText is not found, or found more than once unless allowMultiple is true.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    path = new { type = "string", description = "Relative file path." },
                                    oldText = new { type = "string", description = "Exact text to find and replace." },
                                    newText = new { type = "string", description = "Replacement text." },
                                    allowMultiple = new { type = "boolean", description = "If true, replace all occurrences (default false)." }
                                },
                                required = new[] { "path", "oldText", "newText" }
                            }
                        },
                        new
                        {
                            name = "search_files",
                            description = "Search text files under a subpath for a query string. Returns matching file paths and line numbers.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    query = new { type = "string", description = "String to search for (case-insensitive)." },
                                    subpath = new { type = "string", description = "Folder to search under (default: project root)." },
                                    fileExtension = new { type = "string", description = "Limit search to this extension, e.g. .cs" }
                                },
                                required = new[] { "query" }
                            }
                        },
                        new
                        {
                            name = "list_csharp_scripts",
                            description = "List all .cs files under the Assets folder.",
                            inputSchema = new { type = "object", properties = new { } }
                        },
                        new
                        {
                            name = "list_unity_scenes",
                            description = "List all .unity scene files under the Assets folder.",
                            inputSchema = new { type = "object", properties = new { } }
                        },
                        new
                        {
                            name = "read_package_manifest",
                            description = "Read Packages/manifest.json from the project.",
                            inputSchema = new { type = "object", properties = new { } }
                        },
                        new
                        {
                            name = "read_editor_log",
                            description = "Read the last 400 lines of the Unity Editor log from the current Windows user profile.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    lines = new { type = "integer", description = "Number of trailing lines to return (default 400, max 2000)." }
                                }
                            }
                        }
                    }
                }));
                break;
            }

            case "tools/call":
            {
                var requestParams = msg["params"]?.AsObject();
                var toolName = requestParams?["name"]?.GetValue<string>();
                var toolArgs = requestParams?["arguments"]?.AsObject();

                if (string.IsNullOrWhiteSpace(toolName))
                {
                    Send(Error(id, -32602, "Missing tool name"));
                    break;
                }

                switch (toolName)
                {
                    case "get_project_root":
                    {
                        Send(MakeTextResult(id, projectRoot));
                        break;
                    }

                    case "list_files":
                    {
                        var subpath = toolArgs?["subpath"]?.GetValue<string>() ?? ".";
                        var ext = toolArgs?["fileExtension"]?.GetValue<string>();
                        var maxResults = toolArgs?["maxResults"]?.GetValue<int>() ?? 500;
                        maxResults = Math.Clamp(maxResults, 1, 5000);

                        var folder = SafePath(subpath);
                        if (!Directory.Exists(folder))
                        {
                            Send(Error(id, -32004, $"Folder not found: {subpath}"));
                            break;
                        }

                        var pattern = string.IsNullOrWhiteSpace(ext) ? "*" : $"*{ext}";
                        var files = Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(projectRoot, f).Replace("\\", "/"))
                            .Take(maxResults)
                            .ToArray();

                        Send(MakeTextResult(id, string.Join("\n", files)));
                        break;
                    }

                    case "read_file":
                    {
                        var relPath = toolArgs?["path"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(relPath))
                        {
                            Send(Error(id, -32602, "Missing path"));
                            break;
                        }

                        var filePath = SafePath(relPath);
                        if (!File.Exists(filePath))
                        {
                            Send(Error(id, -32004, $"File not found: {relPath}"));
                            break;
                        }

                        Send(MakeTextResult(id, File.ReadAllText(filePath)));
                        break;
                    }

                    case "write_file":
                    {
                        var relPath = toolArgs?["path"]?.GetValue<string>();
                        var content = toolArgs?["content"]?.GetValue<string>();

                        if (string.IsNullOrWhiteSpace(relPath))
                        {
                            Send(Error(id, -32602, "Missing path"));
                            break;
                        }

                        if (content is null)
                        {
                            Send(Error(id, -32602, "Missing content"));
                            break;
                        }

                        var ext = Path.GetExtension(relPath);
                        if (!SafeWriteExtensions.Contains(ext))
                        {
                            Send(Error(id, -32003, $"Extension '{ext}' is not allowed for write_file. Allowed: {string.Join(", ", SafeWriteExtensions)}"));
                            break;
                        }

                        var filePath = SafePath(relPath);
                        var dir = Path.GetDirectoryName(filePath)!;
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        File.WriteAllText(filePath, content);
                        Send(MakeTextResult(id, $"Written {relPath} ({content.Length} chars)"));
                        break;
                    }

                    case "edit_file":
                    {
                        var relPath = toolArgs?["path"]?.GetValue<string>();
                        var oldText = toolArgs?["oldText"]?.GetValue<string>();
                        var newText = toolArgs?["newText"]?.GetValue<string>();
                        var allowMultiple = toolArgs?["allowMultiple"]?.GetValue<bool>() ?? false;

                        if (string.IsNullOrWhiteSpace(relPath)) { Send(Error(id, -32602, "Missing path")); break; }
                        if (oldText is null) { Send(Error(id, -32602, "Missing oldText")); break; }
                        if (newText is null) { Send(Error(id, -32602, "Missing newText")); break; }

                        var filePath = SafePath(relPath);
                        if (!File.Exists(filePath))
                        {
                            Send(Error(id, -32004, $"File not found: {relPath}"));
                            break;
                        }

                        var original = File.ReadAllText(filePath);
                        var count = CountOccurrences(original, oldText);

                        if (count == 0)
                        {
                            Send(Error(id, -32005, $"oldText not found in {relPath}"));
                            break;
                        }

                        if (count > 1 && !allowMultiple)
                        {
                            Send(Error(id, -32005, $"oldText appears {count} times in {relPath}. Set allowMultiple=true to replace all occurrences."));
                            break;
                        }

                        var updated = original.Replace(oldText, newText);
                        File.WriteAllText(filePath, updated);
                        Send(MakeTextResult(id, $"Replaced {count} occurrence(s) in {relPath}"));
                        break;
                    }

                    case "search_files":
                    {
                        var query = toolArgs?["query"]?.GetValue<string>();
                        var subpath = toolArgs?["subpath"]?.GetValue<string>() ?? ".";
                        var ext = toolArgs?["fileExtension"]?.GetValue<string>();

                        if (string.IsNullOrWhiteSpace(query))
                        {
                            Send(Error(id, -32602, "Missing query"));
                            break;
                        }

                        var folder = SafePath(subpath);
                        if (!Directory.Exists(folder))
                        {
                            Send(Error(id, -32004, $"Folder not found: {subpath}"));
                            break;
                        }

                        var pattern = string.IsNullOrWhiteSpace(ext) ? "*" : $"*{ext}";
                        var results = new List<string>();

                        foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories))
                        {
                            // Skip known binary-heavy extensions
                            var fileExt = Path.GetExtension(file).ToLowerInvariant();
                            if (fileExt is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp"
                                or ".tga" or ".exr" or ".hdr" or ".psd"
                                or ".fbx" or ".obj" or ".asset" or ".prefab" or ".dll" or ".so" or ".exe")
                                continue;

                            try
                            {
                                var lines = File.ReadAllLines(file);
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var relFile = Path.GetRelativePath(projectRoot, file).Replace("\\", "/");
                                        results.Add($"{relFile}:{i + 1}: {lines[i].Trim()}");
                                    }
                                }
                            }
                            catch
                            {
                                // skip unreadable files
                            }

                            if (results.Count >= 500) break;
                        }

                        var output = results.Count > 0
                            ? string.Join("\n", results)
                            : $"No matches found for: {query}";

                        Send(MakeTextResult(id, output));
                        break;
                    }

                    case "list_csharp_scripts":
                    {
                        var assetsPath = SafePath("Assets");
                        if (!Directory.Exists(assetsPath))
                        {
                            Send(Error(id, -32004, "Assets folder not found"));
                            break;
                        }

                        var files = Directory.EnumerateFiles(assetsPath, "*.cs", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(projectRoot, f).Replace("\\", "/"))
                            .ToArray();

                        Send(MakeTextResult(id, string.Join("\n", files)));
                        break;
                    }

                    case "list_unity_scenes":
                    {
                        var assetsPath = SafePath("Assets");
                        if (!Directory.Exists(assetsPath))
                        {
                            Send(Error(id, -32004, "Assets folder not found"));
                            break;
                        }

                        var files = Directory.EnumerateFiles(assetsPath, "*.unity", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(projectRoot, f).Replace("\\", "/"))
                            .ToArray();

                        Send(MakeTextResult(id, string.Join("\n", files)));
                        break;
                    }

                    case "read_package_manifest":
                    {
                        var manifestPath = SafePath("Packages/manifest.json");
                        if (!File.Exists(manifestPath))
                        {
                            Send(Error(id, -32004, "Packages/manifest.json not found"));
                            break;
                        }

                        Send(MakeTextResult(id, File.ReadAllText(manifestPath)));
                        break;
                    }

                    case "read_editor_log":
                    {
                        var requestedLines = toolArgs?["lines"]?.GetValue<int>() ?? 400;
                        requestedLines = Math.Clamp(requestedLines, 1, 2000);

                        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var logPath = Path.Combine(localAppData, "Unity", "Editor", "Editor.log");

                        if (!File.Exists(logPath))
                        {
                            Send(Error(id, -32004, $"Unity Editor log not found at: {logPath}"));
                            break;
                        }

                        string[] allLines;
                        // Unity holds the log open; use FileShare.ReadWrite to allow reading a locked file
                        using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs))
                        {
                            allLines = reader.ReadToEnd().Split('\n');
                        }

                        var tail = allLines.Length <= requestedLines
                            ? allLines
                            : allLines[(allLines.Length - requestedLines)..];

                        Send(MakeTextResult(id, string.Join("\n", tail)));
                        break;
                    }

                    default:
                        Send(Error(id, -32601, $"Unknown tool: {toolName}"));
                        break;
                }
                break;
            }

            default:
                Send(Error(id, -32601, $"Unknown method: {method}"));
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            error = new { code = -32000, message = ex.Message }
        }, jsonOptions));
    }
}

static int CountOccurrences(string source, string value)
{
    int count = 0, index = 0;
    while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) != -1)
    {
        count++;
        index += value.Length;
    }
    return count;
}
