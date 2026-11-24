using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Biber.Invoice.Infrastructure.Module.Verfahrensdoku;

/// <summary>
/// Setzt Markdown (Überschriften, Absätze, Listen, Tabellen, Codeblöcke, Zitate, fett/kursiv) mit QuestPDF.
/// Als Schrift dient das in QuestPDF eingebettete Lato; Umgebungsschriften sind abgeschaltet, damit das
/// PDF unter Windows und Linux gleich aussieht.
/// </summary>
internal static class VerfahrensdokuPdf
{
    private const string Schrift = "Lato";
    private const float Grundgroesse = 10;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    static VerfahrensdokuPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public static byte[] Erzeugen(string markdown, string version, string softwareVersion)
    {
        var dokument = Markdown.Parse(markdown, Pipeline);

        return Document.Create(container => container.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(2, Unit.Centimetre);
            seite.DefaultTextStyle(s => s.FontFamily(Schrift).FontSize(Grundgroesse).LineHeight(1.3f));

            seite.Header().PaddingBottom(8).Text("Verfahrensdokumentation Biber-Rechnungen")
                .FontSize(9).FontColor(Colors.Grey.Darken1);

            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                Bloecke(spalte, dokument);
            });

            seite.Footer().Row(zeile =>
            {
                zeile.RelativeItem().Text($"Version {version} · Software-Version {softwareVersion}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                zeile.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Seite ");
                    text.CurrentPageNumber();
                    text.Span(" von ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Bloecke(ColumnDescriptor spalte, ContainerBlock bloecke)
    {
        foreach (var block in bloecke)
        {
            Block(spalte, block);
        }
    }

    private static void Block(ColumnDescriptor spalte, Block block)
    {
        switch (block)
        {
            case HeadingBlock ueberschrift:
                var groesse = ueberschrift.Level switch { 1 => 20f, 2 => 15f, 3 => 12.5f, _ => 11f };
                spalte.Item().PaddingTop(ueberschrift.Level <= 2 ? 8 : 4).Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(groesse).Bold());
                    Inlines(text, ueberschrift.Inline, fett: true, kursiv: false);
                });
                break;
            case ParagraphBlock absatz:
                spalte.Item().Text(text => Inlines(text, absatz.Inline, fett: false, kursiv: false));
                break;
            case ListBlock liste:
                spalte.Item().Element(c => Liste(c, liste));
                break;
            case Table tabelle:
                spalte.Item().Element(c => Tabelle(c, tabelle));
                break;
            case CodeBlock code:
                spalte.Item().Background(Colors.Grey.Lighten4).Padding(6)
                    .Text(string.Join('\n', code.Lines.Lines.Take(code.Lines.Count).Select(z => z.ToString())))
                    .FontSize(9);
                break;
            case QuoteBlock zitat:
                spalte.Item().BorderLeft(2).BorderColor(Colors.Grey.Lighten1).PaddingLeft(8)
                    .Column(inner =>
                    {
                        inner.Spacing(4);
                        Bloecke(inner, zitat);
                    });
                break;
            case ThematicBreakBlock:
                spalte.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                break;
            case ContainerBlock container:
                Bloecke(spalte, container);
                break;
            case LeafBlock { Inline: not null } blatt:
                spalte.Item().Text(text => Inlines(text, blatt.Inline, fett: false, kursiv: false));
                break;
        }
    }

    private static void Liste(IContainer container, ListBlock liste)
    {
        container.PaddingLeft(6).Column(spalte =>
        {
            spalte.Spacing(2);
            var nummer = int.TryParse(liste.OrderedStart, out var start) ? start : 1;
            foreach (var eintrag in liste.OfType<ListItemBlock>())
            {
                var marke = liste.IsOrdered ? $"{nummer++}." : "•";
                spalte.Item().Row(zeile =>
                {
                    zeile.ConstantItem(18).Text(marke);
                    zeile.RelativeItem().Column(inner =>
                    {
                        inner.Spacing(2);
                        Bloecke(inner, eintrag);
                    });
                });
            }
        });
    }

    private static void Tabelle(IContainer container, Table tabelle)
    {
        var zeilen = tabelle.OfType<TableRow>().ToList();
        var spalten = Math.Max(1, zeilen.Count == 0 ? 1 : zeilen.Max(z => z.Count));

        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (var i = 0; i < spalten; i++)
                {
                    c.RelativeColumn();
                }
            });

            foreach (var zeile in zeilen)
            {
                for (var i = 0; i < spalten; i++)
                {
                    var zelle = i < zeile.Count ? zeile[i] as TableCell : null;
                    var kopf = zeile.IsHeader;
                    var feld = t.Cell()
                        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                        .Background(kopf ? Colors.Grey.Lighten3 : Colors.White)
                        .Padding(4);
                    feld.Column(inner =>
                    {
                        if (zelle is null)
                        {
                            return;
                        }

                        foreach (var block in zelle)
                        {
                            if (block is LeafBlock { Inline: not null } blatt)
                            {
                                inner.Item().Text(text => Inlines(text, blatt.Inline, fett: kopf, kursiv: false));
                            }
                        }
                    });
                }
            }
        });
    }

    private static void Inlines(TextDescriptor text, ContainerInline? inlines, bool fett, bool kursiv)
    {
        if (inlines is null)
        {
            return;
        }

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Span(text, literal.Content.ToString(), fett, kursiv);
                    break;
                case CodeInline code:
                    Span(text, code.Content, fett, kursiv).BackgroundColor(Colors.Grey.Lighten3);
                    break;
                case LineBreakInline:
                    text.Span("\n");
                    break;
                case EmphasisInline betonung:
                    Inlines(
                        text,
                        betonung,
                        fett || betonung.DelimiterCount >= 2,
                        kursiv || betonung.DelimiterCount is 1 or 3);
                    break;
                case ContainerInline container:
                    Inlines(text, container, fett, kursiv);
                    break;
                case HtmlInline:
                    break;
            }
        }
    }

    private static TextSpanDescriptor Span(TextDescriptor text, string inhalt, bool fett, bool kursiv)
    {
        var span = text.Span(inhalt);
        if (fett)
        {
            span.Bold();
        }

        if (kursiv)
        {
            span.Italic();
        }

        return span;
    }
}
