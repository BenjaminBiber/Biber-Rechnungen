using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>Alles, was die Prüfungen über einen Beleg wissen müssen.</summary>
/// <param name="Beleg">Mit Steuerzeilen und Positionen.</param>
/// <param name="KorrespondentIstNeu">Der zugeordnete Korrespondent ist noch von niemandem geprüft.</param>
/// <param name="KorrespondentBelege">Anzahl Belege des Korrespondenten einschließlich dieses Belegs.</param>
/// <param name="DuplikatVonNummer">Rechnungsnummer des Belegs, zu dem Dublettenverdacht besteht; sonst <c>null</c>.</param>
/// <param name="Kosit">KoSIT-Ergebnis aus der Pipeline; <c>null</c>, wenn nicht validiert wurde.</param>
/// <param name="Empfaenger">Rechnungsempfänger laut Extraktion; <c>null</c>, wenn nicht bekannt (dann nicht prüfbar).</param>
public sealed record PruefEingabe(
    Beleg Beleg,
    PruefEinstellungen Einstellungen,
    UstEinstellungen Ust,
    DateOnly Heute,
    bool KorrespondentIstNeu = false,
    int KorrespondentBelege = 0,
    string? DuplikatVonNummer = null,
    KositErgebnis? Kosit = null,
    ExtrahiertePartei? Empfaenger = null,
    Korrespondent? Korrespondent = null);
