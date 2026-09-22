namespace Lab_Feedback_WPF.Services
{
    public interface IExtractionProgress
    {
        ExtractionOperationToken AddOperation(string id, string title, int totalFiles);
        void UpdateOperation(string id, int current, string fileName);
        void CompleteOperation(string id);
        void FailOperation(string id, string message);
        bool IsVisible { get; }
        void Close();
    }

    public class ExtractionOperationToken
    {
        public CancellationToken CancellationToken { get; }
        public ExtractionOperationToken(CancellationToken ct) => CancellationToken = ct;
    }
}
