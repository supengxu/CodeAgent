using System.Text.Json;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Tools;

public class ReadToolTests
{
    private readonly string _tempDir;
    private readonly ReadTool _tool;

    public ReadToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tool = new ReadTool(_tempDir);
    }

    ~ReadToolTests()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Name_ShouldBeRead()
    {
        _tool.Name.Should().Be("read");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("读取");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnFalse()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFilePath_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"/nonexistent/file.txt\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingFile_ShouldReturnContent()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "Hello World");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)}}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithDirectory_ShouldReturnEntries()
    {
        File.WriteAllText(Path.Combine(_tempDir, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(_tempDir, "file2.txt"), "content2");
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(_tempDir)}}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("file1.txt");
        result.Output.Should().Contain("file2.txt");
        result.Output.Should().Contain("subdir");
    }

    [Fact]
    public async Task ExecuteAsync_WithOffset_ShouldStartFromOffset()
    {
        var filePath = Path.Combine(_tempDir, "lines.txt");
        await File.WriteAllLinesAsync(filePath, new[] { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" });

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"offset\":3}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("3: Line 3");
        result.Output.Should().NotContain("1: Line 1");
    }

    [Fact]
    public async Task ExecuteAsync_WithLimit_ShouldLimitLines()
    {
        var filePath = Path.Combine(_tempDir, "limited.txt");
        await File.WriteAllLinesAsync(filePath, Enumerable.Range(1, 100).Select(i => $"Line {i}"));

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"limit\":10}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Line 1");
        result.Output.Should().Contain("Line 10");
        result.Output.Should().NotContain("Line 11");
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new ReadTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class WriteToolTests
{
    private readonly string _tempDir;
    private readonly WriteTool _tool;

    public WriteToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tool = new WriteTool(_tempDir);
    }

    ~WriteToolTests()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Name_ShouldBeWrite()
    {
        _tool.Name.Should().Be("write");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("写入");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnTrue()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\",\"content\":\"test\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFilePath_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"content\":\"test\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingContent_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArgs_ShouldWriteFile()
    {
        var filePath = Path.Combine(_tempDir, "newfile.txt");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"content\":\"Hello World\"}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithSubdirectory_ShouldCreateDirectory()
    {
        var filePath = Path.Combine(_tempDir, "subdir", "nested.txt");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"content\":\"nested content\"}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempDir, "subdir")).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new WriteTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class EditToolTests
{
    private readonly string _tempDir;
    private readonly EditTool _tool;

    public EditToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tool = new EditTool(_tempDir);
    }

    ~EditToolTests()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Name_ShouldBeEdit()
    {
        _tool.Name.Should().Be("edit");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("替换");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnTrue()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\",\"oldString\":\"old\",\"newString\":\"new\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFilePath_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"oldString\":\"old\",\"newString\":\"new\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingOldString_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\",\"newString\":\"new\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingNewString_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"test.txt\",\"oldString\":\"old\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"filePath\":\"/nonexistent.txt\",\"oldString\":\"old\",\"newString\":\"new\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidEdit_ShouldModifyFile()
    {
        var filePath = Path.Combine(_tempDir, "edit.txt");
        await File.WriteAllTextAsync(filePath, "Hello old World");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"oldString\":\"old\",\"newString\":\"new\"}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("Hello new World");
    }

    [Fact]
    public async Task ExecuteAsync_WithIdenticalStrings_ShouldReturnFailure()
    {
        var filePath = Path.Combine(_tempDir, "same.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"oldString\":\"text\",\"newString\":\"text\"}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("identical");
    }

    [Fact]
    public async Task ExecuteAsync_WithNotFoundString_ShouldThrow()
    {
        var filePath = Path.Combine(_tempDir, "notfound.txt");
        await File.WriteAllTextAsync(filePath, "Hello World");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"oldString\":\"UNIQUE_STRING_12345\",\"newString\":\"new\"}}").RootElement;

        var act = async () => await _tool.ExecuteAsync(args);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithReplaceAll_ShouldReplaceAllOccurrences()
    {
        var filePath = Path.Combine(_tempDir, "multiple.txt");
        await File.WriteAllTextAsync(filePath, "foo bar foo baz foo");

        var args = JsonDocument.Parse($"{{\"filePath\":{JsonSerializer.Serialize(filePath)},\"oldString\":\"foo\",\"newString\":\"qux\",\"replaceAll\":true}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("qux bar qux baz qux");
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new EditTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class GlobToolTests
{
    private readonly string _tempDir;
    private readonly GlobTool _tool;

    public GlobToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tool = new GlobTool(_tempDir);
    }

    ~GlobToolTests()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Name_ShouldBeGlob()
    {
        _tool.Name.Should().Be("glob");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("glob");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnFalse()
    {
        var args = JsonDocument.Parse("{\"pattern\":\"*.txt\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPattern_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingFiles_ShouldReturnFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "content");
        File.WriteAllText(Path.Combine(_tempDir, "other.txt"), "content");

        var args = JsonDocument.Parse("{\"pattern\":\"*.txt\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("test.txt");
        result.Output.Should().Contain("other.txt");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatches_ShouldReturnEmpty()
    {
        var args = JsonDocument.Parse("{\"pattern\":\"*.nonexistent\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("No files found");
    }

    [Fact]
    public async Task ExecuteAsync_WithRecursivePattern_ShouldFindNestedFiles()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "root.cs"), "code");
        File.WriteAllText(Path.Combine(_tempDir, "sub", "nested.cs"), "code");

        var args = JsonDocument.Parse("{\"pattern\":\"**/*.cs\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain(".cs");
    }

    [Fact]
    public async Task ExecuteAsync_WithSpecificPattern_ShouldFindMatchingFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "code");
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "text");

        var args = JsonDocument.Parse("{\"pattern\":\"*.cs\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("test.cs");
        result.Output.Should().NotContain("test.txt");
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new GlobTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class GrepToolTests
{
    private readonly string _tempDir;
    private readonly GrepTool _tool;

    public GrepToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tool = new GrepTool(_tempDir);
    }

    ~GrepToolTests()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Name_ShouldBeGrep()
    {
        _tool.Name.Should().Be("grep");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("搜索");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnFalse()
    {
        var args = JsonDocument.Parse("{\"pattern\":\"test\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPattern_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingContent_ShouldReturnMatches()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "Hello World\nTest Line\nAnother test");

        var args = JsonDocument.Parse("{\"pattern\":\"test\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("test.txt");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatches_ShouldReturnEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "Hello World");

        var args = JsonDocument.Parse("{\"pattern\":\"nonexistent\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("No files found");
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludePattern_ShouldFilterFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "pattern match");
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "pattern match");

        var args = JsonDocument.Parse("{\"pattern\":\"pattern\",\"include\":\"*.cs\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("test.cs");
        result.Output.Should().NotContain("test.txt");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRegex_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"pattern\":\"[invalid\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Invalid regex");
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new GrepTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class IToolTests
{
    [Fact]
    public void ITool_ShouldHaveNameProperty()
    {
        typeof(ITool).GetProperty("Name").Should().NotBeNull();
    }

    [Fact]
    public void ITool_ShouldHaveDescriptionProperty()
    {
        typeof(ITool).GetProperty("Description").Should().NotBeNull();
    }

    [Fact]
    public void ITool_ShouldHaveInputSchemaProperty()
    {
        typeof(ITool).GetProperty("InputSchema").Should().NotBeNull();
    }

    [Fact]
    public void ITool_ShouldHaveExecuteAsyncMethod()
    {
        var method = typeof(ITool).GetMethod("ExecuteAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ToolResult>));
    }

    [Fact]
    public void ITool_ShouldHaveRequiresConfirmationMethod()
    {
        var method = typeof(ITool).GetMethod("RequiresConfirmation");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(bool));
    }
}

public class ToolResultTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var result = new ToolResult(true, "Output message");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Output message");
    }

    [Fact]
    public void Constructor_WithFailure_ShouldSetSuccessFalse()
    {
        var result = new ToolResult(false, "Error message");

        result.Success.Should().BeFalse();
    }
}