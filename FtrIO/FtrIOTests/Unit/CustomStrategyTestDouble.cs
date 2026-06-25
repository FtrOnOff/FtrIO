namespace FtrIOTests.Unit
{
    using FtrIO.Interfaces;

    /// <summary>
    /// Test double for IToggleDecisionStrategy used to verify that
    /// WithStrategy correctly adds a custom strategy to the builder chain.
    /// </summary>
    public class CustomStrategyTestDouble : IToggleDecisionStrategy
    {
        public bool CanHandle(string rawValue) => rawValue == "custom";
        public bool ShouldExecute(string key, string rawValue) => true;
    }
}
