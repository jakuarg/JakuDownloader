namespace JakuDownloader.ViewModels;

public enum PreviewState
{
    /// <summary>Sin link valido: no se muestra la vista previa.</summary>
    None,
    Loading,
    Ready,
    Failed,
}
