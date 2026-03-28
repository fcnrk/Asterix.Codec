#if NETSTANDARD2_0
// `record` types and `init` accessors require IsExternalInit, which ships with .NET 5+.
// This polyfill makes them compile on netstandard2.0 without any behavioural change.
// CS0436: suppress the "type conflicts with imported type" warning that IDEs may raise
// when the reference assemblies forward a version of this type.
#pragma warning disable CS0436
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#pragma warning restore CS0436
#endif
