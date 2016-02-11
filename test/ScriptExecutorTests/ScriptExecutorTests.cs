using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Utils.ScriptExecutor.Tests
{
    public class ScriptExecutorTests : TestBase
    {
        private static readonly string s_testProjectRoot = Path.Combine(AppContext.BaseDirectory, "TestProjects");

        private TempDirectory _root;

        public ScriptExecutorTests()
        {
            _root = Temp.CreateDirectory();
        }

        [Fact]
        public Test_Project_Local_Script_is_Resolved()
        {
            var sourceTestProjectPath = Path.Combine(s_testProjectRoot, "TestApp");
            var binTestProjectPath = _root.CopyDirectory(sourceTestProjectPath).Path;

            var project = ProjectContext.Create(binTestProjectPath, "dnxcore50").Project;

            CreateTestScriptFile("some.script", binTestProjectPath);
            var scriptCommandLine = "some.script";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, null);

            Assert.True(command != null);
            Assert.Equal(CommandResolutionStrategy.ProjectLocal, command.ResolutionStrategy);
        }
        
        [Fact]
        public Test_Nonexistent_Project_Local_Script_is_not_Resolved()
        {
            var sourceTestProjectPath = Path.Combine(s_testProjectRoot, "TestApp");
            var binTestProjectPath = _root.CopyDirectory(sourceTestProjectPath).Path;

            var project = ProjectContext.Create(binTestProjectPath, "dnxcore50").Project;

            var scriptCommandLine = "nonexistent.script";

            Assert.Throws<CommandUnknownException>(
                () => ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, null));
        }
        
        [Theory]
        [InlineData(".sh")]
        [InlineData(".cmd")]
        public Test_Extension_Inference_For_Project_Local_Scripts(var extension)
        {
            var sourceTestProjectPath = Path.Combine(s_testProjectRoot, "TestApp");
            var binTestProjectPath = _root.CopyDirectory(sourceTestProjectPath).Path;

            var project = ProjectContext.Create(binTestProjectPath, "dnxcore50").Project;

            CreateTestScriptFile("script" + extension, binTestProjectPath);

            // Don't include extension
            var scriptCommandLine = "script";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, null);

            Assert.True(command != null);
            Assert.Equal(CommandResolutionStrategy.ProjectLocal, command.ResolutionStrategy);
        }
        
        [Fact]
        public Test_Script_Builtins_Fail()
        {
            var sourceTestProjectPath = Path.Combine(s_testProjectRoot, "TestApp");
            var binTestProjectPath = _root.CopyDirectory(sourceTestProjectPath).Path;

            var project = ProjectContext.Create(binTestProjectPath, "dnxcore50").Project;

            var scriptCommandLine = "echo";

            Assert.Throws<CommandUnknownException>(
                () => ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, null));
        }
    }
}
