using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 使用 Exa AI 进行 Web 搜索的工具。
/// </summary>
public class WebSearchTool : ITool
{
    private const string ApiBaseUrl = "https://mcp.exa.ai/mcp";
    private const int DefaultNumResults = 8;
    private const int DefaultTimeoutMs = 25000;

    private readonly HttpClient _httpClient;

    public WebSearchTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public string Name => "websearch";

    public string Description =>
        $"使用 Exa AI 搜索网页 - 执行实时网络搜索并可抓取特定 URL 的内容。\n\n" +
        "- 提供最新的事件信息和近期数据\n" +
        "- 支持可配置的结果数量，返回最相关网站的内容\n" +
        "- 用于获取知识截止日期之后的信息\n" +
        "- 搜索在单次 API 调用中自动完成\n\n" +
        $"当前日期是 {DateTime.Now}。搜索近期信息或时事时必须使用此日期。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "query": {
                "type": "string",
                "description": "网络搜索查询"
            },
            "numResults": {
                "type": "integer",
                "description": "返回的搜索结果数量（默认 8）"
            },
            "livecrawl": {
                "type": "string",
                "enum": ["fallback", "preferred"],
                "description": "实时抓取模式 - 'fallback'：缓存不可用时使用实时抓取，'preferred'：优先使用实时抓取（默认 'fallback'）"
            },
            "type": {
                "type": "string",
                "enum": ["auto", "fast", "deep"],
                "description": "搜索类型 - 'auto'：平衡搜索（默认），'fast'：快速结果，'deep'：全面搜索"
            },
            "contextMaxCharacters": {
                "type": "integer",
                "description": "针对 LLM 优化的上下文字符数上限（默认 10000）"
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

        var numResults = arguments.TryGetProperty("numResults", out var numResultsProp)
            ? numResultsProp.GetInt32()
            : DefaultNumResults;

        var livecrawl = arguments.TryGetProperty("livecrawl", out var livecrawlProp)
            ? livecrawlProp.GetString() ?? "fallback"
            : "fallback";

        var type = arguments.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? "auto"
            : "auto";

        var contextMaxCharacters = arguments.TryGetProperty("contextMaxCharacters", out var contextProp)
            ? contextProp.GetInt32()
            : 10000;

        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "tools/call",
            Params = new McpParams
            {
                Name = "web_search_exa",
                Arguments = new WebSearchArguments
                {
                    Query = query,
                    Type = type,
                    NumResults = numResults,
                    Livecrawl = livecrawl,
                    ContextMaxCharacters = contextMaxCharacters
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
                return new ToolResult(false, $"Search error ({response.StatusCode}): {responseText}");

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

            return new ToolResult(true, "No search results found. Please try a different query.");
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, "Search request timed out");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Search error: {ex.Message}");
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

    private class WebSearchArguments
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "auto";

        [JsonPropertyName("numResults")]
        public int NumResults { get; set; } = 8;

        [JsonPropertyName("livecrawl")]
        public string Livecrawl { get; set; } = "fallback";

        [JsonPropertyName("contextMaxCharacters")]
        public int ContextMaxCharacters { get; set; } = 10000;
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