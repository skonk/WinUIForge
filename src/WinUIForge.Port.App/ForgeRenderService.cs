using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using WinUIForge.Port.Core;

namespace WinUIForge.Port.App;

internal sealed class ForgeRenderService
{
    public ForgeRenderResult Render(string source)
    {
        try
        {
            var document = new ForgeXamlDocument(source);
            var loaded = XamlReader.Load(source);
            if (loaded is not UIElement root)
            {
                return ForgeRenderResult.Failure(
                    source,
                    $"XAML root rendered as {loaded?.GetType().FullName ?? "null"}, not UIElement.");
            }

            return ForgeRenderResult.Success(source, document, root);
        }
        catch (Exception e)
        {
            return ForgeRenderResult.Failure(source, e.Message, e);
        }
    }
}

internal sealed record ForgeRenderResult(
    bool Succeeded,
    string Source,
    ForgeXamlDocument? Document,
    UIElement? Root,
    string? Error,
    Exception? Exception)
{
    public static ForgeRenderResult Success(string source, ForgeXamlDocument document, UIElement root) =>
        new(true, source, document, root, null, null);

    public static ForgeRenderResult Failure(string source, string error, Exception? exception = null) =>
        new(false, source, null, null, error, exception);
}
