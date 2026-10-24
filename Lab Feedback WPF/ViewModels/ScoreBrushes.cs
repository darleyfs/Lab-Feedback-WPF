using Lab_Feedback_WPF.Models;
using System.Windows.Media;

namespace Lab_Feedback_WPF.ViewModels
{
    public static class ScoreBrushes
    {
        private static readonly SolidColorBrush Green = Frozen(166, 226, 46);
        private static readonly SolidColorBrush Orange = Frozen(253, 151, 31);
        private static readonly SolidColorBrush Pink = Frozen(249, 38, 114);
        private static readonly SolidColorBrush Grey = Frozen(100, 100, 100);

        public static SolidColorBrush For(ScoreBand band) => band switch
        {
            ScoreBand.Full => Green,
            ScoreBand.Close => Orange,
            ScoreBand.Low => Pink,
            _ => Grey
        };

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
