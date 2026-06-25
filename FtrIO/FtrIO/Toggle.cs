namespace FtrIO
{
    using AspectInjector.Broker;
    using FtrIO.Classes;
    using FtrIO.Interfaces;

    /// <summary>
    /// Decorate a method with [Toggle] to gate it by its own method name.
    /// As of this version, FtrIO itself doubles as an AspectInjector aspect:
    /// the gating check is woven directly into the decorated method's IL at
    /// compile time, so the method is gated even when called directly -
    /// no call through FeatureToggle.ExecuteMethodIfToggleOn is required.
    ///
    /// Weaving happens per-compilation, but consumers don't need to reference
    /// AspectInjector themselves: FtrIO depends on it and flows its weaver
    /// targets transitively (via AspectInjector's buildTransitive assets), so
    /// a consumer's own [Toggle]-decorated methods are woven just by
    /// referencing the FtrIO package.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [Aspect(Scope.Global)]
    [Injection(typeof(Toggle))]
    public class Toggle : Attribute
    {
        [Advice(Kind.Around, Targets = Target.Method)]
        public object? Around(
            [Argument(Source.Name)] string name,
            [Argument(Source.Arguments)] object[] arguments,
            [Argument(Source.Target)] Func<object[], object> target,
            [Argument(Source.ReturnType)] Type returnType)
        {
            IToggleParser parser = ToggleParserProvider.Instance;

            if (parser.GetToggleStatus(name))
            {
                return target(arguments);
            }

            if (returnType == typeof(void))
            {
                return null;
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
