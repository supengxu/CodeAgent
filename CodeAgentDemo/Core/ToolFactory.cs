using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

public class ToolFactory : IToolFactory
{
    private readonly string _workDir;
    private readonly HttpClient _webSearchHttpClient;
    private readonly HttpClient _codeSearchHttpClient;

    public ToolFactory(string workDir, HttpClient webSearchHttpClient, HttpClient codeSearchHttpClient)
    {
        _workDir = workDir;
        _webSearchHttpClient = webSearchHttpClient;
        _codeSearchHttpClient = codeSearchHttpClient;
    }

    public IEnumerable<ITool> CreateTools()
    {
        yield return new ReadTool(_workDir);
        yield return new WriteTool(_workDir);
        yield return new EditTool(_workDir);
        yield return new GlobTool(_workDir);
        yield return new GrepTool(_workDir);
        yield return new BashTool(_workDir);
        yield return new WebSearchTool(_webSearchHttpClient);
        yield return new CodeSearchTool(_codeSearchHttpClient);
    }
}