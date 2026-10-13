namespace JakuDownloader.Models;

/// <summary>
/// Avance de la instalacion de herramientas. <see cref="Fraction"/> es el avance de la herramienta actual
/// (null si el tamano no se conoce).
/// </summary>
public sealed record ToolInstallProgress(Tool Tool, int Index, int Count, ToolInstallStage Stage, double? Fraction = null)
{
    public double OverallPercent => (Index + (Stage == ToolInstallStage.Downloading ? Fraction ?? 0 : 1)) * 100.0 / Count;
}
