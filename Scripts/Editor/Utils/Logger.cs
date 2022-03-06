using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEventTracker.Utils
{
    internal static class Logger
    {
        private static readonly string LogsPath   = Path.Combine(Application.dataPath, "Plugins/UnityEventHelper/Logs");
        private const           int    FilesLimit = 5;

        internal static IEnumerable<string> GetBugReportPaths()
        {
            CreateRootDirectory();

            return Directory.EnumerateFiles(LogsPath);
        }

        internal static bool IsFull()
        {
            return Directory.Exists(LogsPath) && Directory.EnumerateFiles(LogsPath).Count() >= FilesLimit;
        }

        internal static void CreateBugReport(string assetName, string assetContent, Exception exception)
        {
            CreateRootDirectory();

            var files = Directory.EnumerateFiles(LogsPath).ToArray();

            if (files.Length >= FilesLimit)
            {
                return;
            }

            var count = files.Count(name => name.StartsWith(assetName));

            var fileName   = $"{assetName}_{count}.txt";
            var filePath   = Path.Combine(LogsPath, fileName);
            var stackTrace = exception.StackTrace;
            var exMessage  = exception.Message;
            var exType     = exception.GetType();

            File.WriteAllText(filePath, $"{assetContent} " +
                                        $"\n {exType} " +
                                        $"\n {exMessage} " +
                                        $"\n {stackTrace}");
        }

        private static void CreateRootDirectory()
        {
            if (Directory.Exists(LogsPath)) return;

            Directory.CreateDirectory(LogsPath);
        }
    }
}