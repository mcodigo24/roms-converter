using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Tests;

public sealed class ToolLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());

    public ToolLocatorTests() => Directory.CreateDirectory(Path.Combine(_root, "tools"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Resolves_tools_under_tools_folder_next_to_base_directory()
    {
        var locator = new ToolLocator(_root);

        Assert.Equal(Path.Combine(_root, "tools", "chdman.exe"), locator.ChdmanPath);
        Assert.Equal(Path.Combine(_root, "tools", "maxcso.exe"), locator.MaxcsoPath);
    }

    [Fact]
    public void Reports_all_missing_with_actionable_message()
    {
        var locator = new ToolLocator(_root);

        Assert.Equal(2, locator.FindMissing().Count);
        var message = locator.DescribeMissing();
        Assert.NotNull(message);
        Assert.Contains("chdman.exe", message);
        Assert.Contains("maxcso.exe", message);
        Assert.Contains("antivirus", message);
    }

    [Fact]
    public void Reports_only_the_missing_tool()
    {
        File.WriteAllText(Path.Combine(_root, "tools", "chdman.exe"), "x");
        var locator = new ToolLocator(_root);

        Assert.Equal([locator.MaxcsoPath], locator.FindMissing());
    }

    [Fact]
    public void No_message_when_all_tools_exist()
    {
        File.WriteAllText(Path.Combine(_root, "tools", "chdman.exe"), "x");
        File.WriteAllText(Path.Combine(_root, "tools", "maxcso.exe"), "x");
        var locator = new ToolLocator(_root);

        Assert.Empty(locator.FindMissing());
        Assert.Null(locator.DescribeMissing());
    }
}
