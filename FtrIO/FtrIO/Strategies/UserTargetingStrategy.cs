namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class UserTargetingStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _contextAccessor;

        public UserTargetingStrategy(IFtrIOContextAccessor contextAccessor)
            => _contextAccessor = contextAccessor;

        public bool CanHandle(string rawValue)
            => rawValue.StartsWith("users:", StringComparison.OrdinalIgnoreCase);

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            var currentUserId = _contextAccessor.GetUserId();
            if (currentUserId is null) return false;

            var allowedUserIds = rawValue["users:".Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            return allowedUserIds.Contains(currentUserId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
