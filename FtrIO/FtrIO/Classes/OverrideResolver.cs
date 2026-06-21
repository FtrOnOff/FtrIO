namespace FtrIO.Classes
{
    using FtrIO.Interfaces;

    public class OverrideResolver
    {
        private readonly IFtrIOContextAccessor _accessor;
        private readonly IToggleParser _configReader;

        public OverrideResolver(IFtrIOContextAccessor accessor, IToggleParser configReader)
        {
            _accessor = accessor;
            _configReader = configReader;
        }

        /// <summary>
        /// Checks TogglesOverrides for an explicit value for the current user and toggle key.
        /// Returns null if no override exists or if no user context is available.
        /// </summary>
        public bool? GetOverride(string toggleKey)
        {
            var userId = _accessor.GetUserId();
            if (userId is null) return null;
            return _configReader.GetOverride(toggleKey, userId);
        }
    }
}
