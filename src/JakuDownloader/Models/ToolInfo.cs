namespace JakuDownloader.Models;

public sealed record ToolInfo(string Name, string Description, string? Version, bool IsInstalled)
{
    public string Status => IsInstalled ? Version ?? "Instalado" : "No encontrado";
}
