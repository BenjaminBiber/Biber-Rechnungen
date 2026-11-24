using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

        migrationBuilder.CreateTable(
            name: "asp_net_roles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "audit_eintrag",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: true),
                benutzer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                entitaet = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                entitaet_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: true),
                aktion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                feld = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                alter_wert = table.Column<string>(type: "text", nullable: true),
                neuer_wert = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_eintrag", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "benachrichtigung",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                art = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                schweregrad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                titel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                nur_admins = table.Column<bool>(type: "boolean", nullable: false),
                entdoppelungs_schluessel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_benachrichtigung", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "benutzer",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                anzeigename = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                freigegeben = table.Column<bool>(type: "boolean", nullable: false),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                letzte_aktivitaet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_benutzer", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "di_verbrauch",
            columns: table => new
            {
                monat = table.Column<DateOnly>(type: "date", nullable: false),
                seiten = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_di_verbrauch", x => x.monat);
            });

        migrationBuilder.CreateTable(
            name: "einstellung",
            columns: table => new
            {
                schluessel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                wert = table.Column<string>(type: "jsonb", nullable: false),
                geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_einstellung", x => x.schluessel);
            });

        migrationBuilder.CreateTable(
            name: "export_lauf",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: false),
                benutzer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                von = table.Column<DateOnly>(type: "date", nullable: false),
                bis = table.Column<DateOnly>(type: "date", nullable: false),
                bezeichnung = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                inhalt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                anzahl_belege = table.Column<int>(type: "integer", nullable: false),
                dateipfad = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                dateiname = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_export_lauf", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "integritaets_lauf",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                gestartet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                beendet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                anzahl_dateien = table.Column<int>(type: "integer", nullable: false),
                anzahl_abweichungen = table.Column<int>(type: "integer", nullable: false),
                abweichungen = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_integritaets_lauf", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "kategorie",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                farbe = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                euer_zeile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                matching_algorithmus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                matching_muster = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                matching_gross_klein = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_kategorie", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "matching_modell",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ziel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                daten = table.Column<byte[]>(type: "bytea", nullable: false),
                trainiert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                anzahl_beispiele = table.Column<int>(type: "integer", nullable: false),
                guete = table.Column<double>(type: "double precision", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_matching_modell", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "stammartikel",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                basiseinheit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                grundpreis_menge = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stammartikel", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tag",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                farbe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ist_inbox = table.Column<bool>(type: "boolean", nullable: false),
                ist_zahlungs_tag = table.Column<bool>(type: "boolean", nullable: false),
                ist_system = table.Column<bool>(type: "boolean", nullable: false),
                matching_algorithmus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                matching_muster = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                matching_gross_klein = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tag", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_role_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "api_token",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                endung = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                zuletzt_genutzt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                widerrufen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_api_token", x => x.id);
                table.ForeignKey(
                    name: "fk_api_token_benutzer_benutzer_id",
                    column: x => x.benutzer_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_user_claims_benutzer_user_id",
                    column: x => x.user_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_logins",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_asp_net_user_logins_benutzer_user_id",
                    column: x => x.user_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_roles",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_benutzer_user_id",
                    column: x => x.user_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_tokens",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_asp_net_user_tokens_benutzer_user_id",
                    column: x => x.user_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "benachrichtigung_gelesen",
            columns: table => new
            {
                benachrichtigung_id = table.Column<Guid>(type: "uuid", nullable: false),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: false),
                gelesen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_benachrichtigung_gelesen", x => new { x.benachrichtigung_id, x.benutzer_id });
                table.ForeignKey(
                    name: "fk_benachrichtigung_gelesen_benachrichtigung_benachrichtigung_~",
                    column: x => x.benachrichtigung_id,
                    principalTable: "benachrichtigung",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_benachrichtigung_gelesen_benutzer_benutzer_id",
                    column: x => x.benutzer_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "gespeicherte_ansicht",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                definition = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_gespeicherte_ansicht", x => x.id);
                table.ForeignKey(
                    name: "fk_gespeicherte_ansicht_benutzer_benutzer_id",
                    column: x => x.benutzer_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "korrespondent",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                aliase = table.Column<List<string>>(type: "text[]", nullable: false),
                ust_id_nr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                steuernummer = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                ibans = table.Column<List<string>>(type: "text[]", nullable: false),
                anschrift_strasse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                anschrift_plz = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                anschrift_ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                anschrift_land = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                logo_pfad = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                logo_quelle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                default_kategorie_id = table.Column<Guid>(type: "uuid", nullable: true),
                ist_lieferant = table.Column<bool>(type: "boolean", nullable: false),
                ist_kunde = table.Column<bool>(type: "boolean", nullable: false),
                matching_algorithmus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                matching_muster = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                matching_gross_klein = table.Column<bool>(type: "boolean", nullable: false),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                geprueft = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_korrespondent", x => x.id);
                table.ForeignKey(
                    name: "fk_korrespondent_kategorie_default_kategorie_id",
                    column: x => x.default_kategorie_id,
                    principalTable: "kategorie",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "artikel",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                korrespondent_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                name_normalisiert = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                einheit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                stammartikel_id = table.Column<Guid>(type: "uuid", nullable: true),
                umrechnung_faktor = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_artikel", x => x.id);
                table.ForeignKey(
                    name: "fk_artikel_korrespondent_korrespondent_id",
                    column: x => x.korrespondent_id,
                    principalTable: "korrespondent",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_artikel_stammartikel_stammartikel_id",
                    column: x => x.stammartikel_id,
                    principalTable: "stammartikel",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "beleg",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                richtung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                belegart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                quelle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                import_vorgang_id = table.Column<Guid>(type: "uuid", nullable: true),
                korrespondent_id = table.Column<Guid>(type: "uuid", nullable: true),
                korrespondent_erkennung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                kategorie_id = table.Column<Guid>(type: "uuid", nullable: true),
                kategorie_manuell = table.Column<bool>(type: "boolean", nullable: false),
                titel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                rechnungsnummer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                rechnungsdatum = table.Column<DateOnly>(type: "date", nullable: true),
                leistungsdatum = table.Column<DateOnly>(type: "date", nullable: true),
                leistung_von = table.Column<DateOnly>(type: "date", nullable: true),
                leistung_bis = table.Column<DateOnly>(type: "date", nullable: true),
                faelligkeitsdatum = table.Column<DateOnly>(type: "date", nullable: true),
                waehrung = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                netto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                steuer = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                brutto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                extraktions_quelle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                di_konfidenz = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                extrahiert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                seitenanzahl = table.Column<int>(type: "integer", nullable: false),
                volltext = table.Column<string>(type: "text", nullable: true),
                duplikat_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                aufgeteilt_aus_beleg_id = table.Column<Guid>(type: "uuid", nullable: true),
                bestaetigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                bestaetigt_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                erstellt_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                suchvektor = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('german', coalesce(titel, '') || ' ' || coalesce(rechnungsnummer, '') || ' ' || coalesce(volltext, ''))", stored: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_beleg", x => x.id);
                table.ForeignKey(
                    name: "fk_beleg_beleg_aufgeteilt_aus_beleg_id",
                    column: x => x.aufgeteilt_aus_beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_beleg_duplikat_von_id",
                    column: x => x.duplikat_von_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_benutzer_bestaetigt_von_id",
                    column: x => x.bestaetigt_von_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_benutzer_erstellt_von_id",
                    column: x => x.erstellt_von_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_kategorie_kategorie_id",
                    column: x => x.kategorie_id,
                    principalTable: "kategorie",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_korrespondent_korrespondent_id",
                    column: x => x.korrespondent_id,
                    principalTable: "korrespondent",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "beleg_datei",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: false),
                rolle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                pfad = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                groesse = table.Column<long>(type: "bigint", nullable: false),
                mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                abgelegt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                letzte_pruefung_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                pruef_ergebnis = table.Column<bool>(type: "boolean", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_beleg_datei", x => x.id);
                table.ForeignKey(
                    name: "fk_beleg_datei_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "beleg_tag",
            columns: table => new
            {
                beleg_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                gesetzt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                gesetzt_von_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_beleg_tag", x => new { x.beleg_id, x.tag_id });
                table.ForeignKey(
                    name: "fk_beleg_tag_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_beleg_tag_benutzer_gesetzt_von_id",
                    column: x => x.gesetzt_von_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_beleg_tag_tag_tag_id",
                    column: x => x.tag_id,
                    principalTable: "tag",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "import_vorgang",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                quelle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                dateiname = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                groesse = table.Column<long>(type: "bigint", nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                staging_pfad = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                herkunft_pfad = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                mail_message_id = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                mail_absender = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                mail_betreff = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                benutzer_id = table.Column<Guid>(type: "uuid", nullable: true),
                api_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: true),
                fehler = table.Column<string>(type: "text", nullable: true),
                ergebnis = table.Column<string>(type: "text", nullable: true),
                versuche = table.Column<int>(type: "integer", nullable: false),
                naechster_versuch_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                beendet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                schritte = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_import_vorgang", x => x.id);
                table.ForeignKey(
                    name: "fk_import_vorgang_api_token_api_token_id",
                    column: x => x.api_token_id,
                    principalTable: "api_token",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_import_vorgang_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_import_vorgang_benutzer_benutzer_id",
                    column: x => x.benutzer_id,
                    principalTable: "benutzer",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "position",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: false),
                nr = table.Column<int>(type: "integer", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                text_normalisiert = table.Column<string>(type: "text", nullable: true),
                menge = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                einheit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                einzelpreis = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                gesamt = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                steuersatz = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                artikel_id = table.Column<Guid>(type: "uuid", nullable: true),
                grundpreis = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                grundpreis_einheit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_position", x => x.id);
                table.ForeignKey(
                    name: "fk_position_artikel_artikel_id",
                    column: x => x.artikel_id,
                    principalTable: "artikel",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_position_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "pruef_befund",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: false),
                pruefung = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ergebnis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                detail = table.Column<string>(type: "text", nullable: false),
                zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pruef_befund", x => x.id);
                table.ForeignKey(
                    name: "fk_pruef_befund_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "steuerzeile",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                beleg_id = table.Column<Guid>(type: "uuid", nullable: false),
                satz = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                netto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                steuer = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_steuerzeile", x => x.id);
                table.ForeignKey(
                    name: "fk_steuerzeile_beleg_beleg_id",
                    column: x => x.beleg_id,
                    principalTable: "beleg",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.InsertData(
            table: "asp_net_roles",
            columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
            values: new object[,]
            {
                { new Guid("a0000000-0000-0000-0000-000000000001"), "a0000000-0000-0000-0000-000000000001", "Admin", "ADMIN" },
                { new Guid("a0000000-0000-0000-0000-000000000002"), "a0000000-0000-0000-0000-000000000002", "Benutzer", "BENUTZER" },
                { new Guid("a0000000-0000-0000-0000-000000000003"), "a0000000-0000-0000-0000-000000000003", "NurLesen", "NURLESEN" }
            });

        migrationBuilder.InsertData(
            table: "tag",
            columns: new[] { "id", "farbe", "ist_inbox", "ist_system", "ist_zahlungs_tag", "name", "matching_algorithmus", "matching_gross_klein", "matching_muster" },
            values: new object[,]
            {
                { new Guid("b0000000-0000-0000-0000-000000000001"), "orange", false, true, false, "zu prüfen", "Keiner", false, null },
                { new Guid("b0000000-0000-0000-0000-000000000002"), "blau", true, false, false, "Inbox", "Keiner", false, null }
            });

        migrationBuilder.CreateIndex(
            name: "ix_api_token_benutzer_id",
            table: "api_token",
            column: "benutzer_id");

        migrationBuilder.CreateIndex(
            name: "ix_api_token_hash",
            table: "api_token",
            column: "hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_artikel_korrespondent_id_name_normalisiert",
            table: "artikel",
            columns: new[] { "korrespondent_id", "name_normalisiert" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_artikel_name_normalisiert_trgm",
            table: "artikel",
            column: "name_normalisiert")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_artikel_stammartikel_id",
            table: "artikel",
            column: "stammartikel_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_role_claims_role_id",
            table: "asp_net_role_claims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "role_name_index",
            table: "asp_net_roles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_claims_user_id",
            table: "asp_net_user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_logins_user_id",
            table: "asp_net_user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_roles_role_id",
            table: "asp_net_user_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "ix_audit_eintrag_beleg_id",
            table: "audit_eintrag",
            column: "beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_audit_eintrag_entitaet_entitaet_id",
            table: "audit_eintrag",
            columns: new[] { "entitaet", "entitaet_id" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_eintrag_zeitpunkt",
            table: "audit_eintrag",
            column: "zeitpunkt");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_aufgeteilt_aus_beleg_id",
            table: "beleg",
            column: "aufgeteilt_aus_beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_bestaetigt_von_id",
            table: "beleg",
            column: "bestaetigt_von_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_duplikat_von_id",
            table: "beleg",
            column: "duplikat_von_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_erstellt_von_id",
            table: "beleg",
            column: "erstellt_von_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_faelligkeitsdatum",
            table: "beleg",
            column: "faelligkeitsdatum");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_import_vorgang_id",
            table: "beleg",
            column: "import_vorgang_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_kategorie_id",
            table: "beleg",
            column: "kategorie_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_korrespondent_id",
            table: "beleg",
            column: "korrespondent_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_rechnungsdatum",
            table: "beleg",
            column: "rechnungsdatum");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_rechnungsnummer_trgm",
            table: "beleg",
            column: "rechnungsnummer")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_beleg_status",
            table: "beleg",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_suchvektor",
            table: "beleg",
            column: "suchvektor")
            .Annotation("Npgsql:IndexMethod", "gin");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_datei_beleg_id",
            table: "beleg_datei",
            column: "beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_datei_sha256",
            table: "beleg_datei",
            column: "sha256");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_tag_gesetzt_von_id",
            table: "beleg_tag",
            column: "gesetzt_von_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_tag_tag_id",
            table: "beleg_tag",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "ix_benachrichtigung_entdoppelungs_schluessel",
            table: "benachrichtigung",
            column: "entdoppelungs_schluessel",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_benachrichtigung_erstellt_am",
            table: "benachrichtigung",
            column: "erstellt_am");

        migrationBuilder.CreateIndex(
            name: "ix_benachrichtigung_gelesen_benutzer_id",
            table: "benachrichtigung_gelesen",
            column: "benutzer_id");

        migrationBuilder.CreateIndex(
            name: "email_index",
            table: "benutzer",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "user_name_index",
            table: "benutzer",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_export_lauf_erstellt_am",
            table: "export_lauf",
            column: "erstellt_am");

        migrationBuilder.CreateIndex(
            name: "ix_gespeicherte_ansicht_benutzer_id",
            table: "gespeicherte_ansicht",
            column: "benutzer_id");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_api_token_id",
            table: "import_vorgang",
            column: "api_token_id");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_beleg_id",
            table: "import_vorgang",
            column: "beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_benutzer_id",
            table: "import_vorgang",
            column: "benutzer_id");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_erstellt_am",
            table: "import_vorgang",
            column: "erstellt_am");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_sha256",
            table: "import_vorgang",
            column: "sha256");

        migrationBuilder.CreateIndex(
            name: "ix_import_vorgang_status",
            table: "import_vorgang",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_integritaets_lauf_gestartet_am",
            table: "integritaets_lauf",
            column: "gestartet_am");

        migrationBuilder.CreateIndex(
            name: "ix_kategorie_name",
            table: "kategorie",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_korrespondent_default_kategorie_id",
            table: "korrespondent",
            column: "default_kategorie_id");

        migrationBuilder.CreateIndex(
            name: "ix_korrespondent_name_trgm",
            table: "korrespondent",
            column: "name")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_korrespondent_ust_id_nr",
            table: "korrespondent",
            column: "ust_id_nr");

        migrationBuilder.CreateIndex(
            name: "ix_matching_modell_ziel_trainiert_am",
            table: "matching_modell",
            columns: new[] { "ziel", "trainiert_am" });

        migrationBuilder.CreateIndex(
            name: "ix_position_artikel_id",
            table: "position",
            column: "artikel_id");

        migrationBuilder.CreateIndex(
            name: "ix_position_beleg_id_nr",
            table: "position",
            columns: new[] { "beleg_id", "nr" });

        migrationBuilder.CreateIndex(
            name: "ix_pruef_befund_beleg_id",
            table: "pruef_befund",
            column: "beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_steuerzeile_beleg_id",
            table: "steuerzeile",
            column: "beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_tag_name",
            table: "tag",
            column: "name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_beleg_import_vorgang_import_vorgang_id",
            table: "beleg",
            column: "import_vorgang_id",
            principalTable: "import_vorgang",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        // Änderungsprotokoll ist append-only (B7): UPDATE und DELETE werden abgelehnt.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION audit_eintrag_unveraenderlich() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'audit_eintrag ist unveränderlich: % ist nicht erlaubt', TG_OP
                    USING ERRCODE = 'restrict_violation';
            END;
            $$;

            CREATE TRIGGER audit_eintrag_unveraenderlich
                BEFORE UPDATE OR DELETE ON audit_eintrag
                FOR EACH ROW EXECUTE FUNCTION audit_eintrag_unveraenderlich();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS audit_eintrag_unveraenderlich ON audit_eintrag;
            DROP FUNCTION IF EXISTS audit_eintrag_unveraenderlich();
            """);

        migrationBuilder.DropForeignKey(
            name: "fk_api_token_benutzer_benutzer_id",
            table: "api_token");

        migrationBuilder.DropForeignKey(
            name: "fk_beleg_benutzer_bestaetigt_von_id",
            table: "beleg");

        migrationBuilder.DropForeignKey(
            name: "fk_beleg_benutzer_erstellt_von_id",
            table: "beleg");

        migrationBuilder.DropForeignKey(
            name: "fk_import_vorgang_benutzer_benutzer_id",
            table: "import_vorgang");

        migrationBuilder.DropForeignKey(
            name: "fk_beleg_korrespondent_korrespondent_id",
            table: "beleg");

        migrationBuilder.DropForeignKey(
            name: "fk_beleg_import_vorgang_import_vorgang_id",
            table: "beleg");

        migrationBuilder.DropTable(
            name: "asp_net_role_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_logins");

        migrationBuilder.DropTable(
            name: "asp_net_user_roles");

        migrationBuilder.DropTable(
            name: "asp_net_user_tokens");

        migrationBuilder.DropTable(
            name: "audit_eintrag");

        migrationBuilder.DropTable(
            name: "beleg_datei");

        migrationBuilder.DropTable(
            name: "beleg_tag");

        migrationBuilder.DropTable(
            name: "benachrichtigung_gelesen");

        migrationBuilder.DropTable(
            name: "di_verbrauch");

        migrationBuilder.DropTable(
            name: "einstellung");

        migrationBuilder.DropTable(
            name: "export_lauf");

        migrationBuilder.DropTable(
            name: "gespeicherte_ansicht");

        migrationBuilder.DropTable(
            name: "integritaets_lauf");

        migrationBuilder.DropTable(
            name: "matching_modell");

        migrationBuilder.DropTable(
            name: "position");

        migrationBuilder.DropTable(
            name: "pruef_befund");

        migrationBuilder.DropTable(
            name: "steuerzeile");

        migrationBuilder.DropTable(
            name: "asp_net_roles");

        migrationBuilder.DropTable(
            name: "tag");

        migrationBuilder.DropTable(
            name: "benachrichtigung");

        migrationBuilder.DropTable(
            name: "artikel");

        migrationBuilder.DropTable(
            name: "stammartikel");

        migrationBuilder.DropTable(
            name: "benutzer");

        migrationBuilder.DropTable(
            name: "korrespondent");

        migrationBuilder.DropTable(
            name: "import_vorgang");

        migrationBuilder.DropTable(
            name: "api_token");

        migrationBuilder.DropTable(
            name: "beleg");

        migrationBuilder.DropTable(
            name: "kategorie");
    }
}
