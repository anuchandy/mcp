// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Abstractions;

// SYSLIB1100/1101 are analyzer diagnostics the binding source generator attaches to the Bind() call
// in our source because it cannot bind X509Certificate2 (via CredentialDescription.Certificate).
// Unlike SYSLIB0026/0027/0028 (compiler warnings in generated .g.cs, suppressed via NoWarn in the
// csproj), these are rooted in our code and can be suppressed with SuppressMessage.
[assembly: SuppressMessage("AOT", "SYSLIB1100", Justification = "X509Certificate2 in CredentialDescription is not bound at runtime; generator warning is expected.")]
[assembly: SuppressMessage("AOT", "SYSLIB1101", Justification = "X509Certificate2 in CredentialDescription is not bound at runtime; generator warning is expected.")]

namespace Microsoft.Mcp.Core.IdWebBinder;

public static class IdWebBinderHelper
{
    /// <summary>
    /// Creates a delegate that binds the given configuration section to
    /// <see cref="MicrosoftIdentityApplicationOptions"/>. The actual
    /// <see cref="ConfigurationBinder.Bind(IConfiguration, object)"/> call
    /// lives here so that the configuration binding source generator runs
    /// in this assembly (where the SYSLIB warnings are suppressed) rather
    /// than in the consuming assembly.
    /// </summary>
    public static Action<MicrosoftIdentityApplicationOptions> CreateBindAction(IConfigurationSection section)
    {
        return options => section.Bind(options);
    }
}
