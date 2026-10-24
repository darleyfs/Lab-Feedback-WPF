namespace Lab_Feedback_WPF.Models
{
    public enum AssignmentKind
    {
        Lab,
        Practical
    }

    public class Assignment
    {
        public string? Name { get; }
        public string? Folder { get; }

        // Folders named "Lab ..." are Labs; everything else found (current "Practical..." and old
        // "Midterm_Practical..." / "Final..." names) is a Practical.
        public AssignmentKind Kind =>
            Name?.StartsWith("Lab ") == true ? AssignmentKind.Lab : AssignmentKind.Practical;

        public Assignment(string? name, string? folder)
        {
            Name = name;
            Folder = folder;
        }
    }
}
