using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>An event for the harness suites — asserted through <c>MessagingTestHarness</c>, so its handler needs no collector.</summary>
public sealed record HarnessEvent(string Tag) : IEvent;
