using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>An immutable, ordered event-saga definition (the itinerary as data).</summary>
public sealed class EventSagaDefinition
{
    internal EventSagaDefinition(string name, IReadOnlyList<Type> stepTypes, IReadOnlyList<DestinationBinding> destinations)
    {
        Name = name;
        StepTypes = stepTypes;
        Destinations = destinations;
    }

    /// <summary>The saga name.</summary>
    public string Name { get; }

    /// <summary>The ordered step types.</summary>
    public IReadOnlyList<Type> StepTypes { get; }

    /// <summary>
    /// The logical destinations this saga's steps address, declared via <see cref="EventSagaBuilder.SendsTo{TEvent}"/>.
    /// Registration binds each one whose type this process consumes, which is what keeps a step's send from being
    /// dropped as unroutable; a destination another service consumes is recorded but not bound.
    /// </summary>
    public IReadOnlyList<DestinationBinding> Destinations { get; }

    /// <summary>Render the itinerary as a Mermaid <c>flowchart</c> for documentation / visualization.</summary>
    public string ToMermaid()
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart LR");
        builder.Append("  start([start])");

        var previous = "start";
        for (var i = 0; i < StepTypes.Count; i++)
        {
            var id = $"s{i}";
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"  {previous} --> {id}[{StepTypes[i].Name}]");
            previous = id;
        }

        return builder.ToString();
    }
}
