using System.Text.Json.Serialization;
using Biber.Invoice.Domain.Module;

namespace Biber.Invoice.Api.Hosting;

/// <summary>JSON der Minimal APIs wie in <c>docs/api/openapi.json</c>: Eigenschaften camelCase, Enums als Namen wie im Code.</summary>
public sealed class JsonModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }
}
