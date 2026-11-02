using StudyFlow.Models;
using System.Diagnostics.CodeAnalysis;

namespace StudyFlow.Utilities
{
    /// <summary>
    /// Model Extensions
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Check if the subject is null or not saved yet.
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        public static bool IsNullOrNew([NotNullWhen(false)] this Subject? subject)
        {
            return subject is null || subject.ID == 0;
        }
    }
}
