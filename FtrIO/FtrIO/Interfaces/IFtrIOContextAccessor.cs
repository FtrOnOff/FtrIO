namespace FtrIO.Interfaces
{
    public interface IFtrIOContextAccessor
    {
        /// <summary>
        /// The unique identifier for the current user or request context.
        /// Returns null if no context is available (e.g. background jobs).
        /// </summary>
        string? GetUserId();

        /// <summary>
        /// Returns the value of a named attribute for the current context.
        /// e.g. GetAttribute("plan") → "premium"
        ///      GetAttribute("email") → "alice@example.com"
        ///      GetAttribute("country") → "IE"
        /// Returns null if the attribute is not available.
        /// </summary>
        string? GetAttribute(string name);
    }
}
