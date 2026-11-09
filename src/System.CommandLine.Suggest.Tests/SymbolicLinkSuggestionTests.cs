// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Tests.Utility;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;

namespace System.CommandLine.Suggest.Tests
{
    public class SymbolicLinkSuggestionTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "dotnet suggest " + Path.GetRandomFileName());
        private readonly string _executable;
        private readonly FileSuggestionRegistration _registration;

        public SymbolicLinkSuggestionTests()
        {
            Directory.CreateDirectory(_directory);
            _executable = Path.Combine(_directory, "testapp");
            File.WriteAllText(_executable, "");
            _registration = new FileSuggestionRegistration(Path.Combine(_directory, "registrations.txt"));
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        [NonWindowsOnlyFact]
        public async Task Suggestions_are_returned_for_an_absolute_symbolic_link()
        {
            var link = CreateLink("bin", _executable);
            _registration.AddSuggestionRegistration(new Registration(_executable));

            await AssertSuggestions(link, _executable);
        }

        [NonWindowsOnlyFact]
        public async Task Suggestions_are_returned_for_a_relative_symbolic_link()
        {
            var link = CreateLink("bin", Path.Combine("..", "testapp"));
            _registration.AddSuggestionRegistration(new Registration(_executable));

            await AssertSuggestions(link, _executable);
        }

        [NonWindowsOnlyFact]
        public async Task Suggestions_are_returned_for_a_chain_of_symbolic_links()
        {
            CreateLink("first", _executable);
            var link = CreateLink("bin", Path.Combine("..", "first", "testapp"));
            _registration.AddSuggestionRegistration(new Registration(_executable));

            await AssertSuggestions(link, _executable);
        }

        [NonWindowsOnlyFact]
        public async Task The_invoked_symbolic_link_name_is_removed_from_the_command_line()
        {
            var link = CreateLink("bin", _executable, "otherapp");
            _registration.AddSuggestionRegistration(new Registration(_executable));

            await AssertSuggestions(link, _executable);
        }

        [NonWindowsOnlyFact]
        public async Task An_explicit_symbolic_link_registration_takes_precedence()
        {
            var link = CreateLink("bin", _executable);
            _registration.AddSuggestionRegistration(new Registration(_executable));
            _registration.AddSuggestionRegistration(new Registration(link));

            await AssertSuggestions(link, link);
        }

        [NonWindowsOnlyFact]
        public async Task An_unregistered_symbolic_link_target_returns_no_suggestions()
        {
            var link = CreateLink("bin", _executable);

            await AssertSuggestions(link, expectedExecutable: null);
        }

        [NonWindowsOnlyFact]
        public async Task A_dangling_symbolic_link_returns_no_suggestions()
        {
            var link = CreateLink("bin", Path.Combine(_directory, "missing"));

            await AssertSuggestions(link, expectedExecutable: null);
        }

        [NonWindowsOnlyFact]
        public async Task A_symbolic_link_cycle_returns_no_suggestions()
        {
            var firstLink = CreateLink("first", Path.Combine("..", "bin", "testapp"));
            var link = CreateLink("bin", firstLink);

            await AssertSuggestions(link, expectedExecutable: null);
        }

        [NonWindowsOnlyFact]
        public async Task A_missing_executable_returns_no_suggestions()
        {
            await AssertSuggestions(Path.Combine(_directory, "missing"), expectedExecutable: null);
        }

        [NonWindowsOnlyFact]
        public async Task An_unregistered_regular_file_returns_no_suggestions()
        {
            await AssertSuggestions(_executable, expectedExecutable: null);
        }

        private string CreateLink(string directoryName, string target, string fileName = "testapp")
        {
            var directory = Path.Combine(_directory, directoryName);
            Directory.CreateDirectory(directory);
            var link = Path.Combine(directory, fileName);
            File.CreateSymbolicLink(link, target);
            return link;
        }

        private async Task AssertSuggestions(string executable, string expectedExecutable)
        {
            var store = new RecordingSuggestionStore();
            var dispatcher = new SuggestionDispatcher(_registration, store);
            dispatcher.Configuration.Output = new StringWriter();
            dispatcher.Configuration.Error = new StringWriter();
            var commandLine = Path.GetFileName(executable) + " --h";

            var exitCode = await dispatcher.InvokeAsync(new[]
            {
                "get", "--executable", executable, "--position", commandLine.Length.ToString(), "--", commandLine
            });

            exitCode.Should().Be(0);
            dispatcher.Configuration.Error.ToString().Should().BeEmpty();
            store.Executable.Should().Be(expectedExecutable);
            dispatcher.Configuration.Output.ToString().Should().Be(expectedExecutable is null ? "" : "--help");
            if (expectedExecutable is not null)
            {
                store.Arguments.Should().Be("[suggest:3] \"--h\"");
            }
        }

        private class RecordingSuggestionStore : ISuggestionStore
        {
            public string Executable { get; private set; }
            public string Arguments { get; private set; }

            public string GetCompletions(string exeFileName, string suggestionTargetArguments, TimeSpan timeout)
            {
                Executable = exeFileName;
                Arguments = suggestionTargetArguments;
                return "--help";
            }
        }
    }
}
