using Lab_Feedback_WPF.Views;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Provides static methods for extracting ZIP files from a specified folder, displaying extraction progress, and
    /// supporting user cancellation.
    /// </summary>
    /// <remarks>This class is intended for internal use in scenarios where batch extraction of ZIP archives
    /// is required with user feedback and cancellation support. Extraction progress is displayed in a dialog, and users
    /// can cancel the operation at any time. After successful extraction, ZIP files are deleted. Errors encountered
    /// during extraction of individual files are reported, but do not halt the overall extraction process.</remarks>
    internal class ZipFileHandler
    {
        /// <summary>
        /// Extracts all ZIP files in the specified folder, displaying a progress dialog and allowing the user to cancel
        /// the operation.
        /// </summary>
        /// <remarks>If the specified folder does not exist, the method displays an error message and
        /// returns false. The user is prompted to confirm extraction if ZIP files are found. Extraction progress is
        /// shown in a dialog, and the user can cancel the operation at any time. Each ZIP file is deleted after
        /// successful extraction. Errors during extraction of individual files are reported in the progress dialog, but
        /// the method continues processing remaining files.</remarks>
        /// <param name="folderPath">The path to the folder containing ZIP files to extract. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if all ZIP files were extracted
        /// successfully or if no ZIP files were found; otherwise, false.</returns>
        public static async Task<bool> ExtractZipFilesInFolderWithProgressAsync(string? folderPath,
            ExtractionProgressDialog? existingDialog = null) 
        {
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("The specified folder does not exist.", "Error");
                return false;
            }

            var zipFiles = Directory.GetFiles(folderPath, "*.zip");
            if (zipFiles.Length == 0)
                return true;

            var progressDialog = existingDialog ?? new ExtractionProgressDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (!progressDialog.IsVisible)
            {
                progressDialog = new ExtractionProgressDialog
                {
                    Owner = Application.Current.MainWindow
                };
                progressDialog.Show();
            }

            // Register this folder as a new operation
            var operationId = Guid.NewGuid().ToString();
            var folderName = Path.GetFileName(folderPath) ?? folderPath;
            var operation = progressDialog.AddOperation(operationId, folderName, zipFiles.Length);

            try
            {
                for (int i = 0; i < zipFiles.Length; i++)
                {
                    // If token is None (reporter was disposed), exit gracefully
                    if (token?.CancellationToken == CancellationToken.None)
                        return true;

                    if (token?.CancellationToken.IsCancellationRequested == true)
                        return false;

                    var zipFile = zipFiles[i];
                    var fileName = Path.GetFileName(zipFile);

                    progressDialog.UpdateOperation(operationId, i + 1, $"Extracting: {fileName}");

                    try
                    {
                        await ExtractSingleZipFileAsync(zipFile, folderPath,
                            operation.CancellationToken);

                        File.Delete(zipFile);
                        progressDialog.UpdateOperation(operationId, i + 1, $"Done: {fileName}");
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        progressDialog.UpdateOperation(operationId, i + 1,
                            $"Error: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }

                progressDialog.CompleteOperation(operationId);
                return true;
            }
            catch (Exception ex)
            {
                progressDialog.FailOperation(operationId, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Asynchronously extracts all entries from a ZIP archive to the specified folder, overwriting existing files
        /// as needed.
        /// </summary>
        /// <remarks>Entries that cannot be accessed due to insufficient permissions are skipped.
        /// Directory entries in the archive are created as needed. If an entry already exists in the destination, it
        /// will be overwritten.</remarks>
        /// <param name="zipFile">The path to the ZIP file to extract. Must refer to a valid ZIP archive file.</param>
        /// <param name="folderPath">The directory where the contents of the ZIP file will be extracted. The directory and any necessary
        /// subdirectories will be created if they do not exist.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the extraction operation.</param>
        /// <returns>A task that represents the asynchronous extraction operation.</returns>
        private static async Task ExtractSingleZipFileAsync(string zipFile, string folderPath,
            CancellationToken cancellationToken)
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
                    // Cancellation was requested; allow graceful exit
                    throw;
                }
            }, cancellationToken);
        }
    }
}