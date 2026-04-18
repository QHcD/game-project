using System.Text.Json;
using System.Text.Json.Nodes;

var projectRoot = Environment.GetEnvironmentVariable("UNITY_PROJECT_ROOT")
                 ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
                 ?? Directory.GetCurrentDirectory();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = false
};

string SafePath(string relativePath)
{
    var combined = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    if (!combined.StartsWith(Path.GetFullPath(projectRoot), StringComparison.Ordinal))
        throw new InvalidOperationException("Path escapes project root.");
    return combined;
}

object Success(object? id, object result) => new
{
    jsonrpc = "2.0",
    id,
    result
};

object Error(object? id, int code, string message) => new
{
    jsonrpc = "2.0",
    id,
    error = new
    {
        code,
        message
    }
};

while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
        continue;

    try
    {
        var msg = JsonNode.Parse(line)?.AsObject();
        if (msg is null)
            continue;

        var idNode = msg["id"];
        object? id = idNode is null ? null : JsonSerializer.Deserialize<object>(idNode.ToJsonString());

        var method = msg["method"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(method))
        {
            Console.WriteLine(JsonSerializer.Serialize(Error(id, -32600, "Missing method"), jsonOptions));
            continue;
        }

        switch (method)
        {
            case "initialize":
            {
                var result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    serverInfo = new
                    {
                        name = "unity-mcp-server",
                        version = "0.1.0"
                    }
                };

                Console.WriteLine(JsonSerializer.Serialize(Success(id, result), jsonOptions));
                break;
            }

            case "notifications/initialized":
            {
                break;
            }

            case "tools/list":
            {
                var result = new
                {
                    tools = new object[]
                    {
                        new
                        {
                            name = "list_files",
                            description = "List files under the Unity project root. Optional subpath filter.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    subpath = new
                                    {
                                        type = "string",
                                        description = "Optional folder relative to the Unity project root, like Assets or Packages."
                                    }
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
                                    path = new
                                    {
                                        type = "string",
                                        description = "Relative path like Assets/Scripts/PlayerController.cs"
                                    }
                                },
                                required = new[] { "path" }
                            }
                        }
                    }
                };

                Console.WriteLine(JsonSerializer.Serialize(Success(id, result), jsonOptions));
                break;
            }

            case "tools/call":
            {
                var requestParams = msg["params"]?.AsObject();
                var toolName = requestParams?["name"]?.GetValue<string>();
                var toolArgs = requestParams?["arguments"]?.AsObject();

                if (string.IsNullOrWhiteSpace(toolName))
                {
                    Console.WriteLine(JsonSerializer.Serialize(Error(id, -32602, "Missing tool name"), jsonOptions));
                    break;
                }

                if (toolName == "list_files")
                {
                    var subpath = toolArgs?["subpath"]?.GetValue<string>() ?? ".";
                    var folder = SafePath(subpath);

                    if (!Directory.Exists(folder))
                    {
                        Console.WriteLine(JsonSerializer.Serialize(Error(id, -32004, $"Folder not found: {subpath}"), jsonOptions));
                        break;
                    }

                    var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(projectRoot, f).Replace("\\", "/"))
                        .Take(500)
                        .ToArray();

                    var result = new
                    {
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = string.Join("\n", files)
                            }
                        }
                    };

                    Console.WriteLine(JsonSerializer.Serialize(Success(id, result), jsonOptions));
                    break;
                }

                if (toolName == "read_file")
                {
                    var relPath = toolArgs?["path"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(relPath))
                    {
                        Console.WriteLine(JsonSerializer.Serialize(Error(id, -32602, "Missing path"), jsonOptions));
                        break;
                    }

                    var filePath = SafePath(relPath);
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine(JsonSerializer.Serialize(Error(id, -32004, $"File not found: {relPath}"), jsonOptions));
                        break;
                    }

                    var text = File.ReadAllText(filePath);

                    var result = new
                    {
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text
                            }
                        }
                    };

                    Console.WriteLine(JsonSerializer.Serialize(Success(id, result), jsonOptions));
                    break;
                }

                Console.WriteLine(JsonSerializer.Serialize(Error(id, -32601, $"Unknown tool: {toolName}"), jsonOptions));
                break;
            }

            default:
                Console.WriteLine(JsonSerializer.Serialize(Error(id, -32601, $"Unknown method: {method}"), jsonOptions));
                break;
        }
    }
    catch (Exception ex)
    {
        var err = new
        {
            jsonrpc = "2.0",
            error = new
            {
                code = -32000,
                message = ex.Message
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(err, jsonOptions));
    }
}