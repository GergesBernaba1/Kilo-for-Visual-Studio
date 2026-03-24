using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class FileAttachmentService
    {
        private readonly string _workspaceRoot;
        private readonly string _attachmentsPath;
        private List<Attachment> _attachments = new List<Attachment>();

        public event EventHandler? AttachmentsChanged;

        public IReadOnlyList<Attachment> Attachments => _attachments;

        public FileAttachmentService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _attachmentsPath = Path.Combine(workspaceRoot, ".kilo", "attachments");
            EnsureDirectoryExists();
        }

        public Task<Attachment> AddFileAsync(string filePath, string description = "")
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(_attachmentsPath, $"{Guid.NewGuid():N}_{fileName}");

            File.Copy(filePath, destPath, true);

            var attachment = new Attachment
            {
                Id = Guid.NewGuid().ToString("N"),
                OriginalPath = filePath,
                StoredPath = destPath,
                FileName = fileName,
                Description = description,
                AddedAtUtc = DateTimeOffset.UtcNow,
                FileSize = new FileInfo(destPath).Length,
                ContentType = GetContentType(fileName)
            };

            _attachments.Add(attachment);
            AttachmentsChanged?.Invoke(this, EventArgs.Empty);

            return Task.FromResult(attachment);
        }

        public void AddImage(string imagePath, string description = "")
        {
            AddFileAsync(imagePath, description).Wait();
        }

        public void AddScreenshot(byte[] imageData, string description = "")
        {
            var screenshotPath = Path.Combine(_attachmentsPath, $"screenshot_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(screenshotPath, imageData);

            var attachment = new Attachment
            {
                Id = Guid.NewGuid().ToString("N"),
                OriginalPath = screenshotPath,
                StoredPath = screenshotPath,
                FileName = $"screenshot_{Guid.NewGuid():N}.png",
                Description = description,
                AddedAtUtc = DateTimeOffset.UtcNow,
                FileSize = imageData.Length,
                ContentType = "image/png"
            };

            _attachments.Add(attachment);
            AttachmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddTerminalOutput(string output, string description = "Terminal output")
        {
            var terminalPath = Path.Combine(_attachmentsPath, $"terminal_{Guid.NewGuid():N}.txt");
            File.WriteAllText(terminalPath, output);

            var attachment = new Attachment
            {
                Id = Guid.NewGuid().ToString("N"),
                OriginalPath = terminalPath,
                StoredPath = terminalPath,
                FileName = Path.GetFileName(terminalPath),
                Description = description,
                AddedAtUtc = DateTimeOffset.UtcNow,
                FileSize = output.Length,
                ContentType = "text/plain"
            };

            _attachments.Add(attachment);
            AttachmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Remove(string attachmentId)
        {
            for (int i = 0; i < _attachments.Count; i++)
            {
                if (_attachments[i].Id == attachmentId)
                {
                    var attachment = _attachments[i];
                    if (File.Exists(attachment.StoredPath))
                    {
                        try { File.Delete(attachment.StoredPath); } catch { }
                    }
                    _attachments.RemoveAt(i);
                    AttachmentsChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        public void Clear()
        {
            foreach (var attachment in _attachments)
            {
                if (File.Exists(attachment.StoredPath))
                {
                    try { File.Delete(attachment.StoredPath); } catch { }
                }
            }
            _attachments.Clear();
            AttachmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetAttachmentContent(string attachmentId)
        {
            foreach (var attachment in _attachments)
            {
                if (attachment.Id == attachmentId && File.Exists(attachment.StoredPath))
                {
                    if (attachment.ContentType.StartsWith("image/"))
                    {
                        return $"[Image: {attachment.FileName}]";
                    }
                    return File.ReadAllText(attachment.StoredPath);
                }
            }
            return string.Empty;
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_attachmentsPath))
            {
                Directory.CreateDirectory(_attachmentsPath);
            }
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".csv" => "text/csv",
                ".log" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }

    public class Attachment
    {
        public string Id { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset AddedAtUtc { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }
}