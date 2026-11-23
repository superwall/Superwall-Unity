using System.Runtime.CompilerServices;

// Tests/Runtime compiles as its own assembly in Unity and exercises internal members.
[assembly: InternalsVisibleTo("Superwall.Tests")]
