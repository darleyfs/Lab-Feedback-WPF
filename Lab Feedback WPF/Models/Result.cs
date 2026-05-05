using System;
using System.Collections.Generic;
using System.Text;

namespace Lab_Feedback_WPF.Models
{
    internal class Result
    {
        public string Timestamp { get; }
        public int Number { get; }

        public Result(string timestamp, int number)
        {
            Timestamp = timestamp;
            Number = number;
        }

        public override string ToString()
        {
            return $"Timestamp: {Timestamp}, Number: {Number}";
        }
    }
}
