using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Auth;

/// <summary>Bausteine für Tests mit echter Anmeldung: Clients mit Cookies, TOTP-Codes, Benutzer anlegen.</summary>
internal static class AuthTestHilfen
{
    public const string Passwort = "richtig-langes-passwort";

    /// <summary>Fester TOTP-Schlüssel (Base32) für Benutzer mit aktivem TOTP.</summary>
    public const string TotpSchluessel = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Client mit eigenem Cookie-Speicher, der Umleitungen nicht folgt.</summary>
    public static HttpClient Client(this ApiFabrik fabrik) =>
        fabrik.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>
    /// Setzt die Datenbank zurück (die Rollen kommen aus dem Seed) und stellt die Uhr
    /// auf jetzt – Identity rechnet TOTP und Sperren mit der Systemzeit, Cookies mit der Uhr der Anwendung.
    /// </summary>
    public static async Task VorbereitenAsync(this ApiFabrik fabrik)
    {
        fabrik.Zeit.Setze(DateTimeOffset.UtcNow);
        await fabrik.ZuruecksetzenAsync();
    }

    /// <summary>Legt einen Benutzer mit <see cref="Passwort"/> direkt über Identity an.</summary>
    public static async Task<Benutzer> BenutzerAnlegenAsync(
        this ApiFabrik fabrik,
        Rolle? rolle,
        bool freigegeben = true,
        bool mitTotp = false,
        string? email = null)
    {
        using var scope = fabrik.ErzeugeScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var benutzer = Testdaten.Benutzer(email, freigegeben: freigegeben);
        (await userManager.CreateAsync(benutzer, Passwort)).Succeeded.ShouldBeTrue();
        if (rolle is not null)
        {
            (await userManager.AddToRoleAsync(benutzer, rolle.ToString()!)).Succeeded.ShouldBeTrue();
        }

        if (mitTotp)
        {
            var store = (IUserAuthenticatorKeyStore<Benutzer>)scope.ServiceProvider.GetRequiredService<IUserStore<Benutzer>>();
            await store.SetAuthenticatorKeyAsync(benutzer, TotpSchluessel, CancellationToken.None);
            (await userManager.SetTwoFactorEnabledAsync(benutzer, true)).Succeeded.ShouldBeTrue();
        }

        return benutzer;
    }

    public static async Task<AnmeldeStatus> AnmeldenAsync(
        this HttpClient client, string email, string passwort = Passwort, bool angemeldetBleiben = false)
    {
        var antwort = await client.PostAsJsonAsync(
            ApiRouten.Auth.Anmelden, new AnmeldenDto(email, passwort, angemeldetBleiben), Json);
        antwort.EnsureSuccessStatusCode();
        return (await antwort.Content.ReadFromJsonAsync<AnmeldeErgebnisDto>(Json))!.Status;
    }

    public static async Task<AnmeldeStatus> TotpAsync(this HttpClient client, string code, bool geraetVertrauen = false)
    {
        var antwort = await client.PostAsJsonAsync(ApiRouten.Auth.Totp, new TotpDto(code, geraetVertrauen), Json);
        antwort.EnsureSuccessStatusCode();
        return (await antwort.Content.ReadFromJsonAsync<AnmeldeErgebnisDto>(Json))!.Status;
    }

    /// <summary>Client, der mit Passwort (und bei aktivem TOTP mit Code) angemeldet ist.</summary>
    public static async Task<HttpClient> AngemeldetAsync(this ApiFabrik fabrik, Benutzer benutzer)
    {
        var client = fabrik.Client();
        var status = await client.AnmeldenAsync(benutzer.Email!);
        if (status == AnmeldeStatus.TotpErforderlich)
        {
            status = await client.TotpAsync(TotpCode(TotpSchluessel));
        }

        status.ShouldBe(AnmeldeStatus.Ok);
        return client;
    }

    /// <summary>TOTP-Code nach RFC 6238 (SHA-1, 30 s, 6 Stellen) für jetzt plus <paramref name="schritte"/> Zeitschritte.</summary>
    public static string TotpCode(string base32Schluessel, int schritte = 0)
    {
        var zaehler = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + schritte;
        Span<byte> nachricht = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            nachricht[i] = (byte)(zaehler & 0xFF);
            zaehler >>= 8;
        }

        var hash = HMACSHA1.HashData(Base32(base32Schluessel), nachricht);
        var versatz = hash[^1] & 0x0F;
        var wert = ((hash[versatz] & 0x7F) << 24) | (hash[versatz + 1] << 16) | (hash[versatz + 2] << 8) | hash[versatz + 3];
        return (wert % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int puffer = 0, bits = 0;
        foreach (var zeichen in text.TrimEnd('=').ToUpperInvariant())
        {
            puffer = (puffer << 5) | alphabet.IndexOf(zeichen, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((puffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}
