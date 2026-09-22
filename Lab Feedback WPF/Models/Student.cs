using Lab_Feedback_WPF.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lab_Feedback_WPF.Models
{
    public class Student
    {
        public string FirstName { get; }
        public string LastName { get; }
        public string IdNumber { get; }

        public string? Folder { get; }

        public string Section { get; set; } = string.Empty;

        public string FullName => LastName + ", " + FirstName;

        public Student(string firstName, string lastName, string idNumber, string? folder)
        {
            FirstName = firstName;
            LastName = lastName;
            IdNumber = idNumber;
            Folder = folder;
        }

        public Student(string path, string? studentFolder)
        {
            var cleanPath = FileHandler.GetSubfolderFromPath(path, studentFolder);

            var splitName = cleanPath?.Split('_');

            LastName = splitName[0];
            FirstName = splitName[1].Split('-')[0];
            IdNumber = splitName[1].Split('-')[1];
            Folder = studentFolder;
        }

        /// <summary>
        /// True when a folder name parses cleanly as a student folder ("Last_First-IdNumber").
        /// Used to tell a student folder apart from a section folder.
        /// </summary>
        private static bool LooksLikeStudentFolder(string folderPath)
        {
            var name = Path.GetFileName(folderPath);
            var underscoreParts = name.Split('_');
            if (underscoreParts.Length < 2) return false;

            var afterUnderscore = underscoreParts[^1];
            var dashParts = afterUnderscore.Split('-');
            if (dashParts.Length < 2) return false;

            return dashParts[^1].All(char.IsDigit) && dashParts[^1].Length > 0;
        }

        /// <summary>
        /// Loads students from <paramref name="path"/>. If its immediate subfolders already look
        /// like student folders, behaves as a single (unsectioned) folder, same as before. Otherwise,
        /// each subfolder is treated as a section containing its own student folders.
        /// </summary>
        public static List<Student> GetStudentsFromFolders(string path)
        {
            List<Student> folders = new();

            try
            {
                if (Directory.Exists(path))
                {
                    string[] subFolders = Directory.GetDirectories(path);

                    if (subFolders.Any(LooksLikeStudentFolder))
                    {
                        foreach (var subFolder in subFolders)
                            folders.Add(new Student(path, subFolder));
                    }
                    else
                    {
                        foreach (var sectionFolder in subFolders)
                        {
                            var sectionName = Path.GetFileName(sectionFolder);
                            foreach (var studentFolder in Directory.GetDirectories(sectionFolder))
                            {
                                var student = new Student(sectionFolder, studentFolder)
                                {
                                    Section = sectionName
                                };
                                folders.Add(student);
                            }
                        }
                    }
                }
                else
                {
                    // TODO: Update this to WPF equivalent
                    // MessageBox.Show("Error");
                }
            }
            catch (Exception ex)
            {
                // TODO: Update this to WPF equivalent
                //MessageBox.Show("Error: " + ex.Message);
            }

            return folders;
        }
    }
}
