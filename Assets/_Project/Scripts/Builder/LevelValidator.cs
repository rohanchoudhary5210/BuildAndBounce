using System.Collections.Generic;

namespace BuildAndBounce.Builder
{
    public struct ValidationResult
    {
        public bool IsValid;
        public string Message;

        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public static ValidationResult Success => new ValidationResult(true, "Course is valid and ready to race!");
    }

    /// <summary>
    /// Validates course requirements prior to Play testing or sharing:
    /// - Exactly one Start pad.
    /// - At least one Finish pad.
    /// </summary>
    public static class LevelValidator
    {
        public static ValidationResult Validate(IEnumerable<BlockData> blocks)
        {
            int startCount = 0;
            int finishCount = 0;

            foreach (var block in blocks)
            {
                if (block == null) continue;

                if (block.Category == BlockCategory.StartFinish)
                {
                    if (block.Id.ToLowerInvariant().Contains("start"))
                    {
                        startCount++;
                    }
                    else if (block.Id.ToLowerInvariant().Contains("finish"))
                    {
                        finishCount++;
                    }
                }
            }

            if (startCount == 0)
            {
                return new ValidationResult(false, "Course requires a Start Platform to race.");
            }

            if (startCount > 1)
            {
                return new ValidationResult(false, $"Course has {startCount} Start Platforms. Only exactly 1 is allowed.");
            }

            if (finishCount == 0)
            {
                return new ValidationResult(false, "Course requires at least one Finish Platform.");
            }

            return ValidationResult.Success;
        }
    }
}