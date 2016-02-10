using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils.CommandParsing;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ScriptExecutor
    {
        public static Command CreateCommandForScript(Project project, string scriptCommandLine, IDictionary<string, string> variables)
        {
            return CreateCommandForScript(project, scriptCommandLine, WrapVariableDictionary(variables));
        }

        public static Command CreateCommandForScript(Project project, string scriptCommandLine, Func<string, string> getVariable)
        {
            var scriptArguments = ParseScriptArguments(scriptCommandLine);
            if (scriptArguments == null)
            {
                return new NullCommand();
            }

            if (ScriptCommandCanBeResolved(scriptArguments))
            {
                return Command.Create(scriptArguments.FirstOrDefault(), scriptArguments.Skip(1))
                    .WorkingDirectory(project.ProjectDirectory);
            }
            else
            {
                return CreateScriptShellCommand(scriptArguments);
            }

        }

        public static Command CreateScriptShellCommand(IEnumerable<string> scriptArguments)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateWindowsScriptShellCommand(scriptArguments);
            }
            else
            {
                return CreateUnixScriptShellCommand(scriptArguments);
            }
        }

        public static Command CreateWindowsScriptShellCommand(IEnumerable<string> scriptArguments)
        {
            var scriptCommand = scriptArguments.First();

            scriptCommand = scriptCommand.Replace(
                Path.AltDirectorySeparatorChar, 
                Path.DirectorySeparatorChar);
                
            scriptArguments[0] = scriptCommand;

            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrEmpty(comSpec))
            {
                throw new Exception("Expected environment variable ComSpec to be defined.");
            }

            var shellCommandArguments = new string[] { comSpec }
                .Concat(scriptArguments)
                .ToArray();

            return Command
                    .Create(shellCommandArguments.First(), shellCommandArguments.Skip(1), useComSpec: true)
                    .WorkingDirectory(project.ProjectDirectory);
        }

        public static Command CreateUnixScriptShellCommand(IEnumerable<string> scriptArguments)
        {
            var scriptCommand = ResolveUnixScriptCommand(scriptArguments);
            scriptArguments[0] = scriptCommand;

            // Always use /usr/bin/env bash -c in order to support redirection and so on; similar to Windows case.
            // Unlike Windows, must escape quotation marks within the newly-quoted string.
            var shellCommandArguments = new[] { "/usr/bin/env", "bash", "-c", "\"" }
                .Concat(scriptArguments.Select(argument => argument.Replace("\"", "\\\"")))
                .Concat(new[] { "\"" })
                .ToArray();

            return Command
                .Create(shellCommandArguments.First(), shellCommandArguments.Skip(1))
                .WorkingDirectory(project.ProjectDirectory);
        }
        
        private static string ResolveUnixScriptCommand(IEnumerable<string> scriptArguments)
        {
            var scriptCommandCandidates = GenerateBashScriptCommandVariations(scriptArguments.First());
            var scriptCommand = scriptArguments.First();
            
            foreach (var candidate in scriptCommandCandidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        scriptCommand = candidate;
                    }
                }
                catch (Exception e) {}
            }
            
            return scriptCommand;
        }

        private static IEnumerable<string> GenerateBashScriptCommandVariations(string scriptCommand)
        {
            return new string[]
            {
                scriptCommand,
                "./" + scriptCommand,
                scriptCommand + ".sh",
                "./" + scriptCommand + ".sh"
            };
        }

        private static IEnumerable<string> ParseScriptArguments(string scriptCommandLine, Func<string, string> getVariable)
        {
            var scriptArguments = CommandGrammar.Process(
                scriptCommandLine,
                GetScriptVariable(project, getVariable),
                preserveSurroundingQuotes: false);

            // Ensure the array won't be empty and the elements won't be null or empty strings.
            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();
            if (scriptArguments.Length == 0)
            {
                return null;
            }

            return scriptArguments;
        }

        private static bool ScriptCommandCanBeResolved(IEnumerable<string> scriptArguments)
        {
            var resolved = CommandResolver.TryResolveCommandSpec(scriptArguments.FirstOrDefault(), scriptArguments.Skip(1));

            return resolved != null;
        }

        private static Func<string, string> WrapVariableDictionary(IDictionary<string, string> contextVariables)
        {
            return key =>
            {
                string value;
                contextVariables.TryGetValue(key, out value);
                return value;
            };
        }

        private static Func<string, string> GetScriptVariable(Project project, Func<string, string> getVariable)
        {
            var keys = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "project:Directory", () => project.ProjectDirectory },
                { "project:Name", () => project.Name },
                { "project:Version", () => project.Version.ToString() },
            };

            return key =>
            {
                // try returning key from dictionary
                Func<string> valueFactory;
                if (keys.TryGetValue(key, out valueFactory))
                {
                    return valueFactory();
                }

                // try returning command-specific key
                var value = getVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                // try returning environment variable
                return Environment.GetEnvironmentVariable(key);
            };
        }
    }
}
