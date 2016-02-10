// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ArgumentEscaperTests
    {
        [Theory]
        [InlineData("arg", "")]
        public void EscapeAndConcatenateArgArrayForProcessStart_escapes_trailing_slashes(string arg, string expected)
        {
            Assert.Equal(ArgumentEscaper.EscapeAndConcatenateArgArrayForCmdProcessStart(new[] { arg }), expected);
        }
    }
}