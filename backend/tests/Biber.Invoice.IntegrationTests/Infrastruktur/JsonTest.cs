using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>JSON wie die API: camelCase, Enums als Namen. Lesen und Senden in Tests nur hierüber.</summary>
public static class JsonTest
{
    public static JsonSerializerOptions Optionen { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<T?> LesenAsync<T>(this HttpContent inhalt, CancellationToken ct = default) =>
        inhalt.ReadFromJsonAsync<T>(Optionen, ct);

    public static Task<T?> GetJsonAsync<T>(this HttpClient client, string pfad, CancellationToken ct = default) =>
        client.GetFromJsonAsync<T>(pfad, Optionen, ct);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client, string pfad, T wert, CancellationToken ct = default) =>
        client.PostAsJsonAsync(pfad, wert, Optionen, ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(
        this HttpClient client, string pfad, T wert, CancellationToken ct = default) =>
        client.PutAsJsonAsync(pfad, wert, Optionen, ct);
}
