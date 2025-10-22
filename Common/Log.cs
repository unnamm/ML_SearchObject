using Common.Message;
using Common.Config;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Common
{
    public class Log
    {
        public ObservableCollection<string> LogList { get; set; } = []; //print list ui

        private readonly DataYaml _config;

        private string _fileName = string.Empty;
        private DateTime _beforeDay;
        private ConcurrentQueue<string> _waitMessage = [];
        private CancellationTokenSource _cts;

        public Log(DataYaml config)
        {
            _config = config;
        }

        /// <summary>
        /// make folder, set folder path
        /// </summary>
        /// <param name="maxLine">LogList max count</param>
        private void Initialize()
        {
            _beforeDay = DateTime.Now;

            if (Directory.Exists(_config.LogFolderName) == false)
            {
                Directory.CreateDirectory(_config.LogFolderName);
            }

            _fileName = Path.Combine(_config.LogFolderName, _beforeDay.ToString("yyyy-MM-dd") + ".txt");

            _cts?.Cancel();
            _cts = new();
            _ = CheckQueue(_cts.Token);
        }

        private async Task CheckQueue(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    await Task.Delay(100, token);

                    while (!_waitMessage.IsEmpty)
                    {
                        _waitMessage.TryDequeue(out var message);

                        await File.AppendAllTextAsync(_fileName, message + Environment.NewLine, token);
                        WeakReferenceMessenger.Default.Send(new InvokeMessage(WriteUICollection, message!));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        /// <summary>
        /// print textfile, print LogList array
        /// </summary>
        /// <param name="message"></param>
        public void Write(string message)
        {
            if (_beforeDay.Day != DateTime.Now.Day) //check next day
            {
                Initialize();
            }

            _waitMessage.Enqueue($"[{DateTime.Now:HH:mm:ss.f}] {message}{Environment.NewLine}");
        }

        private void WriteUICollection(string message)
        {
            LogList.Insert(0, message);
            if (LogList.Count > _config.LogMaxLine)
            {
                LogList.RemoveAt(LogList.Count - 1);
            }
        }
    }
}
