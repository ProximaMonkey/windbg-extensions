﻿using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using DbgX.Interfaces.Services;

namespace WinDbgExt.History
{
    public partial class CommandControl
    {
        private readonly IDbgConsole _console;
        private readonly IHistoryManager _historyManager;

        public CommandControl(IDbgConsole console, IHistoryManager historyManager, string content)
        {
            _console = console;
            _historyManager = historyManager;

            InitializeComponent();

            AppendDmlOutput(content);
        }

        private async void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                var textBox = (TextBox)sender;

                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    return;
                }

                var command = textBox.Text;

                textBox.Text = string.Empty;

                await ExecuteCommand(command);
            }
        }

        private async Task ExecuteCommand(string command)
        {
            ContentTextBox.Document.Blocks.Add(new Paragraph(new Run(command)));
            ContentTextBox.ScrollToEnd();

            var result = await _console.ExecuteCommandAndCaptureOutputAsync(command);

            _historyManager.LogCommand(command, result);

            AppendDmlOutput(result);
        }

        private void AppendDmlOutput(string content)
        {
            var paragraph = new Paragraph();

            ContentTextBox.Document.Blocks.Add(paragraph);

            var matches = Regex.Matches(content, @"<exec cmd=\""(?<command>.+?)\"">(?<label>.+?)</exec>");

            int index = 0;

            foreach (Match match in matches)
            {
                paragraph.Inlines.Add(new Run(System.Net.WebUtility.HtmlDecode(content.Substring(index, match.Index - index))));

                var hyperLink = new Hyperlink(new Run(match.Groups["label"].Value));

                var command = match.Groups["command"].Value;

                hyperLink.Command = new DmlCommand(() => _ = ExecuteCommand(command));

                paragraph.Inlines.Add(hyperLink);

                index = match.Index + match.Length;
            }

            if (index < content.Length)
            {
                paragraph.Inlines.Add(new Run(System.Net.WebUtility.HtmlDecode(content.Substring(index))));
            }

            Dispatcher.InvokeAsync(() => ContentTextBox.ScrollToEnd(), DispatcherPriority.ApplicationIdle);
        }

        private class DmlCommand : ICommand
        {
            private readonly Action _command;

            public DmlCommand(Action command)
            {
                _command = command;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => _command();
        }
    }
}
