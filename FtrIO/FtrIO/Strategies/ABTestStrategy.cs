namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class ABTestStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _contextAccessor;

        public ABTestStrategy(IFtrIOContextAccessor contextAccessor)
            => _contextAccessor = contextAccessor;

        public bool CanHandle(string rawValue)
        {
            if (!rawValue.StartsWith("ab:", StringComparison.OrdinalIgnoreCase)) return false;
            var valueSegments = rawValue[3..].Split(':', 2);
            return int.TryParse(valueSegments[0], out var parsedPercentage) && parsedPercentage is >= 0 and <= 100;
        }

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            var (rolloutPercentage, bucketingSalt) = ParseValue(rawValue);
            var currentUserId = _contextAccessor.GetUserId();

            if (currentUserId is null)
                return Random.Shared.Next(100) < rolloutPercentage;

            return ComputeBucket(currentUserId, toggleKey, bucketingSalt) < rolloutPercentage;
        }

        private static (int percentage, string salt) ParseValue(string rawValue)
        {
            var valueSegments = rawValue[3..].Split(':', 2);
            var rolloutPercentage = int.TryParse(valueSegments[0], out var parsedPercentage) ? parsedPercentage : 0;
            var bucketingSalt = valueSegments.Length > 1 ? valueSegments[1] : string.Empty;
            return (rolloutPercentage, bucketingSalt);
        }

        private static int ComputeBucket(string userId, string toggleKey, string salt = "")
        {
            var hashInput = string.IsNullOrEmpty(salt)
                ? $"{userId}:{toggleKey}"
                : $"{userId}:{toggleKey}:{salt}";
            var hashBytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(hashInput));
            return Math.Abs(BitConverter.ToInt32(hashBytes, 0)) % 100;
        }
    }
}
