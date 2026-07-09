namespace Saku_Overclock.Contracts.Services;

public interface IPageService
{
    /// <summary>
    ///     Get page type by ViewModel name
    /// </summary>
    /// <param name="key">Page ViewModel name</param>
    /// <returns>Page type</returns>
    Type GetPageType(string key);
}