// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ArgumentEscaper
    {
        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeAndConcatenateArgArrayForProcessStart(IEnumerable<string> args)
        {
            return string.Join(" ", EscapeArgArray(args));
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeAndConcatenateArgArrayForCmdProcessStart(IEnumerable<string> args, bool escapeArgsForProcessStart)
        {
            // when invoking .exe via cmd args need to be escape as for ProcessStart otherwise quoted
            // args ending with '\' (e.g. "C:\temp\") will get corrupted
            if (escapeArgsForProcessStart)
            {
                args = EscapeArgArray(args);
            }

            return string.Join(" ", EscapeArgArrayForCmd(args, escapeArgsForProcessStart));
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static IEnumerable<string> EscapeArgArray(IEnumerable<string> args)
        {
            var escapedArgs = new List<string>();

            foreach (var arg in args)
            {
                escapedArgs.Add(EscapeArg(arg));
            }

            return escapedArgs;
        }

        /// <summary>
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would
        /// be to do this only for cmd metacharacters.
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static IEnumerable<string> EscapeArgArrayForCmd(IEnumerable<string> arguments, bool escapeArgsForProcessStart)
        {
            var escapedArgs = new List<string>();

            foreach (var arg in arguments)
            {
                escapedArgs.Add(EscapeArgForCmd(arg, escapeArgsForProcessStart));
            }

            return escapedArgs;
        }

        private static string EscapeArg(string arg)
        {
            if (!ShouldSurroundWithQuotes(arg, isForCmd: false))
            {
                // already in quotes or does not need quotes and therefore does not need escaping
                return arg;
            }

            var sb = new StringBuilder("\"");

            for (int i = 0; i < arg.Length; ++i)
            {
                var backslashCount = 0;

                // Consume All Backslashes
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                // Escape any backslashes at the end of the arg
                // This ensures the outside quote is interpreted as
                // an argument delimiter
                if (i == arg.Length)
                {
                    sb.Append('\\', 2 * backslashCount);
                }

                // Escape any preceding backslashes and the quote
                else if (arg[i] == '"')
                {
                    sb.Append('\\', (2 * backslashCount) + 1);
                    sb.Append('"');
                }

                // Output any consumed backslashes and the character
                else
                {
                    sb.Append('\\', backslashCount);
                    sb.Append(arg[i]);
                }
            }

            sb.Append("\"");

            return sb.ToString();
        }

        /// <summary>
        /// Prepare as single argument to
        /// roundtrip properly through cmd.
        ///
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would
        /// be to do this only for cmd metacharacters.
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static string EscapeArgForCmd(string argument, bool escapeArgsForProcessStart)
        {
            var hasQuotes = argument.StartsWith("\"", StringComparison.Ordinal) &&
                argument.EndsWith("\"", StringComparison.Ordinal);

            var needsQuotes = ShouldSurroundWithQuotes(argument, isForCmd: true);

            var sb = new StringBuilder();

            if (needsQuotes) sb.Append("^\"");

            foreach (var character in argument)
            {
                // escaping nested quotes in cmd is impossible so only escape if unquoted
                if (character == '"') // && !needsQuotes && !hasQuotes)
                {
                    sb.Append("^\"");
                    if (!escapeArgsForProcessStart)
                    {
                        sb.Append("^\"");
                    }
                }
                else
                {
                    sb.Append("^");
                    sb.Append(character);
                }
            }

            if (needsQuotes) sb.Append("^\"");

            return sb.ToString();
        }

        /// <summary>
        /// Prepare as single argument to
        /// roundtrip properly through cmd.
        ///
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would
        /// be to do this only for cmd metacharacters.
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <param name="isForCmd">
        /// Whether the argument is a CMD parameter. Argument for Process Start need to be in quotes if they contain "
        /// </param>
        /// <returns></returns>
        internal static bool ShouldSurroundWithQuotes(string argument, bool isForCmd)
        {
            // Don't quote already quoted strings
            if (argument.StartsWith("\"", StringComparison.Ordinal) &&
                argument.EndsWith("\"", StringComparison.Ordinal))
            {
                return false;
            }

            // Only quote if whitespace exists in the string
            return (argument.Contains(" ") || argument.Contains("\t") || argument.Contains("\n")
                || (argument.Contains("\"") && !isForCmd));
        }
    }
}
