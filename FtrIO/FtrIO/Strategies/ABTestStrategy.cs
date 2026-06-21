namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class ABTestStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _accessor;

        public ABTestStrategy(IFtrIOContextAccessor accessor)
            => _accessor = accessor;

        public bool CanHandle(string rawValue)
        {
            if (!rawValue.StartsWith("ab:", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = rawValue[3..].Split(':', 2);
            return int.TryParse(parts[0], out var pct) && pct is >= 0 and <= 100;
        }

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            var (percentage, salt) = ParseValue(rawValue);
            var userId = _accessor.GetUserId();

            if (userId is null)
                return Random.Shared.Next(100) < percentage;

            return ComputeBucket(userId, toggleKey, salt) < percentage;
        }

        private static (int percentage, string salt) ParseValue(string rawValue)
        {
            var parts = rawValue[3..].Split(':', 2);
            var percentage = int.TryParse(parts[0], out var pct) ? pct : 0;
            var salt = parts.Length > 1 ? parts[1] : string.Empty;
            return (percentage, salt);
        }

        private static int ComputeBucket(string userId, string toggleKey, string salt = "")
        {
            var input = string.IsNullOrEmpty(salt)
                ? $"{userId}:{toggleKey}"
                : $"{userId}:{toggleKey}:{salt}";
            var hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(input));
            return Math.Abs(BitConverter.ToInt32(hash, 0)) % 100;
        }
    }
}
