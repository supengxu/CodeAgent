using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 使用 Exa Code API 搜索代码和文档的工具。
/// </summary>
public class CodeSearchTool : ITool
{
    private const string ApiBaseUrl = "https://mcp.exa.ai/mcp";
    private const int DefaultTimeoutMs = 30000;
    private const int DefaultTokensNum = 5000;

    private readonly HttpClient _httpClient;

    public CodeSearchTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public string Name => "codesearch";

    public string Description =>
        "使用 Exa Code API 搜索代码和获取编程相关上下文。\n\n" +
        "- 提供库、SDK 和 API 的最高质量、最新鲜的上下文\n" +
        "- 用于任何与编程相关的问题或任务\n" +
        "- 返回全面的代码示例、文档和 API 参考\n" +
        "- 专为查找特定编程模式和解决方案优化\n\n" +
        "用法说明：\n" +
        "- 可调整 token 数量（1000-50000）获取聚焦或全面的结果\n" +
        "- 默认 5000 tokens 为大多数查询提供平衡的上下文\n" +
        "- 特定问题使用较低值，全面文档使用较高值\n" +
        "- 支持查询框架、库、API 和编程概念\n" +
        "- 示例：\"React useState hook examples\"、\"Python pandas dataframe filtering\"、\"Express.js middleware\"";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "query": {
                "type": "string",
                "description": "搜索 API、库和 SDK 相关上下文的查询。例如：\"React useState hook examples\"、\"Python pandas dataframe filtering\"、\"Express.js middleware\""
            },
            "tokensNum": {
                "type": "integer",
                "minimum": 1000,
                "maximum": 50000,
                "default": 5000,
                "description": "返回的 token 数量（1000-50000）。默认 5000 tokens。根据需要的上下文量调整 - 聚焦查询用较低值，全面文档用较高值。"
            }
        },
        "required": ["query"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => false;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("query", out var queryProp))
            return new ToolResult(false, "query is required");

        var query = queryProp.GetString();
        if (string.IsNullOrEmpty(query))
            return new ToolResult(false, "query cannot be empty");

        var tokensNum = arguments.TryGetProperty("tokensNum", out var tokensProp)
            ? Math.Clamp(tokensProp.GetInt32(), 1000, 50000)
            : DefaultTokensNum;

        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "tools/call",
            Params = new McpParams
            {
                Name = "get_code_context_exa",
                Arguments = new CodeSearchArguments
                {
                    Query = query,
                    TokensNum = tokensNum
                }
            }
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            var jsonContent = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl);
            httpRequest.Content = content;
            httpRequest.Headers.Add("Accept", "application/json, text/event-stream");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken: cts.Token);
            var responseText = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, $"Code search error ({response.StatusCode}): {responseText}");

            foreach (var line in responseText.Split('\n'))
            {
                if (line.StartsWith("data: "))
                {
                    var data = JsonSerializer.Deserialize<McpResponse>(line[6..]);
                    if (data?.Result?.Content is { Count: > 0 })
                    {
                        return new ToolResult(true, data.Result.Content[0].Text);
                    }
                }
            }

            return new ToolResult(true, "No code snippets or documentation found. Please try a different query, be more specific about the library or programming concept, or check the spelling of framework names.");
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, "Code search request timed out");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Code search error: {ex.Message}");
        }
    }

    private class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = "";

        [JsonPropertyName("params")]
        public McpParams Params { get; set; } = new();
    }

    private class McpParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("arguments")]
        public object Arguments { get; set; } = new { };
    }

    private class CodeSearchArguments
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";

        [JsonPropertyName("tokensNum")]
        public int TokensNum { get; set; } = 5000;
    }

    private class McpResponse
    {
        [JsonPropertyName("result")]
        public McpResult? Result { get; set; }
    }

    private class McpResult
    {
        [JsonPropertyName("content")]
        public List<McpContent>? Content { get; set; }
    }

    private class McpContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }
}