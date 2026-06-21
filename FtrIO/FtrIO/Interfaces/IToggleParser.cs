namespace FtrIO.Interfaces
{
    public interface IToggleParser
    {
        bool GetToggleStatus(string toggle);

        bool ParseBoolValueFromSource(string status);

        /// <summary>
        /// Returns an explicit override value for the given toggle key and user ID,
        /// or null if no override exists. Default implementation returns null.
        /// </summary>
        bool? GetOverride(string toggleKey, string userId) => null;
    }
}