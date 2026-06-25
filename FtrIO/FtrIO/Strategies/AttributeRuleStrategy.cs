namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class AttributeRuleStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _contextAccessor;

        private static readonly string[] Operators =
            new[] { "equals", "notEquals", "startsWith", "endsWith", "contains", "in", "notIn" };

        public AttributeRuleStrategy(IFtrIOContextAccessor contextAccessor)
            => _contextAccessor = contextAccessor;

        public bool CanHandle(string rawValue)
            => rawValue.StartsWith("attribute:", StringComparison.OrdinalIgnoreCase)
            && Operators.Any(candidateOperator => rawValue.Contains($" {candidateOperator} ", StringComparison.OrdinalIgnoreCase));

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            if (!TryParseRule(rawValue, out var attributeName, out var comparisonOperator, out var expectedValue))
                return false;

            var attributeValue = _contextAccessor.GetAttribute(attributeName);
            if (attributeValue is null) return false;

            return comparisonOperator.ToLowerInvariant() switch
            {
                "equals"     => attributeValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
                "notequals"  => !attributeValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
                "startswith" => attributeValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
                "endswith"   => attributeValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
                "contains"   => attributeValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
                "in"         => expectedValue.Split(',', StringSplitOptions.TrimEntries)
                                     .Contains(attributeValue, StringComparer.OrdinalIgnoreCase),
                "notin"      => !expectedValue.Split(',', StringSplitOptions.TrimEntries)
                                      .Contains(attributeValue, StringComparer.OrdinalIgnoreCase),
                _            => false
            };
        }

        private static bool TryParseRule(string rawValue,
            out string attributeName, out string comparisonOperator, out string expectedValue)
        {
            attributeName = comparisonOperator = expectedValue = string.Empty;
            var ruleBody = rawValue["attribute:".Length..].Trim();

            foreach (var candidateOperator in Operators)
            {
                var operatorMarker = $" {candidateOperator} ";
                var operatorIndex = ruleBody.IndexOf(operatorMarker, StringComparison.OrdinalIgnoreCase);
                if (operatorIndex < 0) continue;

                attributeName      = ruleBody[..operatorIndex].Trim();
                comparisonOperator = candidateOperator;
                expectedValue      = ruleBody[(operatorIndex + operatorMarker.Length)..].Trim();
                return true;
            }

            return false;
        }
    }
}
