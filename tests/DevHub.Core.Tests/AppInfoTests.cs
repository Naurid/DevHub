using DevHub.Core;
using Xunit;

namespace DevHub.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_is_DevHub()
    {
        Assert.Equal("DevHub", AppInfo.Name);
    }

    [Fact]
    public void Template_schema_version_is_1()
    {
        Assert.Equal(1, AppInfo.TemplateSchemaVersion);
    }
}
