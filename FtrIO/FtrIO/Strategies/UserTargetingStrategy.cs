namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class UserTargetingStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _accessor;

        public UserTargetingStrategy(IFtrIOContextAccessor accessor)
            => _accessor = accessor;

        public bool CanHandle(string rawValue)
            => rawValue.StartsWith("users:", StringComparison.OrdinalIgnoreCase);

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            var currentUser = _accessor.GetUserId();
            if (currentUser is null) return false;

            var allowedUsers = rawValue["users:".Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            return allowedUsers.Contains(currentUser, StringComparer.OrdinalIgnoreCase);
        }
    }
}
