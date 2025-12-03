using System;
using Xunit;

namespace Grex.UITests
{
    /// <summary>
    /// Marker attribute for UI-focused tests while still leveraging xUnit's <see cref="FactAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UITestMethodAttribute : FactAttribute
    {
    }
}

