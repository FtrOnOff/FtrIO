namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class AttributeRuleStrategy : IToggleDecisionStrategy
    {
        private readonly IFtrIOContextAccessor _accessor;

        private static readonly string[] Operators =
            new[] { "equals", "notEquals", "startsWith", "endsWith", "contains", "in", "notIn" };

        public AttributeRuleStrategy(IFtrIOContextAccessor accessor)
            => _accessor = accessor;

        public bool CanHandle(string rawValue)
            => rawValue.StartsWith("attribute:", StringComparison.OrdinalIgnoreCase)
            && Operators.Any(op => rawValue.Contains($" {op} ", StringComparison.OrdinalIgnoreCase));

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            if (!TryParseRule(rawValue, out var attribute, out var op, out var value))
                return false;

            var attributeValue = _accessor.GetAttribute(attribute);
            if (attributeValue is null) return false;

            return op.ToLowerInvariant() switch
            {
                "equals"     => attributeValue.Equals(value, StringComparison.OrdinalIgnoreCase),
                "notequals"  => !attributeValue.Equals(value, StringComparison.OrdinalIgnoreCase),
                "startswith" => attributeValue.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                "endswith"   => attributeValue.EndsWith(value, StringComparison.OrdinalIgnoreCase),
                "contains"   => attributeValue.Contains(value, StringComparison.OrdinalIgnoreCase),
                "in"         => value.Split(',', StringSplitOptions.TrimEntries)
                                     .Contains(attributeValue, StringComparer.OrdinalIgnoreCase),
                "notin"      => !value.Split(',', StringSplitOptions.TrimEntries)
                                      .Contains(attributeValue, StringComparer.OrdinalIgnoreCase),
                _            => false
            };
        }

        private static bool TryParseRule(string rawValue,
            out string attribute, out string op, out string value)
        {
            attribute = op = value = string.Empty;
            var body = rawValue["attribute:".Length..].Trim();

            foreach (var candidate in Operators)
            {
                var marker = $" {candidate} ";
                var idx = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                attribute = body[..idx].Trim();
                op        = candidate;
                value     = body[(idx + marker.Length)..].Trim();
                return true;
            }

            return false;
        }
    }
}
