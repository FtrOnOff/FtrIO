namespace FtrIOTests.Unit
{
    using FtrIO.Interfaces;

    /// <summary>
    /// Test double for IFtrIOContextAccessor that returns configurable
    /// user ID and attribute values without any HTTP context dependency.
    /// </summary>
    public class FtrIOContextAccessorTestDouble : IFtrIOContextAccessor
    {
        private readonly string? _userId;
        private readonly Dictionary<string, string> _attributes;

        public FtrIOContextAccessorTestDouble(
            string? userId = null,
            Dictionary<string, string>? attributes = null)
        {
            _userId = userId;
            _attributes = attributes ?? new Dictionary<string, string>();
        }

        public string? GetUserId() => _userId;

        public string? GetAttribute(string name)
            => _attributes.TryGetValue(name, out var value) ? value : null;
    }
}
