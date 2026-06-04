using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Lab_Feedback_WPF.Services
{
    internal class ZipFileHandler
    {
        public static async Task<bool> ExtractZipFilesInFolderWithProgressAsync(
            string? folderPath,
            IExtractionProgress? reporter = null)
        {
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("The specified folder does not exist.", "Error");
                return false;
            }

            var zipFiles = Directory.GetFiles(folderPath, "*.zip");
            if (zipFiles.Length == 0)
                return true;

            var operationId = Guid.NewGuid().ToString();
            var folderName = Path.GetFileName(folderPath) ?? folderPath;
            var token = reporter?.AddOperation(operationId, folderName, zipFiles.Length);

            try
            {
                for (int i = 0; i < zipFiles.Length; i++)
                {
                    if (token?.CancellationToken.IsCancellationRequested == true)
                        return false;

                    var zipFile = zipFiles[i];
                    var fileName = Path.GetFileName(zipFile);

                    reporter?.UpdateOperation(operationId, i + 1, $"Extracting: {fileName}");

                    try
                    {
                        await ExtractSingleZipFileAsync(
                            zipFile, folderPath,
                            token?.CancellationToken ?? CancellationToken.None);

                        File.Delete(zipFile);
                        reporter?.UpdateOperation(operationId, i + 1, $"Done: {fileName}");
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        reporter?.UpdateOperation(operationId, i + 1, $"Error: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }

                reporter?.CompleteOperation(operationId);
                return true;
            }
            catch (Exception ex)
            {
                reporter?.FailOperation(operationId, ex.Message);
                return false;
            }
        }

        private static async Task ExtractSingleZipFileAsync(
            string zipFile, string folderPath, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var archive = ZipFile.OpenRead(zipFile);
                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var destinationPath = Path.Combine(folderPath, entry.FullName);

                        try
                        {
                            if (entry.Name == "")
                            {
                                Directory.CreateDirectory(destinationPath);
                            }
                            else
                            {
                                var parentDir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                                    Directory.CreateDirectory(parentDir);

                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip files we can't access
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }, cancellationToken);
        }
    }
}
