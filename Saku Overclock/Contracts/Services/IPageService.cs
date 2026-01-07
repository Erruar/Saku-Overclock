namespace Saku_Overclock.Contracts.Services;

public interface IPageService
{
    /// <summary>
    ///     Получает тип страницы по имени ViewModel
    /// </summary>
    /// <param name="key">Имя ViewModel страницы</param>
    /// <returns>Тип страницы</returns>
    Type GetPageType(string key);
}