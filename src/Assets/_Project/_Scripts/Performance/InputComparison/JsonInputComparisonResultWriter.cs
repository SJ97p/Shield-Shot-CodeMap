using System;
using System.IO;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class JsonInputComparisonResultWriter
        : IInputComparisonResultWriter
    {
        private const string FolderName = "PerformanceBenchmarks";

        public string Write(InputComparisonResultDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string directoryPath =
                Path.Combine(
                    Application.persistentDataPath,
                    FolderName);

            Directory.CreateDirectory(directoryPath);

            string timestamp =
                document.completedAtUtc
                    .Replace(":", string.Empty)
                    .Replace("-", string.Empty);
            string fileName =
                $"InputComparison_{timestamp}.json";
            string filePath =
                Path.Combine(directoryPath, fileName);
            string json =
                JsonUtility.ToJson(document, prettyPrint: true);

            File.WriteAllText(filePath, json);
            return filePath;
        }
    }
}
