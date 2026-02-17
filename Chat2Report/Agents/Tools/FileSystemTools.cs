using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Chat2Report.Agents.Tools
{
    public class FileSystemTools
    {
        // Mock data to simulate the file system
        private static Dictionary<string, List<string>> _mockFileSystem = new Dictionary<string, List<string>>()
        {
            { "downloads", new List<string> { "downloads/file1.txt", "downloads/image.jpg", "downloads/document.pdf" } },
            { "documents", new List<string> { "documents/report.docx", "documents/notes.txt" } },
            { "temp", new List<string>() }
        };

        [Description("Lists the files in the specified directory.")]
        public List<string> ListFiles(string directoryPath)
        {
            Console.WriteLine($"ListFiles called with directoryPath: {directoryPath}");
            // Simulate listing files in a directory
            if (_mockFileSystem.ContainsKey(directoryPath.ToLower()))
            {
                return _mockFileSystem[directoryPath.ToLower()];
            }
            else
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return new List<string>(); // Return empty list if directory not found
            }
        }

        public bool CreateDirectory(string directoryPath)
        {
            Console.WriteLine($"CreateDirectory called with directoryPath: {directoryPath}");
            // Simulate creating a directory
            if (!_mockFileSystem.ContainsKey(directoryPath.ToLower()))
            {
                _mockFileSystem.Add(directoryPath.ToLower(), new List<string>());
                return true;
            }
            else
            {
                Console.WriteLine($"Directory already exists: {directoryPath}");
                return false; // Directory already exists
            }
        }

        public bool MoveFile(string sourceFilePath, string destinationFilePath)
        {
            Console.WriteLine($"MoveFile called with sourceFilePath: {sourceFilePath}, destinationFilePath: {destinationFilePath}");
            // Simulate moving a file
            string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            string destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            string fileName = Path.GetFileName(sourceFilePath);

            if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(destinationDirectory) || string.IsNullOrEmpty(fileName))
            {
                Console.WriteLine("Invalid file paths provided.");
                return false;
            }

            if (_mockFileSystem.ContainsKey(sourceDirectory.ToLower()) && _mockFileSystem[sourceDirectory.ToLower()].Contains(sourceFilePath))
            {
                if (_mockFileSystem.ContainsKey(destinationDirectory.ToLower()))
                {
                    _mockFileSystem[sourceDirectory.ToLower()].Remove(sourceFilePath);
                    _mockFileSystem[destinationDirectory.ToLower()].Add(destinationFilePath);
                    return true;
                }
                else
                {
                    Console.WriteLine($"Destination directory not found: {destinationDirectory}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Source file not found: {sourceFilePath}");
                return false;
            }
        }

        public Dictionary<string, List<string>> GroupFilesByExtension(string directoryPath)
        {
            Console.WriteLine($"GroupFilesByExtension called with directoryPath: {directoryPath}");
            // Simulate grouping files by extension
            if (_mockFileSystem.ContainsKey(directoryPath.ToLower()))
            {
                return _mockFileSystem[directoryPath.ToLower()]
                    .GroupBy(Path.GetExtension)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            else
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return new Dictionary<string, List<string>>(); // Return empty dictionary if directory not found
            }
        }
    }
}

