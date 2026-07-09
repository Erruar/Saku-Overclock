using Microsoft.Windows.ApplicationModel.Resources;

namespace Saku_Overclock.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader ResourceLoader = new();

    /// <summary>
    ///     Translate resource by key
    /// </summary>
    /// <param name="resourceKey">ResourceKey</param>
    /// <returns>Translated resource</returns>
    public static string GetLocalized(this string resourceKey) => ResourceLoader.GetString(resourceKey);
}